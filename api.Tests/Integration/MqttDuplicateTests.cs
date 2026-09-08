using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Services;
using Api.Test.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Integration;

public class MqttDuplicateTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;
    private readonly Mock<ILogger<MqttEventHandler>> _logger = new();

    public async ValueTask InitializeAsync()
    {
        (_container, string cs) = await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(cs);
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(cs);
        _db = new DatabaseUtilities(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private async Task<MqttInspectionRecordResult> Create(IsarInspectionResultMessage message)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope
            .ServiceProvider.GetRequiredService<IInspectionRecordService>()
            .CreateFromMqttMessage(message);
    }

    private InspectionRecordService Service(SaraDbContext context) =>
        new(
            context,
            Mock.Of<IAnalysisTriggerService>(),
            Options.Create(new AnalysisOptions()),
            NullLogger<InspectionRecordService>.Instance
        );

    private SaraDbContext ContextWith(IInterceptor interceptor) =>
        new(
            new DbContextOptionsBuilder<SaraDbContext>()
                .UseNpgsql(
                    _container.GetConnectionString(),
                    o => o.EnableRetryOnFailure(1, TimeSpan.Zero, null)
                )
                .AddInterceptors(interceptor)
                .Options
        );

    private async Task Process(IsarInspectionResultMessage message)
    {
        using var handler = new MqttEventHandler(
            _logger.Object,
            _factory.Services.GetRequiredService<IServiceScopeFactory>()
        );
        try
        {
            await handler.ProcessIsarInspectionResult(message);
        }
        finally
        {
            handler.Unsubscribe();
        }
    }

    private void AssertLog(LogLevel level, string text) =>
        Assert.Contains(
            _logger.Invocations,
            i =>
                i.Method.Name == "Log"
                && (LogLevel)i.Arguments[0] == level
                && i.Arguments[2].ToString()!.Contains(text)
        );

    private void AssertNoWarningsOrErrors() =>
        Assert.DoesNotContain(
            _logger.Invocations,
            i => i.Method.Name == "Log" && (LogLevel)i.Arguments[0] >= LogLevel.Warning
        );

    [Fact]
    public async Task Duplicate_DoesNotCreateRecordsOrRetriggerAnalyses()
    {
        var message = _db.NewIsarInspectionResultMessage(requiredAnalysis: ["per-record-test"]);
        await Process(message);

        await Process(message);

        Assert.Equal(
            1,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, await _context.Analyses.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            1,
            await _context.AnalysisRuns.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, await _context.Workflows.CountAsync(TestContext.Current.CancellationToken));
        Assert.Single(_factory.ArgoWorkflowClient.Requests);
        AssertLog(LogLevel.Information, "Ignoring duplicate");
        AssertNoWarningsOrErrors();
    }

    [Fact]
    public async Task NoAnalysisDuplicate_UsesSanitizedInspectionId()
    {
        var message = _db.NewIsarInspectionResultMessage(inspectionId: "test-\r\ninspection");
        await Process(message);
        message.InspectionId = "test-inspection";

        await Process(message);

        Assert.Equal(
            1,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
        AssertLog(LogLevel.Information, "Ignoring duplicate");
        AssertNoWarningsOrErrors();
    }

    [Fact]
    public async Task Duplicate_WithDifferentMetadata_IsSkippedWithoutChangingStoredRecord()
    {
        var message = _db.NewIsarInspectionResultMessage(requiredAnalysis: ["per-record-test"]);
        await Process(message);
        var originalBlobName = message.InspectionDataPath.BlobName;
        var originalRobotName = message.RobotName;
        message.InspectionDataPath.BlobName = "other.jpg";
        message.RobotName = "other-robot";
        message.TargetPosition = new Position(1, 2, 3);
        message.RequiredAnalysis = ["group-test"];

        await Process(message);

        AssertLog(LogLevel.Information, "Ignoring duplicate");
        AssertNoWarningsOrErrors();
        Assert.Single(_factory.ArgoWorkflowClient.Requests);
        var record = await _context
            .InspectionRecords.Include(r => r.Analyses)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(originalBlobName, record.BlobStorageLocation.BlobName);
        Assert.Equal(originalRobotName, record.RobotName);
        Assert.Equal("per-record-test", Assert.Single(record.Analyses).AnalysisType);
    }

    [Fact]
    public async Task SubmissionFailure_DuplicateDoesNotRetrySideEffects()
    {
        var message = _db.NewIsarInspectionResultMessage(requiredAnalysis: ["per-record-test"]);
        _factory.ArgoWorkflowClient.CreateException = new InvalidOperationException(
            "Argo unavailable"
        );
        await Process(message);
        AssertLog(LogLevel.Error, "triggering analyses");
        _logger.Invocations.Clear();
        _factory.ArgoWorkflowClient.CreateException = null;

        await Process(message);

        AssertLog(LogLevel.Information, "Ignoring duplicate");
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(
            1,
            await _context.AnalysisRuns.CountAsync(TestContext.Current.CancellationToken)
        );
        AssertNoWarningsOrErrors();
    }

    [Fact]
    public async Task GroupDuplicate_IsSkippedWhileWaitingOrAfterInterruptedCompletion()
    {
        var group = _db.NewAnalysisGroupMessage();
        var first = _db.NewIsarInspectionResultMessage(
            requiredAnalysis: ["group-test"],
            analysisGroup: group
        );
        await Process(first);
        await Process(first);

        Assert.Equal(
            1,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, await _context.Analyses.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
        AssertLog(LogLevel.Information, "Ignoring duplicate");
        AssertNoWarningsOrErrors();
        _logger.Invocations.Clear();
        await Create(
            _db.NewIsarInspectionResultMessage(
                inspectionId: "second",
                requiredAnalysis: ["group-test"],
                analysisGroup: group
            )
        );
        await Process(first);

        AssertLog(LogLevel.Information, "Ignoring duplicate");
        AssertNoWarningsOrErrors();
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentInsert_UniqueConstraintRollsBackLosingSideEffects(bool grouped)
    {
        var first = _db.NewIsarInspectionResultMessage(
            requiredAnalysis: ["group-test"],
            analysisGroup: grouped ? _db.NewAnalysisGroupMessage() : null
        );

        var barrier = new ConcurrentSaveBarrier();
        async Task<MqttInspectionRecordResult> Insert()
        {
            await using var context = ContextWith(barrier);
            var result = await Service(context).CreateFromMqttMessage(first);
            // A reused context must not re-save any rolled-back entities.
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            return result;
        }

        var results = await Task.WhenAll(Insert(), Insert());

        Assert.Single(results, r => r is { IsDuplicate: false });
        Assert.Single(results, r => r.IsDuplicate);
        Assert.Equal(1, barrier.UniqueViolations);
        Assert.Equal(
            1,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, await _context.Analyses.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            grouped ? 1 : 0,
            await _context.AnalysisGroups.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.False(await _context.AnalysisRuns.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GroupedCreation_SavesOncePerRecordAndReusesExistingAnalyses()
    {
        var saves = new SaveCounter();
        await using var context = ContextWith(saves);
        var service = Service(context);
        var group = _db.NewAnalysisGroupMessage();
        var first = await service.CreateFromMqttMessage(
            _db.NewIsarInspectionResultMessage(
                requiredAnalysis: ["group-test", "group-test"],
                analysisGroup: group
            )
        );
        Assert.Equal(1, saves.Count);

        var second = await service.CreateFromMqttMessage(
            _db.NewIsarInspectionResultMessage(
                inspectionId: "second",
                requiredAnalysis: ["group-test", "per-record-test"],
                analysisGroup: group
            )
        );

        Assert.Equal(2, saves.Count);
        Assert.False(first.IsDuplicate);
        Assert.False(second.IsDuplicate);
        Assert.Single(first.Record.Analyses);
        Assert.Equal(2, second.Record.Analyses.Count);
        Assert.Contains(second.Record.Analyses, a => a.Id == first.Record.Analyses[0].Id);
        Assert.Equal(2, second.Record.AnalysisGroup!.Analyses.Count);
        Assert.Equal(
            2,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(2, await _context.Analyses.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            1,
            await _context.AnalysisGroups.CountAsync(TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransactionRetry_PreservesOneRecordAndSkipsUncertainCommit(bool committed)
    {
        await using var context = ContextWith(
            committed ? new FailFirstCommitAcknowledgement() : new FailFirstRecordSave()
        );
        var message = _db.NewIsarInspectionResultMessage(
            requiredAnalysis: ["group-test"],
            analysisGroup: _db.NewAnalysisGroupMessage(size: 1)
        );
        var result = await Service(context).CreateFromMqttMessage(message);

        Assert.Equal(committed, result.IsDuplicate);
        await Process(message);
        AssertLog(LogLevel.Information, "Ignoring duplicate");
        AssertNoWarningsOrErrors();
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(
            1,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, await _context.Analyses.CountAsync(TestContext.Current.CancellationToken));
        Assert.False(await _context.AnalysisRuns.AnyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            1,
            await _context.AnalysisGroups.CountAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SqliteInMemoryMode_CreatesAndRecognizesReplay()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<SaraDbContext>().UseSqlite(connection).Options;
        await using var context = new SaraDbContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = Service(context);
        var message = _db.NewIsarInspectionResultMessage();

        var first = await service.CreateFromMqttMessage(message);
        var replay = await service.CreateFromMqttMessage(message);

        Assert.False(first.IsDuplicate);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(first.Record.Id, replay.Record.Id);
        Assert.Equal(
            1,
            await context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UnrelatedDatabaseFailure_IsNotClassifiedAsDuplicateAndRollsBackGroup()
    {
        var message = _db.NewIsarInspectionResultMessage(
            requiredAnalysis: ["group-test"],
            analysisGroup: _db.NewAnalysisGroupMessage()
        );
        message.InspectionDataPath.StorageAccount = null!;

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => Create(message));

        Assert.Equal(
            PostgresErrorCodes.NotNullViolation,
            Assert.IsType<PostgresException>(exception.InnerException).SqlState
        );
        Assert.False(
            await _context.InspectionRecords.AnyAsync(TestContext.Current.CancellationToken)
        );
        Assert.False(await _context.Analyses.AnyAsync(TestContext.Current.CancellationToken));
        Assert.False(await _context.AnalysisGroups.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreationFailures_AreLoggedAsErrors(bool databaseFailure)
    {
        var service = new Mock<IInspectionRecordService>();
        Exception failure = databaseFailure
            ? new DbUpdateException("database failed")
            : new InvalidOperationException("creation failed");
        service
            .Setup(s => s.CreateFromMqttMessage(It.IsAny<IsarInspectionResultMessage>()))
            .ThrowsAsync(failure);
        var trigger = new Mock<IAnalysisTriggerService>();
        var services = new ServiceCollection()
            .AddSingleton(service.Object)
            .AddSingleton(trigger.Object)
            .AddSingleton<IBlobStorageService>(_factory.BlobStorageService);
        using var provider = services.BuildServiceProvider();
        using var handler = new MqttEventHandler(
            _logger.Object,
            provider.GetRequiredService<IServiceScopeFactory>()
        );
        try
        {
            await handler.ProcessIsarInspectionResult(_db.NewIsarInspectionResultMessage());
        }
        finally
        {
            handler.Unsubscribe();
        }

        AssertLog(LogLevel.Error, failure.Message);
        trigger.Verify(t => t.OnInspectionRecordCreated(It.IsAny<InspectionRecord>()), Times.Never);
    }

    private sealed class FailFirstCommitAcknowledgement : DbTransactionInterceptor
    {
        private bool _failed;

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default
        )
        {
            if (!_failed)
            {
                _failed = true;
                throw new TimeoutException("Commit persisted but acknowledgement was lost");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class SaveCounter : SaveChangesInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailFirstRecordSave : SaveChangesInterceptor
    {
        private bool _failed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                !_failed
                && eventData
                    .Context!.ChangeTracker.Entries<InspectionRecord>()
                    .Any(e => e.State == EntityState.Added)
            )
            {
                _failed = true;
                throw new TimeoutException("Transient failure before saving the inspection graph");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ConcurrentSaveBarrier : SaveChangesInterceptor
    {
        private readonly HashSet<Guid> _contexts = [];
        private readonly TaskCompletionSource _ready = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private int _uniqueViolations;
        public int UniqueViolations => _uniqueViolations;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            bool firstSave;
            lock (_contexts)
            {
                firstSave = _contexts.Add(eventData.Context!.ContextId.InstanceId);
                if (_contexts.Count == 2)
                    _ready.TrySetResult();
            }
            if (firstSave)
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }

        public override Task SaveChangesFailedAsync(
            DbContextErrorEventData eventData,
            CancellationToken cancellationToken = default
        )
        {
            if (
                eventData.Exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation
                }
            )
                Interlocked.Increment(ref _uniqueViolations);
            return Task.CompletedTask;
        }
    }
}
