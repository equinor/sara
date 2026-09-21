using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.Services.ResultHandlers.AnalysisResultHandlers;
using api.Services.Results;
using Api.Test.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services.ResultHandlers.AnalysisResultHandlers;

public class AnalysisResultHandlerTests : IAsyncLifetime
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

    private async Task<Analysis> Arrange(
        string analysisType,
        object result,
        AnalysisThreshold threshold,
        IEnumerable<InspectionRecord>? records = null
    )
    {
        records ??= [await _db.NewInspectionRecord(tag: "tag-1", inspectionType: analysisType)];

        var analysis = await _db.NewAnalysis(
            type: analysisType,
            inspectionRecords: records,
            thresholds: [threshold]
        );
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: analysisType);
        workflow.Status = WorkflowStatus.Succeeded;
        workflow.ResultJson = JsonSerializer.Serialize(result);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return analysis;
    }

    private async Task RunHandler(Guid analysisId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SaraDbContext>();

        var analysis = await context
            .Analyses.Include(a => a.InspectionRecords)
            .Include(a => a.Thresholds)
            .Include(a => a.Runs)
                .ThenInclude(r => r.Workflows)
            .FirstAsync(a => a.Id == analysisId, TestContext.Current.CancellationToken);

        var handler = scope
            .ServiceProvider.GetServices<IAnalysisResultHandler>()
            .Single(h => h.AnalysisName == analysis.AnalysisType);

        await handler.OnAnalysisCompleted(analysis, analysis.Runs[0]);
    }

    private Task<List<AnalysisResultValue>> StoredValues() =>
        _context
            .AnalysisResultValues.AsNoTracking()
            .OrderBy(v => v.Tag)
            .ToListAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task CLOEStoresTheOilLevelRatioAsAPercentage()
    {
        var analysis = await Arrange(
            "cloe",
            new { oilLevel = 0.42f, confidence = 0.93f },
            new AnalysisThreshold { Key = ResultKeys.OilLevel, LowerAlert = 20 }
        );

        await RunHandler(analysis.Id);

        var value = Assert.Single(await StoredValues());
        Assert.Equal(42d, value.NumericValue!.Value, 5);
        Assert.Equal(ResultSeverity.Ok, value.Severity);
    }

    [Fact]
    public async Task LowConfidenceIsRecordedAsInconclusive()
    {
        var analysis = await Arrange(
            "thermal-reading",
            new { temperature = 23.5f, confidence = 0.5f },
            new AnalysisThreshold
            {
                Key = ResultKeys.Temperature,
                UpperAlert = 80,
                MinConfidence = 0.99,
            }
        );

        await RunHandler(analysis.Id);

        Assert.Equal(ResultSeverity.Inconclusive, Assert.Single(await StoredValues()).Severity);
    }

    [Fact]
    public async Task GroupedAnalysisRecordsNothing()
    {
        var first = await _db.NewInspectionRecord(inspectionId: "insp-1", tag: "tag-1");
        var second = await _db.NewInspectionRecord(inspectionId: "insp-2", tag: "tag-2");
        var analysis = await Arrange(
            "cloe",
            new { oilLevel = 0.10f, confidence = 0.93f },
            new AnalysisThreshold { Key = ResultKeys.OilLevel, LowerAlert = 20 },
            records: [first, second]
        );

        await RunHandler(analysis.Id);

        Assert.Empty(await StoredValues());
    }

    [Fact]
    public async Task RunningTwiceDoesNotDuplicateValues()
    {
        var analysis = await Arrange(
            "cloe",
            new { oilLevel = 0.42f, confidence = 0.93f },
            new AnalysisThreshold { Key = ResultKeys.OilLevel, LowerAlert = 20 }
        );

        await RunHandler(analysis.Id);
        await RunHandler(analysis.Id);

        Assert.Single(await StoredValues());
    }
}
