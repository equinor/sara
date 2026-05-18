using System;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using Api.Test.Database;
using api.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Utilities;

public class InspectionRecordResolverTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;

    public async ValueTask InitializeAsync()
    {
        (_container, string cs) = await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(cs);
        _db = new DatabaseUtilities(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetInspectionRecords_WorkflowLinkedToOneRecord_ReturnsThatRecord()
    {
        var record = await _db.NewInspectionRecord();
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run);

        var result = await InspectionRecordResolver.GetInspectionRecords(_context, workflow);

        Assert.Single(result);
        Assert.Equal(record.Id, result[0].Id);
    }

    [Fact]
    public async Task GetInspectionRecords_WorkflowLinkedToGroupAnalysis_ReturnsAllRecords()
    {
        var recordA = await _db.NewInspectionRecord(inspectionId: "insp-a");
        var recordB = await _db.NewInspectionRecord(inspectionId: "insp-b");
        var recordC = await _db.NewInspectionRecord(inspectionId: "insp-c");
        var analysis = await _db.NewAnalysis(inspectionRecords: [recordA, recordB, recordC]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run);

        var result = await InspectionRecordResolver.GetInspectionRecords(_context, workflow);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, r => r.Id == recordA.Id);
        Assert.Contains(result, r => r.Id == recordB.Id);
        Assert.Contains(result, r => r.Id == recordC.Id);
    }

    [Fact]
    public async Task GetInspectionRecord_WorkflowLinkedToOneRecord_ReturnsThatRecord()
    {
        var record = await _db.NewInspectionRecord();
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run);

        var result = await InspectionRecordResolver.GetInspectionRecord(
            _context,
            workflow,
            NullLogger.Instance
        );

        Assert.NotNull(result);
        Assert.Equal(record.Id, result.Id);
    }
}
