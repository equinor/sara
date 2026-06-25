using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using Api.Test.Database;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services;

/// <summary>
/// Tests for <see cref="WorkflowService"/>'s post-completion lifecycle and
/// retry surface. Per-step Argo CR submission lives on
/// <see cref="IAnalysisTriggerService"/>; this class drives
/// <c>OnWorkflowCompleted</c> and <c>RetryWorkflow</c> directly.
/// </summary>
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

    private async Task OnWorkflowCompletedInScope(Guid workflowId)
    {
        using var scope = _factory.Services.CreateScope();
        await ResolveService(scope).OnWorkflowCompleted(workflowId);
    }

    [Fact]
    public async Task OnWorkflowCompleted_WorkflowNotFound_DoesNothing()
    {
        await OnWorkflowCompletedInScope(Guid.NewGuid());

        Assert.Empty(_factory.ArgoSubmitter.SubmittedManifests);
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

        await OnWorkflowCompletedInScope(workflow.Id);

        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisRunStatus.Failed, run.Status);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public async Task OnWorkflowCompleted_SucceededIntermediateStep_RunStillInProgress()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        run.Status = AnalysisRunStatus.InProgress;
        var first = await _db.NewWorkflow(run, workflowType: "test-workflow-1", stepNumber: 1);
        first.Status = WorkflowStatus.Succeeded;
        await _db.NewWorkflow(
            run,
            workflowType: "test-workflow-2",
            stepNumber: 2,
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await OnWorkflowCompletedInScope(first.Id);

        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);

        // Argo drives chain progression natively; SARA only finalises on the
        // last step. The intermediate step's completion must not flip the run
        // to Succeeded/Failed or trigger any new Argo submission.
        Assert.Equal(AnalysisRunStatus.InProgress, run.Status);
        Assert.Null(run.CompletedAt);
        Assert.Empty(_factory.ArgoSubmitter.SubmittedManifests);
    }

    [Fact]
    public async Task OnWorkflowCompleted_SucceededFinalStep_MarksRunSucceeded()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "test-workflow-1", stepNumber: 1);
        workflow.Status = WorkflowStatus.Succeeded;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await OnWorkflowCompletedInScope(workflow.Id);

        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisRunStatus.Succeeded, run.Status);
        Assert.NotNull(run.CompletedAt);
        Assert.Empty(_factory.ArgoSubmitter.SubmittedManifests);
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

        await OnWorkflowCompletedInScope(workflow.Id);

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

        await service.OnWorkflowCompleted(workflow.Id);

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
    public async Task RetryWorkflow_DeletesPriorCrsAndSubmitsPartialChain()
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
        Assert.Equal(WorkflowStatus.InProgress, downstream.Status);
        Assert.Null(downstream.CompletedAt);

        var expectedSelector = $"sara.equinor.com/analysis-run-id={run.Id}";
        Assert.Contains(expectedSelector, _factory.ArgoSubmitter.DeletedLabelSelectors);
        Assert.Single(_factory.ArgoSubmitter.SubmittedManifests);
    }

    [Fact]
    public async Task RetryWorkflow_OnDownstreamStep_LeavesUpstreamSuccessfulStepsUntouched()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var step1 = await _db.NewWorkflow(
            run,
            workflowType: "test-workflow-1",
            stepNumber: 1,
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );
        step1.Status = WorkflowStatus.Succeeded;
        step1.CompletedAt = DateTime.UtcNow;
        step1.ResultJson = "{\"keep\":true}";
        var step2 = await _db.NewWorkflow(
            run,
            workflowType: "test-workflow-2",
            stepNumber: 2,
            outputBlobStorageLocation: _db.NewBlobStorageLocation()
        );
        step2.Status = WorkflowStatus.Failed;
        step2.ErrorMessage = "boom";
        step2.CompletedAt = DateTime.UtcNow;
        run.Status = AnalysisRunStatus.Failed;
        run.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using (var scope = _factory.Services.CreateScope())
        {
            await ResolveService(scope).RetryWorkflow(step2.Id);
        }

        await _context.Entry(step1).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(step2).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);

        // Upstream successful step is untouched.
        Assert.Equal(WorkflowStatus.Succeeded, step1.Status);
        Assert.Equal("{\"keep\":true}", step1.ResultJson);

        // Retried step is reset and resubmitted.
        Assert.Equal(WorkflowStatus.InProgress, step2.Status);
        Assert.Null(step2.ErrorMessage);
        Assert.Null(step2.CompletedAt);

        Assert.Equal(AnalysisRunStatus.InProgress, run.Status);
        Assert.Single(_factory.ArgoSubmitter.SubmittedManifests);
    }
}
