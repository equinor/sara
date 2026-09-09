using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Configurations;
using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using Api.Test.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services;

public class ArgoWorkflowEventProcessorTests : IAsyncLifetime
{
    private const string UploadedLocation =
        """{"storageAccount":"uploadedstorage","blobContainer":"images","blobName":"result.jpg"}""";
    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;

    public async ValueTask InitializeAsync()
    {
        (_container, string connectionString) =
            await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(connectionString);
        _ = _factory.Services;
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(connectionString);
        _db = new DatabaseUtilities(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task UploadedOutput_IsPersistedBeforeResultHandlers()
    {
        var record = await _db.NewInspectionRecord();
        var analysis = await _db.NewAnalysis(type: "fencilla", inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "fencilla");
        SetArgoIdentity(run, "argo-uid");
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var result =
            $$"""{"isBreak":true,"confidence":0.9,"outputBlobStorageLocation":{{UploadedLocation}}}""";

        await Process(run, "Succeeded", Node(workflow, "Succeeded", result));
        await Process(run, "Succeeded", Node(workflow, "Succeeded", "{\"isBreak\":false}"));

        var completed = await _context
            .Workflows.AsNoTracking()
            .Include(w => w.AnalysisRun)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            "uploadedstorage/images/result.jpg",
            completed.OutputBlobStorageLocation?.ToString()
        );
        Assert.Equal(result, completed.ResultJson);
        Assert.NotNull(new WorkflowDto(completed, _factory.BlobStorageService).OutputBlobSAS);
        Assert.Single(_factory.EmailService.FencillaEmails);
        Assert.Single(_factory.MqttPublisher.AnalysisResultMessages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"invalid\"")]
    [InlineData(
        "{\"storageAccount\":\" \",\"blobContainer\":\"images\",\"blobName\":\"result.jpg\"}"
    )]
    [InlineData(
        "{\"storageAccount\":\"account\",\"blobContainer\":null,\"blobName\":\"result.jpg\"}"
    )]
    [InlineData("{\"storageAccount\":\"account\",\"blobContainer\":\"images\",\"blobName\":42}")]
    [InlineData("{\"storageAccount\":\"account\",\"blobContainer\":\"images\",\"blobName\":\"\"}")]
    public async Task MissingOrMalformedOutput_PreservesSuccessfulMetrics(string? output)
    {
        var record = await _db.NewInspectionRecord();
        var analysis = await _db.NewAnalysis(type: "fencilla", inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "fencilla");
        SetArgoIdentity(run, "argo-uid");
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var result = output is null
            ? "{\"isBreak\":false,\"confidence\":0.9}"
            : $$"""{"isBreak":false,"confidence":0.9,"outputBlobStorageLocation":{{output}}}""";

        await Process(run, "Succeeded", Node(workflow, "Succeeded", result));

        var completed = await _context
            .Analyses.AsNoTracking()
            .Include(a => a.Runs)
                .ThenInclude(r => r.Workflows)
            .SingleAsync(TestContext.Current.CancellationToken);
        var step = Assert.Single(Assert.Single(completed.Runs).Workflows);
        Assert.Equal(WorkflowStatus.Succeeded, step.Status);
        Assert.Null(step.OutputBlobStorageLocation);
        Assert.Equal(result, step.ResultJson);
        var dto = new AnalysisDto(
            completed,
            _factory.BlobStorageService,
            _factory.Services.GetRequiredService<IOptions<AnalysisOptions>>().Value
        );
        Assert.Equal("False", dto.Result?.Value);
        Assert.Null(dto.VisualizedSAS);
        Assert.Single(_factory.MqttPublisher.AnalysisResultMessages);
        Assert.Empty(_factory.EmailService.FencillaEmails);
    }

    [Theory]
    [InlineData("Failed", WorkflowStatus.Failed)]
    [InlineData("Error", WorkflowStatus.Failed)]
    [InlineData("Skipped", WorkflowStatus.Skipped)]
    [InlineData("Omitted", WorkflowStatus.Skipped)]
    public async Task UnsuccessfulNode_DoesNotAcquireOutput(string phase, WorkflowStatus status)
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "fencilla");
        SetArgoIdentity(run, "argo-uid");
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Process(
            run,
            "Running",
            Node(workflow, phase, $$"""{"outputBlobStorageLocation":{{UploadedLocation}}}""")
        );

