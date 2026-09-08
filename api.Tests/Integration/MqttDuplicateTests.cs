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

    private void AssertNoErrors() =>
        Assert.DoesNotContain(
            _logger.Invocations,
            i => i.Method.Name == "Log" && (LogLevel)i.Arguments[0] >= LogLevel.Error
        );

    [Fact]
    public async Task SubmittedReplay_DoesNotCreateAnotherRecordAnalysisRunOrWorkflow()
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
        AssertNoErrors();
    }

    [Fact]
    public async Task CompletedReplay_WithLaterPendingRerun_IsStillHandled()
    {
        var message = _db.NewIsarInspectionResultMessage(requiredAnalysis: ["per-record-test"]);
        await Process(message);
        var run = await _context
            .AnalysisRuns.Include(r => r.Analysis)
            .SingleAsync(TestContext.Current.CancellationToken);
        run.Status = AnalysisRunStatus.Succeeded;
        await _db.NewAnalysisRun(run.Analysis, runNumber: 2);

        await Process(message);

        Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(
            2,
            await _context.AnalysisRuns.CountAsync(TestContext.Current.CancellationToken)
        );
        AssertLog(LogLevel.Information, "Ignoring duplicate");
        AssertNoErrors();
    }

    [Fact]
    public async Task NoAnalysisReplay_NormalizesStoredFieldsAndTimestampPrecision()
    {
        var message = _db.NewIsarInspectionResultMessage(
            inspectionId: "test-\r\ninspection",
            missionName: "test-\r\nmission",
            tagId: "test-\r\ntag",
            installationCode: "T\r\nST",
            inspectionDescription: "test-\r\ndescription",
            robotPose: new Pose(new Position(1, 2, 3), new Orientation(0, 0, 0, 1)),
            targetPosition: new Position(4, 5, 6)
        );
        message.MissionId = "mission-\r\nid";
        message.Timestamp = new DateTime(2026, 9, 7, 11, 0, 30, DateTimeKind.Utc).AddTicks(1234567);
        await Process(message);
        message.InspectionId = "test-inspection";
        message.MissionName = "test-mission";
        message.TagId = "test-tag";
        message.InstallationCode = "TST";
        message.InspectionDescription = "test-description";
        message.MissionId = "mission-id";
        message.RequiredAnalysis = [];
        // These fields are not persisted by ingestion and cannot establish a conflict.
        message.IsarId = "different-isar";
        message.Duration = 123;
        message.FileType = "different-file-type";

        await Process(message);

        Assert.Equal(
            1,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
        AssertLog(LogLevel.Information, "Ignoring duplicate");
        AssertNoErrors();
    }

    [Theory]
    [InlineData("blob")]
    [InlineData("metadata")]
    [InlineData("pose")]
    [InlineData("analysis")]
    public async Task ConflictingReplay_RemainsAnError(string conflict)
    {
        var message = _db.NewIsarInspectionResultMessage(requiredAnalysis: ["per-record-test"]);
        await Process(message);
        switch (conflict)
        {
            case "blob":
                message.InspectionDataPath.BlobName = "other.jpg";
                break;
            case "metadata":
                message.RobotName = "other-robot";
                break;
            case "pose":
                message.TargetPosition = new Position(1, 2, 3);
                break;
            case "analysis":
                message.RequiredAnalysis = ["group-test"];
                break;
        }

        await Process(message);

        AssertLog(LogLevel.Error, "Conflicting MQTT inspection result");
        Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(
            1,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task RecordWithoutRun_ReplayWarnsAndDoesNotTrigger()
    {
        var message = _db.NewIsarInspectionResultMessage(requiredAnalysis: ["per-record-test"]);
        await Create(message);

        await Process(message);

        AssertLog(LogLevel.Warning, "no initial run");
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
        Assert.False(await _context.AnalysisRuns.AnyAsync(TestContext.Current.CancellationToken));
        AssertNoErrors();
    }

    [Fact]
    public async Task SubmissionFailure_ReplayWarnsWithoutRetryingSideEffects()
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

        AssertLog(LogLevel.Warning, "is Failed");
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(
            1,
            await _context.AnalysisRuns.CountAsync(TestContext.Current.CancellationToken)
        );
        AssertNoErrors();
    }

    [Fact]
    public async Task PartiallySubmittedRecord_ReplayWarnsWithoutRepeatingSuccessfulSubmission()
    {
        var message = _db.NewIsarInspectionResultMessage(
            requiredAnalysis: ["per-record-test", "multi-step-test"]
        );
        var created = await Create(message);
        using (var scope = _factory.Services.CreateScope())
        {
            var trigger = scope.ServiceProvider.GetRequiredService<IAnalysisTriggerService>();
            await trigger.RerunAnalysis(created.Record.Analyses[0].Id);
        }

        await Process(message);

        AssertLog(LogLevel.Warning, "no initial run");
        Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(
            1,
            await _context.AnalysisRuns.CountAsync(TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeferredGroupReplay_DoesNotAddMembersOrRuns(bool complete)
    {
        var group = _db.NewAnalysisGroupMessage();
        var first = _db.NewIsarInspectionResultMessage(
            requiredAnalysis: ["group-test"],
            analysisGroup: group
        );
        await Process(first);
        if (complete)
            await Process(
                _db.NewIsarInspectionResultMessage(
                    inspectionId: "second",
                    requiredAnalysis: ["group-test"],
                    analysisGroup: group
                )
            );

        await Process(first);

        Assert.Equal(
            complete ? 2 : 1,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, await _context.Analyses.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(complete ? 1 : 0, _factory.ArgoWorkflowClient.Requests.Count);
        AssertLog(LogLevel.Information, "Ignoring duplicate");
        AssertNoErrors();
    }

    [Theory]
    [InlineData("full")]
    [InlineData("expired")]
    [InlineData("timed-out")]
    public async Task GroupWithoutSubmissionWhenNoLongerWaiting_ReplayWarns(string state)
    {
        var group = _db.NewAnalysisGroupMessage(size: state == "full" ? 1 : 2);
        var message = _db.NewIsarInspectionResultMessage(
            requiredAnalysis: ["group-test"],
            analysisGroup: group
        );
        await Create(message);
        var storedGroup = await _context.AnalysisGroups.SingleAsync(
            TestContext.Current.CancellationToken
        );
        if (state == "expired")
            storedGroup.TimeoutAt = DateTime.UtcNow.AddMinutes(-1);
        if (state == "timed-out")
            storedGroup.Status = AnalysisGroupStatus.TimedOut;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Process(message);

        AssertLog(LogLevel.Warning, "no initial run");
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
    }

    [Fact]
    public async Task AddedAnalysis_DoesNotMakeOriginalReplayConflictOrTriggerAgain()
    {
        var message = _db.NewIsarInspectionResultMessage(requiredAnalysis: ["per-record-test"]);
        await Process(message);
        var record = await _context.InspectionRecords.SingleAsync(
            TestContext.Current.CancellationToken
        );
        await _db.NewAnalysis(type: "multi-step-test", inspectionRecords: [record]);

        await Process(message);

        AssertLog(LogLevel.Information, "Ignoring duplicate");
        Assert.Single(_factory.ArgoWorkflowClient.Requests);
        AssertNoErrors();
    }

    [Theory]
    [InlineData("none")]
    [InlineData("new")]
    [InlineData("existing")]
    [InlineData("conflicting")]
    public async Task ConcurrentInsert_UniqueConstraintRollsBackLosingSideEffects(string groupMode)
    {
        if (groupMode == "existing")
            await _db.NewAnalysisGroup();
        var first = _db.NewIsarInspectionResultMessage(
            requiredAnalysis: ["group-test"],
            analysisGroup: groupMode == "none" ? null : _db.NewAnalysisGroupMessage()
        );
        var second = _db.NewIsarInspectionResultMessage(
            requiredAnalysis: ["group-test"],
            blobName: first.InspectionDataPath.BlobName,
            analysisGroup: groupMode == "none"
                ? null
                : _db.NewAnalysisGroupMessage(
                    groupId: groupMode == "conflicting" ? "other-group" : "test-group"
                )
        );
        second.Timestamp = first.Timestamp;

        var barrier = new ConcurrentSaveBarrier();
        async Task<MqttInspectionRecordResult?> Insert(IsarInspectionResultMessage message)
        {
            var options = new DbContextOptionsBuilder<SaraDbContext>()
                .UseNpgsql(
                    _container.GetConnectionString(),
                    options => options.EnableRetryOnFailure()
                )
                .AddInterceptors(barrier)
                .Options;
            await using var context = new SaraDbContext(options);
            var service = new InspectionRecordService(
                context,
                Mock.Of<IAnalysisTriggerService>(),
                _factory.Services.GetRequiredService<IOptions<AnalysisOptions>>(),
                NullLogger<InspectionRecordService>.Instance
            );
            try
            {
                var result = await service.CreateFromMqttMessage(message);
                // A reused context must not re-save any rolled-back entities.
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                return result;
            }
            catch (InvalidOperationException ex) when (groupMode == "conflicting")
            {
                Assert.Contains("Conflicting MQTT inspection result", ex.Message);
                return null;
            }
        }

        var results = await Task.WhenAll(Insert(first), Insert(second));

        Assert.Single(results, r => r is { IsDuplicate: false });
        Assert.Single(
            results,
            r => groupMode == "conflicting" ? r is null : r is { IsDuplicate: true }
        );
        Assert.Equal(1, barrier.UniqueViolations);
        Assert.Equal(
            1,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, await _context.Analyses.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            groupMode == "none" ? 0 : 1,
            await _context.AnalysisGroups.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.False(await _context.AnalysisRuns.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UncertainCommit_RetryRecognizesRecordButReportsMissingAnalysisRun()
    {
        var options = new DbContextOptionsBuilder<SaraDbContext>()
            .UseNpgsql(
                _container.GetConnectionString(),
                o => o.EnableRetryOnFailure(1, TimeSpan.Zero, null)
            )
            .AddInterceptors(new FailFirstCommitAcknowledgement())
            .Options;
        await using var context = new SaraDbContext(options);
        var service = new InspectionRecordService(
            context,
            Mock.Of<IAnalysisTriggerService>(),
            _factory.Services.GetRequiredService<IOptions<AnalysisOptions>>(),
            NullLogger<InspectionRecordService>.Instance
        );
        var message = _db.NewIsarInspectionResultMessage(requiredAnalysis: ["per-record-test"]);

        var result = await service.CreateFromMqttMessage(message);

        Assert.True(result.IsDuplicate);
        Assert.Contains(result.IncompleteAnalyses, a => a.Contains("no initial run"));
        await Process(message);
        AssertLog(LogLevel.Warning, "no initial run");
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(
            1,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, await _context.Analyses.CountAsync(TestContext.Current.CancellationToken));
        Assert.False(await _context.AnalysisRuns.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TransientFailure_RetriesWholeTransactionWithoutOrphanEntities()
    {
        var options = new DbContextOptionsBuilder<SaraDbContext>()
            .UseNpgsql(
                _container.GetConnectionString(),
                o => o.EnableRetryOnFailure(1, TimeSpan.Zero, null)
            )
            .AddInterceptors(new FailFirstRecordSave())
            .Options;
        await using var context = new SaraDbContext(options);
        var service = new InspectionRecordService(
            context,
            Mock.Of<IAnalysisTriggerService>(),
            _factory.Services.GetRequiredService<IOptions<AnalysisOptions>>(),
            NullLogger<InspectionRecordService>.Instance
        );
        var message = _db.NewIsarInspectionResultMessage(
            requiredAnalysis: ["group-test"],
            analysisGroup: _db.NewAnalysisGroupMessage()
        );

        var result = await service.CreateFromMqttMessage(message);

        Assert.False(result.IsDuplicate);
        Assert.Equal(
            1,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(1, await _context.Analyses.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            1,
            await _context.AnalysisGroups.CountAsync(TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData(AnalysisRunStatus.Pending, false)]
    [InlineData(AnalysisRunStatus.InProgress, false)]
    [InlineData(AnalysisRunStatus.InProgress, true)]
    public async Task UnconfirmedInitialRun_ReplayWarns(
        AnalysisRunStatus status,
        bool includeWorkflow
    )
    {
        var message = _db.NewIsarInspectionResultMessage(requiredAnalysis: ["per-record-test"]);
        await Create(message);
        var analysis = await _context.Analyses.SingleAsync(TestContext.Current.CancellationToken);
        var run = await _db.NewAnalysisRun(analysis);
        run.Status = status;
        if (includeWorkflow)
            run.Workflows.Add(
                new Workflow
                {
                    AnalysisRun = run,
                    StepNumber = 1,
                    WorkflowType = "per-record-test",
                    InputBlobStorageLocations = [],
                    ArgoWorkflowName = "name-without-confirmed-uid",
                }
            );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Process(message);

        AssertLog(LogLevel.Warning, "incomplete or unconfirmed");
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(
            1,
            await _context.AnalysisRuns.CountAsync(TestContext.Current.CancellationToken)
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
        var service = new InspectionRecordService(
            context,
            Mock.Of<IAnalysisTriggerService>(),
            Options.Create(new AnalysisOptions()),
            NullLogger<InspectionRecordService>.Instance
        );
        var message = _db.NewIsarInspectionResultMessage();

        var first = await service.CreateFromMqttMessage(message);
        var replay = await service.CreateFromMqttMessage(message);

        Assert.False(first.IsDuplicate);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(first.Record.Id, replay.Record.Id);
        Assert.Empty(replay.IncompleteAnalyses);
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
                throw new TimeoutException("Transient failure after group and analysis saves");
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
