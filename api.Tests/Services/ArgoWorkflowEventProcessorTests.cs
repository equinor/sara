using System;
using System.Collections.Generic;
using System.Linq;
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
        var output = _db.NewBlobStorageLocation();
        var result = JsonSerializer.Serialize(
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

        await Process(workflow, "Succeeded", result);
        await Process(workflow, "Succeeded", result);

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
            .Setup(service => service.FinalizeWorkflowCompleted(It.IsAny<Workflow>()))
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
        Assert.Null(workflow.ResultJson);
    }

    [Fact]
    public async Task SucceededFencillaWithoutDetection_ClearsLegacyOutputLocation()
    {
        var record = await _db.NewInspectionRecord(inspectionId: "insp-fencilla-1");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "fencilla",
            outputBlobStorageLocation: _db.NewBlobStorageLocation(blobName: "speculative.jpg")
        );
        workflow.Status = WorkflowStatus.InProgress;
        workflow.ArgoWorkflowName = "argo-name";
        workflow.ArgoWorkflowUid = "current-uid";
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Process(
            workflow,
            "Succeeded",
            JsonSerializer.Serialize(new { isBreak = false, confidence = 0.95f })
        );

        workflow = await _context
            .Workflows.AsNoTracking()
            .SingleAsync(
                candidate => candidate.Id == workflow.Id,
                TestContext.Current.CancellationToken
            );
        await _context.Entry(run).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Null(workflow.OutputBlobStorageLocation);
        Assert.Equal(WorkflowStatus.Succeeded, workflow.Status);
        Assert.Equal(AnalysisRunStatus.Succeeded, run.Status);
        Assert.Single(_factory.MqttPublisher.AnalysisResultMessages);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"outputBlobStorageLocation\":123}")]
    [InlineData("{\"outputBlobStorageLocation\":{\"storageAccount\":123}}")]
    public async Task SucceededWithUnexpectedResultShape_DoesNotRetryForever(string result)
    {
        var workflow = await NewInProgressWorkflow();

        await Process(workflow, "Succeeded", result);

        await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowStatus.Succeeded, workflow.Status);
        Assert.Null(workflow.OutputBlobStorageLocation);
    }

    [Fact]
    public async Task IntermediateWorkflowWithoutOutput_FailsRunDeterministically()
    {
        var workflow = await NewInProgressWorkflow(withNextWorkflow: true);

        await Process(workflow, "Succeeded", "{}");

        var workflows = await _context
            .Workflows.AsNoTracking()
            .Where(candidate => candidate.AnalysisRunId == workflow.AnalysisRunId)
            .OrderBy(candidate => candidate.StepNumber)
            .ToListAsync(TestContext.Current.CancellationToken);
        var run = await _context
            .AnalysisRuns.AsNoTracking()
            .SingleAsync(
                candidate => candidate.Id == workflow.AnalysisRunId,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(WorkflowStatus.Succeeded, workflows[0].Status);
        Assert.Equal(WorkflowStatus.Failed, workflows[1].Status);
        Assert.Contains("succeeded without an output", workflows[1].ErrorMessage);
        Assert.Equal(AnalysisRunStatus.Failed, run.Status);
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
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

    [Fact]
    public async Task SucceededAnonymizer_PersistsOutputPublishesAndTriggersNextWorkflow()
    {
        var record = await _db.NewInspectionRecord(inspectionId: "insp-anonymizer-1");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "anonymizer", stepNumber: 1);
        workflow.Status = WorkflowStatus.InProgress;
        workflow.ArgoWorkflowName = "argo-name";
        workflow.ArgoWorkflowUid = "current-uid";
        await _db.NewWorkflow(run, workflowType: "test-workflow-2", stepNumber: 2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var output = _db.NewBlobStorageLocation(blobName: "anonymized.jpg");
        await Process(
            workflow,
            "Succeeded",
            JsonSerializer.Serialize(
                new
                {
                    outputBlobStorageLocation = new
                    {
                        storageAccount = output.StorageAccount,
                        blobContainer = output.BlobContainer,
                        blobName = output.BlobName,
                    },
                }
            )
        );

        var completed = await _context
            .Workflows.AsNoTracking()
            .SingleAsync(w => w.Id == workflow.Id, TestContext.Current.CancellationToken);
        Assert.Equal(output.StorageAccount, completed.OutputBlobStorageLocation?.StorageAccount);
        Assert.Equal(output.BlobContainer, completed.OutputBlobStorageLocation?.BlobContainer);
        Assert.Equal(output.BlobName, completed.OutputBlobStorageLocation?.BlobName);
        Assert.Single(_factory.MqttPublisher.VisualizationMessages);
        Assert.Single(_factory.ArgoWorkflowClient.Requests);
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
