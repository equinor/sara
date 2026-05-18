using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using Api.Test.Database;
using api.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Utilities;

public class AnonymizerPayloadEnricherTests : IAsyncLifetime
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

    private AnonymizerPayloadEnricher ResolveEnricher(IServiceScope scope) =>
        scope
            .ServiceProvider.GetRequiredService<IEnumerable<ITriggerPayloadEnricher>>()
            .OfType<AnonymizerPayloadEnricher>()
            .Single();

    [Fact]
    public async Task EnrichAsync_FffInput_ReturnsTiffLocationOnAnonStorage()
    {
        var record = await _db.NewInspectionRecord();
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var input = _db.NewBlobStorageLocation(
            storageAccount: "rawstorage",
            blobContainer: "raw-container",
            blobName: "inspections/2026/img-001.fff"
        );
        var output = _db.NewBlobStorageLocation(
            storageAccount: "anonstorage",
            blobContainer: "anon-container",
            blobName: "analysis-runs/abc/1-anonymizer.jpg"
        );
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "anonymizer",
            inputBlobStorageLocations: [input],
            outputBlobStorageLocation: output
        );

        using var scope = _factory.Services.CreateScope();
        var enricher = ResolveEnricher(scope);

        var result = await enricher.EnrichAsync(workflow, [record]);

        var preProcessed = Assert.IsType<BlobStorageLocation>(
            result["preProcessedBlobStorageLocation"]
        );
        Assert.Equal("anonstorage", preProcessed.StorageAccount);
        Assert.Equal("raw-container", preProcessed.BlobContainer);
        Assert.Equal("inspections/2026/img-001.tiff", preProcessed.BlobName);
    }

    [Theory]
    [InlineData("data.fff", "data.tiff")]
    [InlineData("noextension", "noextension.tiff")]
    [InlineData("dir.with.dot/file.fff", "dir.with.dot/file.tiff")]
    public async Task EnrichAsync_AlwaysSwapsExtensionToTiff(
        string rawBlobName,
        string expectedTiffBlobName
    )
    {
        var record = await _db.NewInspectionRecord();
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var input = _db.NewBlobStorageLocation(blobName: rawBlobName);
        var output = _db.NewBlobStorageLocation(storageAccount: "anonstorage");
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "anonymizer",
            inputBlobStorageLocations: [input],
            outputBlobStorageLocation: output
        );

        using var scope = _factory.Services.CreateScope();
        var enricher = ResolveEnricher(scope);

        var result = await enricher.EnrichAsync(workflow, [record]);

        var preProcessed = Assert.IsType<BlobStorageLocation>(
            result["preProcessedBlobStorageLocation"]
        );
        Assert.Equal(expectedTiffBlobName, preProcessed.BlobName);
    }

    [Fact]
    public async Task EnrichAsync_NoInputBlobStorageLocations_Throws()
    {
        var record = await _db.NewInspectionRecord();
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var output = _db.NewBlobStorageLocation();
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "anonymizer",
            inputBlobStorageLocations: [],
            outputBlobStorageLocation: output
        );

        using var scope = _factory.Services.CreateScope();
        var enricher = ResolveEnricher(scope);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            enricher.EnrichAsync(workflow, [record])
        );
    }

    [Fact]
    public async Task EnrichAsync_NoOutputBlobStorageLocation_Throws()
    {
        var record = await _db.NewInspectionRecord();
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "anonymizer");

        using var scope = _factory.Services.CreateScope();
        var enricher = ResolveEnricher(scope);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            enricher.EnrichAsync(workflow, [record])
        );
    }
}
