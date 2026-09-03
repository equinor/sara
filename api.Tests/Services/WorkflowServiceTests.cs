using System;
using System.Text.Json;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using Api.Test.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services;

public class WorkflowServiceTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;

    public async ValueTask InitializeAsync()
    {
        (_container, string cs) = await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(cs);
        _ = _factory.Services;
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

    private IWorkflowService ResolveService(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IWorkflowService>();

    private async Task TriggerWorkflowInScope(Workflow workflow)
    {
        using var scope = _factory.Services.CreateScope();
        await ResolveService(scope).TriggerWorkflow(workflow);
    }

    private async Task OnWorkflowCompletedInScope(Workflow workflow)
    {
        using var scope = _factory.Services.CreateScope();
        await ResolveService(scope).OnWorkflowCompleted(workflow);
    }

    [Fact]
    public async Task TriggerWorkflow_UnknownWorkflowType_Throws()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "unknown-type",
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );
        await Assert.ThrowsAsync<InvalidOperationException>(() => TriggerWorkflowInScope(workflow));
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
    }

    [Fact]
    public async Task TriggerWorkflow_WithoutInputs_ThrowsClearError()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "test-workflow-1",
            inputBlobStorageLocations: []
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TriggerWorkflowInScope(workflow)
        );

        Assert.Contains("has no input blob storage locations", exception.Message);
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
    }

    [Fact]
    public async Task TriggerWorkflow_HappyPathNoEnricher_PostsPayloadAndMarksInProgress()
    {
        const string workflowType = "test-workflow-1";
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: workflowType,
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );
        _factory.ArgoWorkflowClient.BeforeCreate = async request =>
        {
            await using var persistedContext = TestSetupHelpers.ConfigurePostgreSqlContext(
                _container.GetConnectionString()
            );
            var persisted = await persistedContext.Workflows.SingleAsync(
                item => item.Id == workflow.Id,
                TestContext.Current.CancellationToken
            );
            Assert.Equal(request.WorkflowName, persisted.ArgoWorkflowName);
            Assert.Equal(WorkflowStatus.InProgress, persisted.Status);
            Assert.NotNull(persisted.StartedAt);
            Assert.Null(persisted.ArgoWorkflowUid);
        };

        await TriggerWorkflowInScope(workflow);

        await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);

        var request = Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal($"sara-{workflow.Id:N}", request.WorkflowName);
        Assert.Equal(_factory.WorkflowTemplateNameFor(workflowType), request.WorkflowTemplateName);
        Assert.Equal(workflow.Id.ToString(), request.Arguments["workflowId"]);
        Assert.Equal(workflowType, request.Arguments["workflowType"]);
        Assert.Equal(
            JsonValueKind.Array,
            JsonDocument.Parse(request.Arguments["inputBlobStorageLocations"]).RootElement.ValueKind
        );
        Assert.Equal(
            JsonValueKind.Object,
            JsonDocument.Parse(request.Arguments["outputBlobStorageLocation"]).RootElement.ValueKind
        );
        Assert.Equal(WorkflowStatus.InProgress, workflow.Status);
        Assert.NotNull(workflow.StartedAt);
        Assert.NotNull(workflow.ArgoWorkflowName);
        Assert.NotNull(workflow.ArgoWorkflowUid);

        using var doc = JsonDocument.Parse(request.Arguments["extras"]);
        var extras = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, extras.ValueKind);
        Assert.Empty(extras.EnumerateObject());
    }

    [Fact]
    public async Task TriggerWorkflow_PayloadIncludesWorkflowType()
    {
        const string workflowType = "test-workflow-1";
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: workflowType,
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );

        await TriggerWorkflowInScope(workflow);

        var request = Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(workflowType, request.Arguments["workflowType"]);
    }

    [Fact]
    public async Task TriggerUtilitiesWorkflow_PayloadIncludesOperationInExtras()
    {
        const string workflowType = "copy-raw-to-visualized";
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: workflowType,
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );

        await TriggerWorkflowInScope(workflow);

        var request = Assert.Single(_factory.ArgoWorkflowClient.Requests);
        using var extras = JsonDocument.Parse(request.Arguments["extras"]);
        Assert.Equal(workflowType, extras.RootElement.GetProperty("operation").GetString());
    }

    [Fact]
    public async Task TriggerWorkflow_HappyPathWithEnricher_PayloadIncludesEnrichedFields()
    {
        const string installationCode = "HUA";
        const string tag = "tag-42";
        const string inspectionDescription = "thermal-spot";
        var record = await _db.NewInspectionRecord(
            installationCode: installationCode,
            tag: tag,
            inspectionDescription: inspectionDescription
        );
        await _db.NewThermalReferenceMetadata(
            installationCode: installationCode,
            tagId: tag,
            inspectionDescription: inspectionDescription
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "thermal-reading",
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );

        await TriggerWorkflowInScope(workflow);

        var request = Assert.Single(_factory.ArgoWorkflowClient.Requests);

        using var doc = JsonDocument.Parse(request.Arguments["extras"]);
        var extras = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, extras.ValueKind);
        Assert.True(extras.TryGetProperty("referenceImageBlobStorageLocation", out _));
        Assert.True(extras.TryGetProperty("referencePolygonBlobStorageLocation", out _));
    }

    [Fact]
    public async Task TriggerWorkflow_ArgoReturnsError_MarksWorkflowAndRunFailed()
    {
        _factory.ArgoWorkflowClient.CreateException = new InvalidOperationException("Argo error");
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "test-workflow-1",
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );

        await Assert.ThrowsAsync<WorkflowTriggerFailedException>(() =>
            TriggerWorkflowInScope(workflow)
        );

        await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowStatus.Failed, workflow.Status);
        Assert.NotNull(workflow.ErrorMessage);
        Assert.Equal(AnalysisRunStatus.Failed, run.Status);
    }

    [Fact]
    public async Task OnWorkflowCompleted_WorkflowNotFound_DoesNothing()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "test-workflow-1",
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );
        await OnWorkflowCompletedInScope(workflow);

        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
        Assert.Empty(_factory.MqttPublisher.AnalysisResultMessages);
    }

    [Fact]
    public async Task OnWorkflowCompleted_FailedWorkflow_MarksRunFailed()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "test-workflow-1");
        workflow.Status = WorkflowStatus.Failed;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await OnWorkflowCompletedInScope(workflow);

        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisRunStatus.Failed, run.Status);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public async Task FinalizeWorkflowCompleted_StaleExecutionDoesNotOverwriteRetry()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "test-workflow-1");
        workflow.Status = WorkflowStatus.InProgress;
        workflow.ArgoWorkflowName = "old-name";
        workflow.ArgoWorkflowUid = "old-uid";
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var staleCompletion = new Workflow
        {
            Id = workflow.Id,
            AnalysisRunId = run.Id,
            AnalysisRun = run,
            StepNumber = workflow.StepNumber,
            WorkflowType = workflow.WorkflowType,
            InputBlobStorageLocations = [],
            Status = WorkflowStatus.Succeeded,
            ArgoWorkflowName = "old-name",
            ArgoWorkflowUid = "old-uid",
            ResultJson = "{}",
            CompletedAt = DateTime.UtcNow,
        };

        workflow.Status = WorkflowStatus.InProgress;
        workflow.ArgoWorkflowName = "retry-name";
        workflow.ArgoWorkflowUid = "retry-uid";
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var finalized = await ResolveService(scope).FinalizeWorkflowCompleted(staleCompletion);

        workflow = await _context
            .Workflows.AsNoTracking()
            .SingleAsync(
                candidate => candidate.Id == workflow.Id,
                TestContext.Current.CancellationToken
            );
        Assert.False(finalized);
        Assert.Equal(WorkflowStatus.InProgress, workflow.Status);
        Assert.Equal("retry-name", workflow.ArgoWorkflowName);
        Assert.Equal("retry-uid", workflow.ArgoWorkflowUid);
        Assert.Null(workflow.ResultJson);
    }

    [Fact]
    public async Task OnWorkflowCompleted_SucceededWithNextWorkflow_TriggersNextWorkflow()
    {
        const string nextWorkflowType = "test-workflow-2";
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var firstWorkflow = await _db.NewWorkflow(
            run,
            workflowType: "test-workflow-1",
            stepNumber: 1
        );
        firstWorkflow.Status = WorkflowStatus.Succeeded;
        var output = _db.NewBlobStorageLocation();
        firstWorkflow.ResultJson = JsonSerializer.Serialize(
            new
            {
                outputBlobStorageLocation = new
                {
                    storageAccount = output.StorageAccount,
                    blobContainer = output.BlobContainer,
                    blobName = output.BlobName,
                },
            }
        );
        await _db.NewWorkflow(
            run,
            workflowType: nextWorkflowType,
            stepNumber: 2,
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await OnWorkflowCompletedInScope(firstWorkflow);

        var request = Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(
            _factory.WorkflowTemplateNameFor(nextWorkflowType),
            request.WorkflowTemplateName
        );
    }

    [Fact]
    public async Task OnWorkflowCompleted_SucceededWithNoNextWorkflow_MarksRunSucceeded()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "test-workflow-1", stepNumber: 1);
        workflow.Status = WorkflowStatus.Succeeded;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await OnWorkflowCompletedInScope(workflow);

        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisRunStatus.Succeeded, run.Status);
        Assert.NotNull(run.CompletedAt);
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
    }

    [Fact]
    public async Task OnWorkflowCompleted_SucceededWithRegisteredHandler_DispatchesHandler()
    {
        var record = await _db.NewInspectionRecord();
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "fencilla");
        workflow.Status = WorkflowStatus.Succeeded;
        workflow.ResultJson = JsonSerializer.Serialize(new { isBreak = false, confidence = 0.5f });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await OnWorkflowCompletedInScope(workflow);

        Assert.Single(_factory.MqttPublisher.AnalysisResultMessages);
    }

    [Fact]
    public async Task OnWorkflowCompleted_HandlerThrows_RunStillMarkedSucceeded()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "test-workflow-1");
        workflow.Status = WorkflowStatus.Succeeded;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var customFactory = _factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s =>
                s.AddSingleton<IWorkflowResultHandler, ThrowingWorkflowResultHandler>()
            )
        );
        using var scope = customFactory.Services.CreateScope();
        var service = ResolveService(scope);

        await service.OnWorkflowCompleted(workflow);

        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisRunStatus.Succeeded, run.Status);
        Assert.NotNull(run.CompletedAt);
    }

    private sealed class ThrowingWorkflowResultHandler : IWorkflowResultHandler
    {
        public string WorkflowType => "test-workflow-1";

        public Task OnWorkflowCompleted(Workflow workflow) =>
            throw new InvalidOperationException("handler failure for testing");
    }

    [Fact]
    public async Task OnWorkflowCompleted_GateMatches_SkipsDownstreamWorkflows()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var gate = await _db.NewWorkflow(run, workflowType: "test-gate", stepNumber: 1);
        gate.Status = WorkflowStatus.Succeeded;
        gate.ResultJson = JsonSerializer.Serialize(new { skip = true });
        var downstream = await _db.NewWorkflow(
            run,
            workflowType: "test-workflow-2",
            stepNumber: 2,
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await OnWorkflowCompletedInScope(gate);

        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(downstream).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisRunStatus.Skipped, run.Status);
        Assert.NotNull(run.SkipReason);
        Assert.Equal(WorkflowStatus.Skipped, downstream.Status);
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
    }

    [Fact]
    public async Task OnWorkflowCompleted_GateDoesNotMatch_ContinuesToNextWorkflow()
    {
        const string nextWorkflowType = "test-workflow-2";
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var gate = await _db.NewWorkflow(run, workflowType: "test-gate", stepNumber: 1);
        gate.Status = WorkflowStatus.Succeeded;
        gate.ResultJson = JsonSerializer.Serialize(new { skip = false });
        await _db.NewWorkflow(
            run,
            workflowType: nextWorkflowType,
            stepNumber: 2,
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await OnWorkflowCompletedInScope(gate);

        var request = Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(
            _factory.WorkflowTemplateNameFor(nextWorkflowType),
            request.WorkflowTemplateName
        );
    }

    [Fact]
    public async Task OnWorkflowCompleted_GateResultMissing_SkipsChainFailClosed()
    {
        const string nextWorkflowType = "test-workflow-2";
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var gate = await _db.NewWorkflow(run, workflowType: "test-gate", stepNumber: 1);
        gate.Status = WorkflowStatus.Succeeded;
        gate.ResultJson = null;
        var downstream = await _db.NewWorkflow(
            run,
            workflowType: nextWorkflowType,
            stepNumber: 2,
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await OnWorkflowCompletedInScope(gate);

        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(downstream).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisRunStatus.Skipped, run.Status);
        Assert.Equal(WorkflowStatus.Skipped, downstream.Status);
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
    }

    [Fact]
    public async Task RetryWorkflow_OnGate_ResetsSkippedRunAndDownstreamSiblings()
    {
        const string gateType = "test-gate";
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var gate = await _db.NewWorkflow(
            run,
            workflowType: gateType,
            stepNumber: 1,
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );
        gate.Status = WorkflowStatus.Succeeded;
        gate.ArgoWorkflowName = $"sara-{gate.Id:N}";
        var previousArgoWorkflowName = gate.ArgoWorkflowName;
        gate.ResultJson = "{\"skip\":true}";
        gate.CompletedAt = DateTime.UtcNow;
        var downstream = await _db.NewWorkflow(
            run,
            workflowType: "test-workflow-2",
            stepNumber: 2,
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );
        downstream.Status = WorkflowStatus.Skipped;
        downstream.CompletedAt = DateTime.UtcNow;
        run.Status = AnalysisRunStatus.Skipped;
        run.SkipReason = "previous skip";
        run.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using (var scope = _factory.Services.CreateScope())
        {
            await ResolveService(scope).RetryWorkflow(gate.Id);
        }

        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(gate).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(downstream).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisRunStatus.InProgress, run.Status);
        Assert.Null(run.SkipReason);
        Assert.Null(run.CompletedAt);
        Assert.Equal(WorkflowStatus.InProgress, gate.Status);
        Assert.Null(gate.ResultJson);
        Assert.Equal(WorkflowStatus.Pending, downstream.Status);
        Assert.Null(downstream.CompletedAt);
        var request = Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(_factory.WorkflowTemplateNameFor(gateType), request.WorkflowTemplateName);
        Assert.StartsWith($"sara-{gate.Id:N}-", gate.ArgoWorkflowName);
        Assert.NotEqual(previousArgoWorkflowName, gate.ArgoWorkflowName);
        Assert.Equal(gate.ArgoWorkflowName, request.WorkflowName);
        Assert.NotNull(gate.ArgoWorkflowUid);
    }
}
