using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Database.Context;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using Api.Test.Database;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services.ResultHandlers.WorkflowResultHandlers;

public class CopyRawToVisualizedResultHandlerTests : IAsyncLifetime
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

    private CopyRawToVisualizedResultHandler ResolveHandler(IServiceScope scope) =>
        scope
            .ServiceProvider.GetRequiredService<IEnumerable<IWorkflowResultHandler>>()
            .OfType<CopyRawToVisualizedResultHandler>()
            .Single();

    [Fact]
    public async Task OnWorkflowCompleted_ValidSingleRecordWithOutput_PublishesVisualizationAvailable()
    {
        const string inspectionId = "insp-123";
        const string blobName = "raw.mp4";
        var record = await _db.NewInspectionRecord(inspectionId: inspectionId);
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var output = _db.NewBlobStorageLocation(blobName: blobName);
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "copy-raw-to-visualized",
            outputBlobStorageLocation: output
        );

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        var published = Assert.Single(_factory.MqttPublisher.VisualizationMessages);
        Assert.Equal(inspectionId, published.InspectionId);
        Assert.Equal(blobName, published.BlobName);
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
            workflowType: "copy-raw-to-visualized",
            outputBlobStorageLocation: output
        );

        using var scope = _factory.Services.CreateScope();
        var handler = ResolveHandler(scope);

        await handler.OnWorkflowCompleted(workflow);

        Assert.Empty(_factory.MqttPublisher.VisualizationMessages);
    }
}
