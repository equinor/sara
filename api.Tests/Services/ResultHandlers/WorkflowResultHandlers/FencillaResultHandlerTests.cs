using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using api.Database.Context;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using Api.Test.Database;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services.ResultHandlers.WorkflowResultHandlers;

public class FencillaResultHandlerTests : IAsyncLifetime
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

    private FencillaResultHandler ResolveHandler(IServiceScope scope) =>
        scope
            .ServiceProvider.GetRequiredService<IEnumerable<IWorkflowResultHandler>>()
            .OfType<FencillaResultHandler>()
            .Single();

    [Fact]
    public async Task OnWorkflowCompleted_BreakDetectedWithOutput_PublishesMessageWithBlobAndSendsEmail()
    {
        const string blobName = "breach.jpg";
        const bool isBreak = true;

        var record = await _db.NewInspectionRecord(
            inspectionId: "insp-123",
            installationCode: "HUA"
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var output = _db.NewBlobStorageLocation(blobName: blobName);
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "fencilla",
            outputBlobStorageLocation: output
        );
        workflow.ResultJson = JsonSerializer.Serialize(
            new
            {
                isBreak = isBreak,
                confidence = 0.88f,
                warning = (string?)null,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        var published = Assert.Single(_factory.MqttPublisher.AnalysisResultMessages);
        Assert.Single(_factory.EmailService.FencillaEmails);
    }

    [Fact]
    public async Task OnWorkflowCompleted_NoBreakDetected_PublishesMessageWithoutBlobAndNoEmail()
    {
        const bool isBreak = false;

        var record = await _db.NewInspectionRecord(
            inspectionId: "insp-123",
            installationCode: "HUA"
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var output = _db.NewBlobStorageLocation(blobName: "should-not-be-attached.jpg");
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "fencilla",
            outputBlobStorageLocation: output
        );
        workflow.ResultJson = JsonSerializer.Serialize(
            new
            {
                isBreak = isBreak,
                confidence = 0.88f,
                warning = (string?)null,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        var published = Assert.Single(_factory.MqttPublisher.AnalysisResultMessages);
        Assert.Empty(_factory.EmailService.FencillaEmails);
    }

    [Fact]
    public async Task OnWorkflowCompleted_NoResolvableInspectionRecord_DoesNotPublish()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var output = _db.NewBlobStorageLocation();
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "fencilla",
            outputBlobStorageLocation: output
        );
        workflow.ResultJson = JsonSerializer.Serialize(new { isBreak = true, confidence = 0.88f });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        Assert.Empty(_factory.MqttPublisher.AnalysisResultMessages);
        Assert.Empty(_factory.EmailService.FencillaEmails);
    }

    [Fact]
    public async Task OnWorkflowCompleted_GroupAnalysisWithMultipleRecords_DoesNotPublish()
    {
        var record1 = await _db.NewInspectionRecord(inspectionId: "insp-1");
        var record2 = await _db.NewInspectionRecord(inspectionId: "insp-2");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record1, record2]);
        var run = await _db.NewAnalysisRun(analysis);
        var output = _db.NewBlobStorageLocation();
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "fencilla",
            outputBlobStorageLocation: output
        );
        workflow.ResultJson = JsonSerializer.Serialize(new { isBreak = true, confidence = 0.88f });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        Assert.Empty(_factory.MqttPublisher.AnalysisResultMessages);
        Assert.Empty(_factory.EmailService.FencillaEmails);
    }

    [Fact]
    public async Task OnWorkflowCompleted_EmailServiceThrows_StillPublishesMessage()
    {
        var record = await _db.NewInspectionRecord(
            inspectionId: "insp-123",
            installationCode: "HUA"
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var output = _db.NewBlobStorageLocation();
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "fencilla",
            outputBlobStorageLocation: output
        );
        workflow.ResultJson = JsonSerializer.Serialize(new { isBreak = true, confidence = 0.88f });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _factory.EmailService.ThrowOnSend = true;

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        Assert.Single(_factory.MqttPublisher.AnalysisResultMessages);
        Assert.Empty(_factory.EmailService.FencillaEmails);
    }
}
