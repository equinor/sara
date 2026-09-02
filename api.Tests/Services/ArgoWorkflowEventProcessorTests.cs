using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using Api.Test.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
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
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-json")]
    public async Task SucceededWithoutValidResult_FailsWorkflow(string? result)
    {
        var workflow = await NewInProgressWorkflow();

        await Process(workflow, "Succeeded", result);

        await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowStatus.Failed, workflow.Status);
        Assert.NotNull(workflow.ErrorMessage);
    }

    [Fact]
    public async Task StaleUid_IsIgnored()
    {
        var workflow = await NewInProgressWorkflow();

        await Process(workflow, "Succeeded", "{}", uid: "stale");

        await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowStatus.InProgress, workflow.Status);
    }

    [Fact]
    public async Task MissingPersistedUid_IsRecoveredForMatchingResourceName()
    {
        var workflow = await NewInProgressWorkflow();
        workflow.ArgoWorkflowUid = null;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Process(workflow, "Succeeded", "{}", uid: "recovered-uid");

        await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowStatus.Succeeded, workflow.Status);
        Assert.Equal("recovered-uid", workflow.ArgoWorkflowUid);
    }

    [Fact]
    public async Task DuplicateTerminalEvent_DoesNotTriggerNextWorkflowTwice()
    {
        var workflow = await NewInProgressWorkflow(withNextWorkflow: true);

        await Process(workflow, "Succeeded", "{}");
        await Process(workflow, "Succeeded", "{}");

        Assert.Single(_factory.ArgoWorkflowClient.Requests);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Error")]
    public async Task FailedPhase_StoresStatusMessage(string phase)
    {
        var workflow = await NewInProgressWorkflow();

        await Process(workflow, phase, null, message: "Argo failed");

        await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowStatus.Failed, workflow.Status);
        Assert.Equal("Argo failed", workflow.ErrorMessage);
    }

    [Fact]
    public async Task CompletionFailure_RollsBackClaimSoRelistCanRetry()
    {
        var workflow = await NewInProgressWorkflow();
        var workflowService = new Mock<IWorkflowService>();
        workflowService
            .Setup(service => service.OnWorkflowCompleted(It.IsAny<Workflow>()))
            .ThrowsAsync(new InvalidOperationException("completion failed"));
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IWorkflowService>();
                services.AddScoped(_ => workflowService.Object);
            })
        );
        _ = factory.Services;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Process(workflow, "Succeeded", "{}", factory: factory)
        );

        await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowStatus.InProgress, workflow.Status);
        Assert.Null(workflow.CompletedAt);
    }

    private async Task<Workflow> NewInProgressWorkflow(bool withNextWorkflow = false)
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "test-workflow-1");
        workflow.Status = WorkflowStatus.InProgress;
        workflow.ArgoWorkflowName = "argo-name";
        workflow.ArgoWorkflowUid = "current-uid";
        if (withNextWorkflow)
        {
            await _db.NewWorkflow(
                run,
                workflowType: "test-workflow-2",
                stepNumber: 2,
                outputBlobStorageLocation: _db.NewBlobStorageLocation()
            );
        }
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return workflow;
    }

    [Fact]
    public async Task SucceededWorkflow_PublishesAnalysisResult()
    {
        // The processor re-reads the workflow with AsNoTracking, so result
        // handlers see whatever that query loaded -- not the change-tracked
        // graph the test built. Handlers that read workflow.AnalysisRun break
        // if the navigation is not included, and the failure is swallowed and
        // logged, so the workflow still looks complete while the MQTT message
        // and the timeseries upload are silently lost.
        var record = await _db.NewInspectionRecord(inspectionId: "insp-cloe-1");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "cloe",
            outputBlobStorageLocation: _db.NewBlobStorageLocation(blobName: "result.json")
        );
        workflow.Status = WorkflowStatus.InProgress;
        workflow.ArgoWorkflowName = "argo-name";
        workflow.ArgoWorkflowUid = "current-uid";
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Process(
            workflow,
            "Succeeded",
            JsonSerializer.Serialize(
                new
                {
                    oilLevel = 0.42f,
                    confidence = 0.93f,
                    warning = (string?)null,
                }
            )
        );

        Assert.Single(_factory.MqttPublisher.AnalysisResultMessages);
        Assert.Single(_factory.TimeseriesService.Uploads);
    }

    private async Task Process(
        Workflow workflow,
        string phase,
        string? result,
        string? uid = null,
        string? message = null,
        WebApplicationFactory<Program>? factory = null
    )
    {
        using var scope = (factory ?? _factory).Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IArgoWorkflowEventProcessor>();
        await processor.Process(
            new ArgoWorkflowResource
            {
                Metadata = new ArgoObjectMetadata
                {
                    Name = workflow.ArgoWorkflowName,
                    Uid = uid ?? workflow.ArgoWorkflowUid,
                    Labels = new Dictionary<string, string>
                    {
                        [ArgoWorkflowClient.WorkflowIdLabel] = workflow.Id.ToString(),
                    },
                },
                Status = new ArgoWorkflowStatus
                {
                    Phase = phase,
                    Message = message,
                    Outputs = result is null
                        ? null
                        : new ArgoOutputs
                        {
                            Parameters = [new ArgoParameter { Name = "result", Value = result }],
                        },
                },
            },
            TestContext.Current.CancellationToken
        );
    }
}
