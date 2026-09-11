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

public class FencillaPayloadEnricherTests : IAsyncLifetime
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

    private FencillaPayloadEnricher ResolveEnricher(IServiceScope scope) =>
        scope
            .ServiceProvider.GetRequiredService<IEnumerable<ITriggerPayloadEnricher>>()
            .OfType<FencillaPayloadEnricher>()
            .Single();

    [Fact]
    public async Task EnrichAsync_ValidRecordWithMetadata_ReturnsBothBlobLocations()
    {
        var record = await _db.NewInspectionRecord(
            installationCode: "HUA",
            tag: "tag-test",
            inspectionDescription: "fencilla-test"
        );
        var metadata = await _db.NewReferencePolygonMetadata(
            installationCode: "HUA",
            tagId: "tag-test",
            inspectionDescription: "fencilla-test",
            sourceAnalysisType: AnalysisTypeEnum.Fencilla
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "fencilla");

        using var scope = _factory.Services.CreateScope();
        var enricher = ResolveEnricher(scope);

        var result = await enricher.EnrichAsync(workflow, [record]);

        Assert.Equal(2, result.Count);
        Assert.Equal(
            metadata.ReferenceImageBlobStorageLocation.ToString(),
            result["referenceImageBlobStorageLocation"].ToString()
        );
        Assert.Equal(metadata.Polygon.ToString(), result["referencePolygon"].ToString());
    }

    [Theory]
    [InlineData("InstallationCode")]
    [InlineData("Tag")]
    [InlineData("InspectionDescription")]
    public async Task EnrichAsync_RecordMissingRequiredField_Throws(string missingField)
    {
        var record = await _db.NewInspectionRecord(
            installationCode: missingField == "InstallationCode" ? "" : "HUA",
            tag: missingField == "Tag" ? "" : "tag-test",
            inspectionDescription: missingField == "InspectionDescription" ? "" : "fencilla-test"
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "fencilla");

        using var scope = _factory.Services.CreateScope();
        var enricher = ResolveEnricher(scope);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            enricher.EnrichAsync(workflow, [record])
        );
    }

    [Fact]
    public async Task EnrichAsync_NoInspectionRecords_Throws()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "fencilla");

        using var scope = _factory.Services.CreateScope();
        var enricher = ResolveEnricher(scope);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            enricher.EnrichAsync(workflow, [])
        );
    }

    [Fact]
    public async Task EnrichAsync_NoMatchingMetadata_ReturnsEmptyExtras()
    {
        var record = await _db.NewInspectionRecord(
            installationCode: "HUA",
            tag: "tag-test",
            inspectionDescription: "fencilla-test"
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "fencilla");

        using var scope = _factory.Services.CreateScope();
        var enricher = ResolveEnricher(scope);

        var result = await enricher.EnrichAsync(workflow, [record]);

        Assert.Empty(result);
    }
}
