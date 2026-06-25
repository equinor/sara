using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using api.Controllers;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Services;
using Api.Test.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Integration;

/// <summary>
/// End-to-end tests that drive the full SARA pipeline:
/// MQTT ingestion -> analysis trigger -> single Argo Workflow CR submission ->
/// per-step notification controller PUTs -> workflow result handlers ->
/// analysis-run completion. External I/O is recorded via fakes.
/// </summary>
public class EndToEndPipelineTests : IAsyncLifetime
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

    private Task ProcessInspectionResultInScope(IsarInspectionResultMessage message)
    {
        var handler = _factory.Services.GetRequiredService<MqttEventHandler>();
        return handler.ProcessIsarInspectionResult(message);
    }

    private async Task<HttpResponseMessage> NotifyWorkflowStarted(Guid workflowId)
    {
        var client = _factory.CreateClient();
        return await client.PutAsync(
            $"/api/workflow/{workflowId}/started",
            content: null,
            TestContext.Current.CancellationToken
        );
    }

    private async Task<HttpResponseMessage> NotifyWorkflowResult(Guid workflowId, string resultJson)
    {
        var client = _factory.CreateClient();
        var notification = new WorkflowResultNotification { ResultJson = resultJson };
        return await client.PutAsJsonAsync(
            $"/api/workflow/{workflowId}/result",
            notification,
            TestContext.Current.CancellationToken
        );
    }

    private async Task<HttpResponseMessage> NotifyWorkflowExited(
        Guid workflowId,
        WorkflowExitStatus exitStatus
    )
    {
        var client = _factory.CreateClient();
        var notification = new WorkflowExitedNotification { ExitStatus = exitStatus };
        return await client.PutAsJsonAsync(
            $"/api/workflow/{workflowId}/exited",
            notification,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HappyPath_SingleRecordPerRecordAnalysis_RunsThroughToSuccess()
    {
        const string AnalysisName = "per-record-test";
        const string ResultJson = "{\"value\":42}";
        var message = _db.NewIsarInspectionResultMessage(requiredAnalysis: [AnalysisName]);

        await ProcessInspectionResultInScope(message);

        var workflow = await _context
            .Workflows.Include(w => w.AnalysisRun)
                .ThenInclude(r => r.Analysis)
            .SingleAsync(TestContext.Current.CancellationToken);

        (await NotifyWorkflowStarted(workflow.Id)).EnsureSuccessStatusCode();
        (await NotifyWorkflowResult(workflow.Id, ResultJson)).EnsureSuccessStatusCode();
        (
            await NotifyWorkflowExited(workflow.Id, WorkflowExitStatus.Succeeded)
        ).EnsureSuccessStatusCode();

        await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);
        await _context
            .Entry(workflow.AnalysisRun)
            .ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Single(_factory.ArgoSubmitter.SubmittedManifests);
        Assert.NotNull(workflow.StartedAt);
        Assert.Equal(ResultJson, workflow.ResultJson);
        Assert.Equal(WorkflowStatus.Succeeded, workflow.Status);
        Assert.Equal(AnalysisRunStatus.Succeeded, workflow.AnalysisRun.Status);
    }

    [Fact]
    public async Task MultiStepChain_SingleCrSubmittedRunFinalisesAfterLastStep()
    {
        const string AnalysisName = "multi-step-test";
        const string Step1Result = "{\"step\":1}";
        const string Step2Result = "{\"step\":2}";
        var message = _db.NewIsarInspectionResultMessage(requiredAnalysis: [AnalysisName]);

        await ProcessInspectionResultInScope(message);

        // Whole chain is one CR submission; both step rows start as InProgress.
        Assert.Single(_factory.ArgoSubmitter.SubmittedManifests);

        var step1 = await _context
            .Workflows.Include(w => w.AnalysisRun)
            .SingleAsync(w => w.StepNumber == 1, TestContext.Current.CancellationToken);
        var step2 = await _context.Workflows.SingleAsync(
            w => w.StepNumber == 2,
            TestContext.Current.CancellationToken
        );

        (await NotifyWorkflowStarted(step1.Id)).EnsureSuccessStatusCode();
        (await NotifyWorkflowResult(step1.Id, Step1Result)).EnsureSuccessStatusCode();
        (
            await NotifyWorkflowExited(step1.Id, WorkflowExitStatus.Succeeded)
        ).EnsureSuccessStatusCode();

        await _context.Entry(step1.AnalysisRun).ReloadAsync(TestContext.Current.CancellationToken);

        // Run must still be in progress after the intermediate step — Argo
        // (not SARA) drives the chain to step 2.
        Assert.Equal(AnalysisRunStatus.InProgress, step1.AnalysisRun.Status);

        (await NotifyWorkflowStarted(step2.Id)).EnsureSuccessStatusCode();
        (await NotifyWorkflowResult(step2.Id, Step2Result)).EnsureSuccessStatusCode();
        (
            await NotifyWorkflowExited(step2.Id, WorkflowExitStatus.Succeeded)
        ).EnsureSuccessStatusCode();

        await _context.Entry(step1).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(step2).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(step1.AnalysisRun).ReloadAsync(TestContext.Current.CancellationToken);

        // Still only one CR — SARA did not resubmit for step 2.
        Assert.Single(_factory.ArgoSubmitter.SubmittedManifests);
        Assert.Equal(WorkflowStatus.Succeeded, step1.Status);
        Assert.Equal(WorkflowStatus.Succeeded, step2.Status);
        Assert.Equal(AnalysisRunStatus.Succeeded, step1.AnalysisRun.Status);
    }

    [Fact]
    public async Task GroupedAnalysis_SubmitsCrOnceBothRecordsArrive()
    {
        const string GroupId = "group-abc";
        const string Blob1 = "record-1.jpg";
        const string Blob2 = "record-2.jpg";
        var groupMessage = _db.NewAnalysisGroupMessage(
            groupId: GroupId,
            size: 2,
            analyses: ["group-test"]
        );
        var message1 = _db.NewIsarInspectionResultMessage(
            inspectionId: "rec-1",
            blobName: Blob1,
            requiredAnalysis: ["group-test"],
            analysisGroup: groupMessage
        );
        var message2 = _db.NewIsarInspectionResultMessage(
            inspectionId: "rec-2",
            blobName: Blob2,
            requiredAnalysis: ["group-test"],
            analysisGroup: groupMessage
        );

        await ProcessInspectionResultInScope(message1);

        Assert.Empty(_factory.ArgoSubmitter.SubmittedManifests);

        await ProcessInspectionResultInScope(message2);

        var manifest = Assert.Single(_factory.ArgoSubmitter.SubmittedManifests);
        var group = await _context.AnalysisGroups.SingleAsync(
            g => g.GroupId == GroupId,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(AnalysisGroupStatus.Complete, group.Status);
        var manifestJson = manifest.ToJsonString();
        Assert.Contains(Blob1, manifestJson);
        Assert.Contains(Blob2, manifestJson);
    }

    [Fact]
    public async Task GroupedAnalysis_TimeoutMarksGroupTimedOutAndDoesNotSubmit()
    {
        const string GroupId = "group-timeout";
        using var timeoutFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(
                (_, config) =>
                {
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Analysis:AnalysisGroupTimeoutMinutes"] = "-1",
                        }
                    );
                }
            );
        });
        _ = timeoutFactory.Services;

        var groupMessage = _db.NewAnalysisGroupMessage(
            groupId: GroupId,
            size: 2,
            analyses: ["group-test"]
        );
        var message = _db.NewIsarInspectionResultMessage(
            inspectionId: "rec-timeout",
            requiredAnalysis: ["group-test"],
            analysisGroup: groupMessage
        );

        var handler = timeoutFactory.Services.GetRequiredService<MqttEventHandler>();
        await handler.ProcessIsarInspectionResult(message);

        using (var scope = timeoutFactory.Services.CreateScope())
        {
            var processor =
                scope.ServiceProvider.GetRequiredService<IAnalysisGroupTimeoutProcessor>();
            await processor.ProcessTimedOutGroups(TestContext.Current.CancellationToken);
        }

        var group = await _context.AnalysisGroups.SingleAsync(
            g => g.GroupId == GroupId,
            TestContext.Current.CancellationToken
        );
        var analysis = await _context.Analyses.SingleAsync(
            a => a.AnalysisGroupId == group.Id,
            TestContext.Current.CancellationToken
        );
        await _context
            .Entry(analysis)
            .Collection(a => a.Runs)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisGroupStatus.TimedOut, group.Status);
        Assert.Empty(analysis.Runs);
        Assert.Empty(_factory.ArgoSubmitter.SubmittedManifests);
    }
}
