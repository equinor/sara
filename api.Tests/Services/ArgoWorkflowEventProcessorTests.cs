using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using Api.Test.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services;

public class ArgoWorkflowEventProcessorTests : IAsyncLifetime
{
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
    }

    [Fact]
    public async Task DuplicateTerminalEvent_IsProcessedOnce()
    {
        var record = await _db.NewInspectionRecord(inspectionId: "inspection-cloe");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        run.Status = AnalysisRunStatus.InProgress;
        var workflow = await _db.NewWorkflow(run, workflowType: "cloe");
        SetArgoIdentity(run, "argo-uid");
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var node = Node(workflow, "Succeeded", "{\"oilLevel\":0.42,\"confidence\":0.9}");

        await Process(run, "Succeeded", node);
        await Process(run, "Succeeded", node);

        Assert.Single(_factory.MqttPublisher.AnalysisResultMessages);
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

    private async Task Process(AnalysisRun run, string phase, ArgoNodeStatus[] nodes, string uid)
    {
        using var scope = _factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IArgoWorkflowEventProcessor>();
        await processor.HandleWorkflowEventAsync(
            new ArgoWorkflowResource
            {
                Metadata = new ArgoObjectMetadata
                {
                    Name = AnalysisWorkflowGraphBuilder.GetArgoWorkflowName(
                        run.Analysis.AnalysisType,
                        run.Id
                    ),
                    Uid = uid,
                    Labels = new Dictionary<string, string>
                    {
                        [ArgoWorkflowClient.AnalysisRunIdLabel] = run.Id.ToString(),
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
