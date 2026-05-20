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

public class ThermalReadingResultHandlerTests : IAsyncLifetime
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

    private ThermalReadingResultHandler ResolveHandler(IServiceScope scope) =>
        scope
            .ServiceProvider.GetRequiredService<IEnumerable<IWorkflowResultHandler>>()
            .OfType<ThermalReadingResultHandler>()
            .Single();

    [Fact]
    public async Task OnWorkflowCompleted_ValidResultForSingleRecord_PublishesAnalysisResultMessage()
    {
        var record = await _db.NewInspectionRecord(
            inspectionId: "insp-123",
            installationCode: "HUA",
            tag: "tag-42",
            inspectionDescription: "thermal-spot"
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var output = _db.NewBlobStorageLocation(
            storageAccount: "outstorage",
            blobContainer: "out-container",
            blobName: "result.json"
        );
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "thermal-reading",
            outputBlobStorageLocation: output
        );
        const float temperature = 42.5f;
        const float confidence = 0.93f;
        workflow.ResultJson = JsonSerializer.Serialize(
            new
            {
                temperature = temperature,
                confidence = confidence,
                warning = (string?)null,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        var published = Assert.Single(_factory.MqttPublisher.AnalysisResultMessages);
        Assert.Equal(temperature.ToString("F2"), published.Value);
        Assert.Equal(confidence * 100, published.Confidence);
    }

    [Fact]
    public async Task OnWorkflowCompleted_NoResolvableInspectionRecord_DoesNotPublish()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "thermal-reading");
        workflow.ResultJson = JsonSerializer.Serialize(new { temperature = 42.5f });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        Assert.Empty(_factory.MqttPublisher.AnalysisResultMessages);
    }

    [Fact]
    public async Task OnWorkflowCompleted_GroupAnalysisWithMultipleRecords_DoesNotPublish()
    {
        var record1 = await _db.NewInspectionRecord(inspectionId: "insp-1");
        var record2 = await _db.NewInspectionRecord(inspectionId: "insp-2");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record1, record2]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "thermal-reading");
        workflow.ResultJson = JsonSerializer.Serialize(new { temperature = 42.5f });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        Assert.Empty(_factory.MqttPublisher.AnalysisResultMessages);
    }

    [Fact]
    public async Task OnWorkflowCompleted_NullResultJson_PublishesMessageWithNullValueAndConfidence()
    {
        var record = await _db.NewInspectionRecord(inspectionId: "insp-123");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "thermal-reading");

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        var published = Assert.Single(_factory.MqttPublisher.AnalysisResultMessages);
        Assert.Null(published.Value);
        Assert.Null(published.Confidence);
    }

    [Fact]
    public async Task OnWorkflowCompleted_ValidResult_UploadsTimeseries()
    {
        var record = await _db.NewInspectionRecord(inspectionId: "insp-123");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "thermal-reading");
        const float temperature = 23.5f;
        workflow.ResultJson = JsonSerializer.Serialize(new { temperature = temperature });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        var upload = Assert.Single(_factory.TimeseriesService.Uploads);
        Assert.Equal(temperature, upload.Value);
    }

    [Fact]
    public async Task OnWorkflowCompleted_NullResultJson_DoesNotUploadTimeseries()
    {
        var record = await _db.NewInspectionRecord(inspectionId: "insp-123");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "thermal-reading");

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        Assert.Empty(_factory.TimeseriesService.Uploads);
    }
}