        var completed = await _context
            .Workflows.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(status, completed.Status);
        Assert.Null(completed.OutputBlobStorageLocation);
        Assert.Empty(_factory.MqttPublisher.AnalysisResultMessages);
    }

    [Fact]
    public async Task NodeEvents_ReconcileEveryStepAndCompleteRun()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        run.Status = AnalysisRunStatus.InProgress;
        var first = await _db.NewWorkflow(run, workflowType: "test-workflow-1", stepNumber: 1);
        var second = await _db.NewWorkflow(run, workflowType: "test-workflow-2", stepNumber: 2);
        SetArgoIdentity(run, "argo-uid");
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Process(
            run,
            "Succeeded",
            Node(first, "Succeeded", "{\"step\":1}"),
            Node(second, "Succeeded", "{\"step\":2}")
        );

        await _context.Entry(first).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(second).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowStatus.Succeeded, first.Status);
        Assert.Equal(WorkflowStatus.Succeeded, second.Status);
        Assert.Equal("node-0", first.ArgoNodeId);
        Assert.Equal("node-1", second.ArgoNodeId);
        Assert.Equal(AnalysisRunStatus.Succeeded, run.Status);
        var acknowledged = Assert.Single(_factory.ArgoWorkflowClient.ReconciledWorkflows);
        Assert.Equal(first.ArgoWorkflowName, acknowledged.Name);
        Assert.Equal("argo-uid", acknowledged.Uid);
        Assert.Equal("10", acknowledged.ResourceVersion);
    }

    [Fact]
    public async Task GateOmittedNodes_MarkRunSkipped()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        run.Status = AnalysisRunStatus.InProgress;
        var gate = await _db.NewWorkflow(run, workflowType: "test-gate", stepNumber: 1);
        var downstream = await _db.NewWorkflow(run, workflowType: "test-workflow-2", stepNumber: 2);
        SetArgoIdentity(run, "argo-uid");
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Process(
            run,
            "Succeeded",
            Node(gate, "Succeeded", "{\"skip\":true}"),
            Node(downstream, "Omitted")
        );

        await _context.Entry(downstream).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowStatus.Skipped, downstream.Status);
        Assert.Equal(AnalysisRunStatus.Skipped, run.Status);
        Assert.Contains("test-gate gate matched", run.SkipReason);
        Assert.Single(_factory.ArgoWorkflowClient.ReconciledWorkflows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateTerminalEvent_RetriesAcknowledgmentWithoutRedispatch(bool patchFails)
    {
        var record = await _db.NewInspectionRecord(inspectionId: "inspection-cloe");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        run.Status = AnalysisRunStatus.InProgress;
        var workflow = await _db.NewWorkflow(run, workflowType: "cloe");
        SetArgoIdentity(run, "argo-uid");
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var node = Node(workflow, "Succeeded", "{\"oilLevel\":0.42,\"confidence\":0.9}");

        if (patchFails)
        {
            _factory.ArgoWorkflowClient.ReconcileException = new InvalidOperationException(
                "Patch failed"
            );
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Process(run, "Succeeded", node)
            );
            Assert.Empty(_factory.ArgoWorkflowClient.ReconciledWorkflows);
            await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(AnalysisRunStatus.Succeeded, run.Status);
            _factory.ArgoWorkflowClient.ReconcileException = null;
        }
        else
        {
            await Process(run, "Succeeded", node);
        }
        await Process(run, "Succeeded", node);

        Assert.Single(_factory.MqttPublisher.AnalysisResultMessages);
        Assert.Equal(patchFails ? 1 : 2, _factory.ArgoWorkflowClient.ReconciledWorkflows.Count);
    }

    [Fact]
    public async Task StaleUid_IsIgnored()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        run.Status = AnalysisRunStatus.InProgress;
        var workflow = await _db.NewWorkflow(run, workflowType: "test-workflow-1");
        SetArgoIdentity(run, "current-uid");
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Process(run, "Succeeded", Node(workflow, "Succeeded", "{}"), "stale-uid");

        await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowStatus.Pending, workflow.Status);
        Assert.Empty(_factory.ArgoWorkflowClient.ReconciledWorkflows);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Error")]
    public async Task FailedWorkflow_IsAcknowledgedAfterEveryStepIsReconciled(string phase)
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        run.Status = AnalysisRunStatus.InProgress;
        var first = await _db.NewWorkflow(run, workflowType: "test-workflow-1", stepNumber: 1);
        var second = await _db.NewWorkflow(run, workflowType: "test-workflow-2", stepNumber: 2);
        SetArgoIdentity(run, "argo-uid");
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Process(run, phase, Node(first, phase));

        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AnalysisRunStatus.Failed, run.Status);
        Assert.Empty(_factory.ArgoWorkflowClient.ReconciledWorkflows);

        await Process(run, phase, Node(first, phase), Node(second, "Omitted"));

        await _context.Entry(second).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowStatus.Skipped, second.Status);
        Assert.Single(_factory.ArgoWorkflowClient.ReconciledWorkflows);
    }

    [Theory]
    [InlineData("Running", "Succeeded")]
    [InlineData("Succeeded", "Running")]
    public async Task IncompleteReconciliation_IsNotAcknowledged(string phase, string nodePhase)
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        run.Status = AnalysisRunStatus.InProgress;
        var workflow = await _db.NewWorkflow(run, workflowType: "test-workflow-1");
        SetArgoIdentity(run, "argo-uid");
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Process(run, phase, Node(workflow, nodePhase, "{}"));

        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AnalysisRunStatus.InProgress, run.Status);
        Assert.Empty(_factory.ArgoWorkflowClient.ReconciledWorkflows);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AcknowledgedOrDeletedWorkflow_IsNotPatched(bool reconciled, bool deleted)
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        run.Status = AnalysisRunStatus.InProgress;
        var workflow = await _db.NewWorkflow(run, workflowType: "test-workflow-1");
        SetArgoIdentity(run, "argo-uid");
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Process(
            run,
            "Succeeded",
            [Node(workflow, "Succeeded", "{}")],
            "argo-uid",
            reconciled,
            deleted
        );

        await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            reconciled ? WorkflowStatus.Pending : WorkflowStatus.Succeeded,
            workflow.Status
        );
        Assert.Empty(_factory.ArgoWorkflowClient.ReconciledWorkflows);
    }

    private void SetArgoIdentity(AnalysisRun run, string uid)
    {
        foreach (var workflow in run.Workflows)
        {
            workflow.ArgoWorkflowName = AnalysisWorkflowGraphBuilder.GetArgoWorkflowName(
                run.Analysis.AnalysisType,
                run.Id
            );
            workflow.ArgoWorkflowUid = uid;
        }
    }

    private static ArgoNodeStatus Node(Workflow workflow, string phase, string? result = null) =>
        new()
        {
            DisplayName = AnalysisWorkflowGraphBuilder.GetTaskName(workflow),
            Type = "DAG",
            Phase = phase,
            Outputs = result is null
                ? null
                : new ArgoOutputs
                {
                    Parameters = [new ArgoParameter { Name = "result", Value = result }],
                },
        };

    private async Task Process(AnalysisRun run, string phase, params ArgoNodeStatus[] nodes) =>
        await Process(run, phase, nodes, "argo-uid");

    private async Task Process(AnalysisRun run, string phase, ArgoNodeStatus node, string uid) =>
        await Process(run, phase, [node], uid);

    private async Task Process(
        AnalysisRun run,
        string phase,
        ArgoNodeStatus[] nodes,
        string uid,
        bool reconciled = false,
        bool deleted = false
    )
    {
        using var scope = _factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IArgoWorkflowEventProcessor>();
        await processor.HandleWorkflowEventAsync(
            new ArgoWorkflowResource
            {
                IsDeleted = deleted,
                Metadata = new ArgoObjectMetadata
                {
                    Name = AnalysisWorkflowGraphBuilder.GetArgoWorkflowName(
                        run.Analysis.AnalysisType,
                        run.Id
                    ),
                    Uid = uid,
                    ResourceVersion = "10",
                    Labels = new Dictionary<string, string>
                    {
                        [ArgoWorkflowClient.AnalysisRunIdLabel] = run.Id.ToString(),
                        [ArgoWorkflowClient.ReconciledLabel] = reconciled ? "true" : "false",
                    },
                },
                Status = new ArgoWorkflowStatus
                {
                    Phase = phase,
                    Nodes = nodes
                        .Select((node, index) => (node, index))
                        .ToDictionary(pair => $"node-{pair.index}", pair => pair.node),
                },
            },
            TestContext.Current.CancellationToken
        );
    }
}
