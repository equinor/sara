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

    private async Task OnInspectionRecordCreatedInScope(InspectionRecord record)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAnalysisTriggerService>();
        await service.OnInspectionRecordCreated(record);
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
            .SingleAsync(a => a.AnalysisType == name, TestContext.Current.CancellationToken);

    [Fact]
    public async Task OnInspectionRecordCreated_NoMatchingAnalysis_DoesNothing()
    {
        var record = await _db.NewInspectionRecord(blobName: "image.dat");

        await OnInspectionRecordCreatedInScope(record);

        Assert.Empty(await _context.Analyses.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
    }

    [Fact]
    public async Task OnInspectionRecordCreated_MixedKnownAndUnknownAnalyses_RunsKnownSkipsUnknown()
    {
        const string knownAnalysis = "per-record-test";
        var newAnalysis = await _db.NewAnalysis(type: knownAnalysis);
        var record = await _db.NewInspectionRecord(analyses: [newAnalysis]);

        await OnInspectionRecordCreatedInScope(record);

        var analysis = await LoadAnalysisByNameAsync(knownAnalysis);
        Assert.Equal(knownAnalysis, analysis.AnalysisType);
        Assert.Single(_factory.ArgoWorkflowClient.Requests);
    }

    [Fact]
    public async Task OnInspectionRecordCreated_AllUnknownAnalyses_CreatesNothing()
    {
        var record = await _db.NewInspectionRecord();

        await OnInspectionRecordCreatedInScope(record);

        Assert.Empty(await _context.Analyses.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
    }

    [Fact]
    public async Task OnInspectionRecordCreated_NonGroupedHappyPath_CreatesAnalysisAndTriggersFirstWorkflow()
    {
        const string analysisName = "per-record-test";
        const string workflowType = "test-workflow-1";
        var newAnalysis = await _db.NewAnalysis(type: analysisName);
        var record = await _db.NewInspectionRecord(analyses: [newAnalysis]);

        await OnInspectionRecordCreatedInScope(record);

        var analysis = await LoadOnlyAnalysisAsync();
        var workflow = analysis.Runs.Single().Workflows.Single();
        Assert.Equal(workflowType, workflow.WorkflowType);

        var request = Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(_factory.WorkflowTemplateNameFor(workflowType), request.WorkflowTemplateName);
    }

    [Fact]
    public async Task OnInspectionRecordCreated_MultiStepChain_CreatesAllWorkflowsButTriggersOnlyFirst()
    {
        const string analysisName = "multi-step-test";
        const string firstWorkflowType = "test-workflow-1";
        const string secondWorkflowType = "test-workflow-2";
        var newAnalysis = await _db.NewAnalysis(type: analysisName);
        var record = await _db.NewInspectionRecord(analyses: [newAnalysis]);

        await OnInspectionRecordCreatedInScope(record);

        var analysis = await LoadOnlyAnalysisAsync();
        var workflows = analysis.Runs.Single().Workflows.OrderBy(w => w.StepNumber).ToList();
        Assert.Equal(
            [firstWorkflowType, secondWorkflowType],
            workflows.Select(w => w.WorkflowType)
        );
        // Step 1 has the inspection record's blob as input; step 2 starts with empty inputs
        // because it will be wired at result time once step 1 reports its output location.
        Assert.Single(workflows[0].InputBlobStorageLocations);
        Assert.Empty(workflows[1].InputBlobStorageLocations);
        // Neither step has an output location pre-persisted.
        Assert.Null(workflows[0].OutputBlobStorageLocation);
        Assert.Null(workflows[1].OutputBlobStorageLocation);

        var request = Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(
            _factory.WorkflowTemplateNameFor(firstWorkflowType),
            request.WorkflowTemplateName
        );
    }

    [Fact]
    public async Task OnInspectionRecordCreated_GroupedAnalysisIncomplete_DefersAndDoesNotTrigger()
    {
        const string analysisName = "group-test";

        var group = await _db.NewAnalysisGroup();
        var newAnalysis = await _db.NewAnalysis(type: analysisName, analysisGroup: group);
        var record = await _db.NewInspectionRecord(analysisGroup: group, analyses: [newAnalysis]);

        await OnInspectionRecordCreatedInScope(record);

        var analysis = await LoadOnlyAnalysisAsync();
        Assert.Empty(analysis.Runs);
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
    }

    [Fact]
    public async Task OnInspectionRecordCreated_GroupedAnalysisCompletes_TriggersDeferredAnalysisWithAllRecords()
    {
        const string analysisName = "group-test";
        var group = await _db.NewAnalysisGroup();
        var newAnalysis = await _db.NewAnalysis(type: analysisName, analysisGroup: group);
        var firstRecord = await _db.NewInspectionRecord(
            inspectionId: "inspection-1",
            analysisGroup: group,
            analyses: [newAnalysis]
        );
        var secondRecord = await _db.NewInspectionRecord(
            inspectionId: "inspection-2",
            analysisGroup: group,
            analyses: [newAnalysis]
        );

        await OnInspectionRecordCreatedInScope(firstRecord);
        await OnInspectionRecordCreatedInScope(secondRecord);

        var analysis = await LoadOnlyAnalysisAsync();
        Assert.Equal(2, analysis.InspectionRecords.Count);
        Assert.Equal(2, analysis.Runs.Single().Workflows.Single().InputBlobStorageLocations.Count);
        Assert.Single(_factory.ArgoWorkflowClient.Requests);
    }

    [Fact]
    public async Task OnInspectionRecordCreated_MixedGroupedAndNonGrouped_TriggersNonGroupedImmediately()
    {
        const string nonGroupedAnalysisType = "per-record-test";
        const string groupedAnalysisType = "group-test";
        const string firstWorkflowType = "test-workflow-1";
        var group = await _db.NewAnalysisGroup();
        var groupedAnalysis = await _db.NewAnalysis(
            type: groupedAnalysisType,
            analysisGroup: group
        );
        var nonGroupedAnalysis = await _db.NewAnalysis(type: nonGroupedAnalysisType);
        var record = await _db.NewInspectionRecord(
            analyses: [nonGroupedAnalysis, groupedAnalysis],
            analysisGroup: group
        );

        await OnInspectionRecordCreatedInScope(record);

        Assert.Empty((await LoadAnalysisByNameAsync(groupedAnalysisType)).Runs);
        Assert.Single((await LoadAnalysisByNameAsync(nonGroupedAnalysisType)).Runs);

        var request = Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(
            _factory.WorkflowTemplateNameFor(firstWorkflowType),
            request.WorkflowTemplateName
        );
    }

    [Fact]
    public async Task OnInspectionRecordCreated_GatedChain_PersistsDistinctBlobsPerWorkflow()
    {
        const string analysisName = "multi-step-gated-test";
        var newAnalysis = await _db.NewAnalysis(type: analysisName);
        var record = await _db.NewInspectionRecord(analyses: [newAnalysis]);

        await OnInspectionRecordCreatedInScope(record);

        var analysis = await _context
            .Analyses.Include(a => a.Runs)
                .ThenInclude(r => r.Workflows)
                    .ThenInclude(w => w.InputBlobStorageLocations)
            .SingleAsync(
                a => a.AnalysisType == analysisName,
                TestContext.Current.CancellationToken
            );

        var run = Assert.Single(analysis.Runs);
        var workflows = run.Workflows.OrderBy(w => w.StepNumber).ToList();
        Assert.Equal(3, workflows.Count);

        // At creation time, no workflow has a pre-persisted output location.
        // Step 1 carries the inspection record's blob as input; later steps start empty
        // and are wired at result time once the previous step reports its output.
        Assert.All(workflows, w => Assert.Null(w.OutputBlobStorageLocation));
        Assert.Single(workflows[0].InputBlobStorageLocations);
        Assert.Empty(workflows[1].InputBlobStorageLocations);
        Assert.Empty(workflows[2].InputBlobStorageLocations);
    }
}
