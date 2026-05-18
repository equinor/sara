using System;
using System.Linq;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Services;
using Api.Test.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services;

public class AnalysisTriggerServiceTests : IAsyncLifetime
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

    private async Task OnInspectionRecordCreatedInScope(InspectionRecordCreatedEvent createdEvent)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAnalysisTriggerService>();
        await service.OnInspectionRecordCreated(createdEvent);
    }

    private Task<Analysis> LoadOnlyAnalysisAsync() =>
        _context
            .Analyses.Include(a => a.InspectionRecords)
            .Include(a => a.Runs)
                .ThenInclude(r => r.Workflows)
            .SingleAsync(TestContext.Current.CancellationToken);

    private Task<Analysis> LoadAnalysisByNameAsync(string name) =>
        _context
            .Analyses.Include(a => a.InspectionRecords)
            .Include(a => a.Runs)
                .ThenInclude(r => r.Workflows)
            .SingleAsync(a => a.Name == name, TestContext.Current.CancellationToken);

    [Fact]
    public async Task OnInspectionRecordCreated_NoMatchingAnalysis_DoesNothing()
    {
        var record = await _db.NewInspectionRecord(blobName: "image.dat");

        await OnInspectionRecordCreatedInScope(_db.NewInspectionRecordCreatedEvent(record));

        Assert.Empty(await _context.Analyses.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_factory.ArgoHttpHandler.Requests);
    }

    [Fact]
    public async Task OnInspectionRecordCreated_RequiredAnalysisTakesPrecedenceOverFileExtension()
    {
        const string requiredAnalysis = "multi-step-test";
        var record = await _db.NewInspectionRecord(blobName: "image.jpg");

        await OnInspectionRecordCreatedInScope(
            _db.NewInspectionRecordCreatedEvent(record, requiredAnalysis: [requiredAnalysis])
        );

        var analysis = await LoadAnalysisByNameAsync(requiredAnalysis);
        Assert.Equal(requiredAnalysis, analysis.Name);
    }

    [Fact]
    public async Task OnInspectionRecordCreated_MixedKnownAndUnknownAnalyses_RunsKnownSkipsUnknown()
    {
        const string knownAnalysis = "per-record-test";
        var record = await _db.NewInspectionRecord();

        await OnInspectionRecordCreatedInScope(
            _db.NewInspectionRecordCreatedEvent(
                record,
                requiredAnalysis: [knownAnalysis, "nonexistent-analysis"]
            )
        );

        var analysis = await LoadAnalysisByNameAsync(knownAnalysis);
        Assert.Equal(knownAnalysis, analysis.Name);
        Assert.Single(_factory.ArgoHttpHandler.Requests);
    }

    [Fact]
    public async Task OnInspectionRecordCreated_AllUnknownAnalyses_CreatesNothing()
    {
        var record = await _db.NewInspectionRecord();

        await OnInspectionRecordCreatedInScope(
            _db.NewInspectionRecordCreatedEvent(
                record,
                requiredAnalysis: ["nonexistent-analysis", "also-not-real"]
            )
        );

        Assert.Empty(await _context.Analyses.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_factory.ArgoHttpHandler.Requests);
    }

    [Fact]
    public async Task OnInspectionRecordCreated_NonGroupedHappyPath_CreatesAnalysisAndTriggersFirstWorkflow()
    {
        const string analysisName = "per-record-test";
        const string workflowType = "test-workflow-1";
        var record = await _db.NewInspectionRecord();

        await OnInspectionRecordCreatedInScope(
            _db.NewInspectionRecordCreatedEvent(record, requiredAnalysis: [analysisName])
        );

        var analysis = await LoadOnlyAnalysisAsync();
        var workflow = analysis.Runs.Single().Workflows.Single();
        Assert.Equal(workflowType, workflow.WorkflowType);

        var request = Assert.Single(_factory.ArgoHttpHandler.Requests);
        Assert.Equal(_factory.TriggerUrlFor(workflowType), request.RequestUri?.ToString());
    }

    [Fact]
    public async Task OnInspectionRecordCreated_MultiStepChain_CreatesAllWorkflowsButTriggersOnlyFirst()
    {
        const string analysisName = "multi-step-test";
        const string firstWorkflowType = "test-workflow-1";
        const string secondWorkflowType = "test-workflow-2";
        var record = await _db.NewInspectionRecord();

        await OnInspectionRecordCreatedInScope(
            _db.NewInspectionRecordCreatedEvent(record, requiredAnalysis: [analysisName])
        );

        var analysis = await LoadOnlyAnalysisAsync();
        var workflows = analysis.Runs.Single().Workflows.OrderBy(w => w.StepNumber).ToList();
        Assert.Equal(
            [firstWorkflowType, secondWorkflowType],
            workflows.Select(w => w.WorkflowType)
        );
        Assert.Equal(
            workflows[0].OutputBlobStorageLocation?.ToString(),
            workflows[1].InputBlobStorageLocations[0].ToString()
        );

        var request = Assert.Single(_factory.ArgoHttpHandler.Requests);
        Assert.Equal(_factory.TriggerUrlFor(firstWorkflowType), request.RequestUri?.ToString());
    }

    [Fact]
    public async Task OnInspectionRecordCreated_GroupedAnalysisIncomplete_DefersAndDoesNotTrigger()
    {
        const string analysisName = "group-test";
        var record = await _db.NewInspectionRecord();

        await OnInspectionRecordCreatedInScope(
            _db.NewInspectionRecordCreatedEvent(
                record,
                requiredAnalysis: [analysisName],
                analysisGroup: _db.NewAnalysisGroupMessage(analyses: [analysisName])
            )
        );

        var analysis = await LoadOnlyAnalysisAsync();
        Assert.Empty(analysis.Runs);
        Assert.Empty(_factory.ArgoHttpHandler.Requests);
    }

    [Fact]
    public async Task OnInspectionRecordCreated_GroupedAnalysisCompletes_TriggersDeferredAnalysisWithAllRecords()
    {
        const string analysisName = "group-test";
        var groupMessage = _db.NewAnalysisGroupMessage(analyses: [analysisName]);
        var firstRecord = await _db.NewInspectionRecord(inspectionId: "inspection-1");
        var secondRecord = await _db.NewInspectionRecord(inspectionId: "inspection-2");

        await OnInspectionRecordCreatedInScope(
            _db.NewInspectionRecordCreatedEvent(
                firstRecord,
                requiredAnalysis: [analysisName],
                analysisGroup: groupMessage
            )
        );
        await OnInspectionRecordCreatedInScope(
            _db.NewInspectionRecordCreatedEvent(
                secondRecord,
                requiredAnalysis: [analysisName],
                analysisGroup: groupMessage
            )
        );

        var analysis = await LoadOnlyAnalysisAsync();
        Assert.Equal(2, analysis.InspectionRecords.Count);
        Assert.Equal(2, analysis.Runs.Single().Workflows.Single().InputBlobStorageLocations.Count);
        Assert.Single(_factory.ArgoHttpHandler.Requests);
    }

    [Fact]
    public async Task OnInspectionRecordCreated_MixedGroupedAndNonGrouped_TriggersNonGroupedImmediately()
    {
        const string nonGroupedAnalysis = "per-record-test";
        const string groupedAnalysis = "group-test";
        const string firstWorkflowType = "test-workflow-1";
        var record = await _db.NewInspectionRecord();

        await OnInspectionRecordCreatedInScope(
            _db.NewInspectionRecordCreatedEvent(
                record,
                requiredAnalysis: [nonGroupedAnalysis, groupedAnalysis],
                analysisGroup: _db.NewAnalysisGroupMessage(analyses: [groupedAnalysis])
            )
        );

        Assert.Empty((await LoadAnalysisByNameAsync(groupedAnalysis)).Runs);
        Assert.Single((await LoadAnalysisByNameAsync(nonGroupedAnalysis)).Runs);

        var request = Assert.Single(_factory.ArgoHttpHandler.Requests);
        Assert.Equal(_factory.TriggerUrlFor(firstWorkflowType), request.RequestUri?.ToString());
    }
}
