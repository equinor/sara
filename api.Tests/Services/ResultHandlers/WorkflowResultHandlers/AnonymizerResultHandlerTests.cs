using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using Api.Test.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services.ResultHandlers.WorkflowResultHandlers;

public class AnonymizerResultHandlerTests : IAsyncLifetime
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

    private AnonymizerResultHandler ResolveHandler(IServiceScope scope) =>
        scope
            .ServiceProvider.GetRequiredService<IEnumerable<IWorkflowResultHandler>>()
            .OfType<AnonymizerResultHandler>()
            .Single();

    [Fact]
    public async Task OnWorkflowCompleted_ValidSingleRecordWithOutput_PublishesVisualizationAvailable()
    {
        const string inspectionId = "insp-123";
        const string storageAccount = "outstorage";
        const string blobContainer = "out-container";
        const string blobName = "anonymized.jpg";

        var record = await _db.NewInspectionRecord(inspectionId: inspectionId);
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var output = _db.NewBlobStorageLocation(
            storageAccount: storageAccount,
            blobContainer: blobContainer,
            blobName: blobName
        );
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "anonymizer",
            outputBlobStorageLocation: output
        );

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        var published = Assert.Single(_factory.MqttPublisher.VisualizationMessages);
        Assert.Equal(inspectionId, published.InspectionId);
        Assert.Equal(workflow.Id, published.WorkflowId);
        Assert.Equal(run.Id, published.AnalysisRunId);
        Assert.Equal(analysis.Id, published.AnalysisId);
    }

    [Fact]
    public async Task OnWorkflowCompleted_NoResolvableInspectionRecord_DoesNotPublish()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var output = _db.NewBlobStorageLocation();
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "anonymizer",
            outputBlobStorageLocation: output
        );

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        Assert.Empty(_factory.MqttPublisher.VisualizationMessages);
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
            workflowType: "anonymizer",
            outputBlobStorageLocation: output
        );

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        Assert.Empty(_factory.MqttPublisher.VisualizationMessages);
    }

    [Fact]
    public async Task OnWorkflowCompleted_NoOutputBlobStorageLocation_DoesNotPublish()
    {
        var record = await _db.NewInspectionRecord(inspectionId: "insp-123");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "anonymizer");

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        Assert.Empty(_factory.MqttPublisher.VisualizationMessages);
    }

    [Fact]
    public async Task OnWorkflowCompleted_ResultWithPreProcessed_NextIsThermalReading_RewiresInputs()
    {
        var record = await _db.NewInspectionRecord(inspectionId: "insp-1");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var output = _db.NewBlobStorageLocation(blobName: "anonymized.jpg");
        var anonymizer = await _db.NewWorkflow(
            run,
            workflowType: "anonymizer",
            stepNumber: 1,
            outputBlobStorageLocation: output
        );
        var rawThermalInput = _db.NewBlobStorageLocation(blobName: "raw.jpg");
        var thermalReading = await _db.NewWorkflow(
            run,
            workflowType: "thermal-reading",
            stepNumber: 2,
            inputBlobStorageLocations: [rawThermalInput]
        );
        anonymizer.ResultJson =
            "{\"isPersonInImage\":false,"
            + "\"outputBlobStorageLocation\":{\"storageAccount\":\"anonstorage\",\"blobContainer\":\"anon-container\",\"blobName\":\"anonymized.jpg\"},"
            + "\"preProcessedBlobStorageLocation\":{\"storageAccount\":\"anonstorage\",\"blobContainer\":\"raw-container\",\"blobName\":\"raw.tiff\"}}";
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(anonymizer);

        var reloaded = await _context
            .Workflows.AsNoTracking()
            .FirstAsync(w => w.Id == thermalReading.Id, TestContext.Current.CancellationToken);
        var input = Assert.Single(reloaded.InputBlobStorageLocations);
        Assert.Equal("anonstorage", input.StorageAccount);
        Assert.Equal("raw-container", input.BlobContainer);
        Assert.Equal("raw.tiff", input.BlobName);
    }
}
