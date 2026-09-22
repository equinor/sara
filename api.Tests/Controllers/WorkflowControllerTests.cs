using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using api.Configurations;
using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using api.Services.ResultHandlers;
using api.Services.ResultHandlers.AnalysisResultHandlers;
using api.Services.Results;
using Api.Test.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Test.Controllers;

public class WorkflowControllerTests : IAsyncLifetime
{
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;
    public required HttpClient Client;

    public required IWorkflowService WorkflowService;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public async ValueTask InitializeAsync()
    {
        (var _container, string cs) = await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(cs);
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(cs);
        _db = new DatabaseUtilities(_context);

        Client = TestSetupHelpers.ConfigureHttpClient(_factory);

        WorkflowService = _factory.Services.GetRequiredService<IWorkflowService>();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetAll_WithStartedSince_ReturnsOnlyWorkflowsStartedWithinRange()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var cutoff = DateTime.UtcNow.AddDays(-1);
        var recent = await _db.NewWorkflow(run, workflowType: "recent");
        recent.StartedAt = cutoff.AddHours(1);
        recent.Status = WorkflowStatus.Failed;
        var old = await _db.NewWorkflow(run, workflowType: "old");
        old.StartedAt = cutoff.AddHours(-1);
        old.Status = WorkflowStatus.Failed;
        var notStarted = await _db.NewWorkflow(run, workflowType: "not-started");
        notStarted.Status = WorkflowStatus.Failed;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await Client.GetAsync(
            $"/api/workflow?Status=Failed&StartedSince={Uri.EscapeDataString(cutoff.ToString("O"))}",
            TestContext.Current.CancellationToken
        );

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<WorkflowDto>>(
            JsonOptions,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(page);
        var workflow = Assert.Single(page.Items);
        Assert.Equal(recent.Id, workflow.Id);
    }

    [Fact]
    public async Task WorkflowDtoProjectsTheStoredResultValue()
    {
        var record = await _db.NewInspectionRecord(inspectionType: "cloe", tag: "test-tag");
        var analysis = await _db.NewAnalysis(type: "cloe", inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "cloe");
        await _db.NewResultValue(run, workflow, numericValue: 42, severity: ResultSeverity.Alert);

        var response = await Client.GetAsync(
            $"/api/workflow/id/{workflow.Id}",
            TestContext.Current.CancellationToken
        );
        var dto = await response.Content.ReadFromJsonAsync<WorkflowDto>(
            JsonOptions,
            TestContext.Current.CancellationToken
        );

        Assert.Equal("42.00000", dto!.Result!.Value);
        Assert.Equal(ResultSeverity.Alert, dto.Result.Severity);
    }

    [Fact]
    public async Task CheckThatDTOIsCorrectlyFormattedForEmptyAnalysis()
    {
        // Arrange
        var record = await _db.NewInspectionRecord(
            blobName: "test",
            inspectionType: "thermal-reading",
            tag: "test-tag",
            inspectionDescription: "test-descr"
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "thermal-reading");
        workflow.Status = WorkflowStatus.Succeeded;

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        string Url = $"/api/workflow/id/{workflow.Id}";
        var response = await Client.GetAsync(Url, TestContext.Current.CancellationToken);

        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        jsonOptions.PropertyNameCaseInsensitive = true;
        jsonOptions.IncludeFields = true;

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        var workflowDto = await response.Content.ReadFromJsonAsync<WorkflowDto>(
            jsonOptions,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(workflowDto);
        Assert.Equal(workflowDto.Id, workflow.Id);
        Assert.Null(workflowDto.Result);
    }

    [Fact]
    public async Task ArgoLinkIdentifiers_AreIncludedInWorkflowDto()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run);
        workflow.ArgoWorkflowName = "analysis-workflow";
        workflow.ArgoWorkflowUid = "workflow-uid";
        workflow.ArgoNodeId = "analysis-workflow-123";
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await Client.GetAsync(
            $"/api/workflow/id/{workflow.Id}",
            TestContext.Current.CancellationToken
        );
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        var workflowDto = await response.Content.ReadFromJsonAsync<WorkflowDto>(
            jsonOptions,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(workflowDto);
        Assert.Equal(workflow.ArgoWorkflowName, workflowDto.ArgoWorkflowName);
        Assert.Equal(workflow.ArgoWorkflowUid, workflowDto.ArgoWorkflowUid);
        Assert.Equal(workflow.ArgoNodeId, workflowDto.ArgoNodeId);
    }

    [Fact]
    public async Task DeleteInProgressWorkflow_ReturnsConflict()
    {
        var analysis = await _db.NewAnalysis();
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run);
        workflow.Status = WorkflowStatus.InProgress;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await Client.DeleteAsync(
            $"/api/workflow/id/{workflow.Id}",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(
            await _context.Workflows.AnyAsync(
                w => w.Id == workflow.Id,
                TestContext.Current.CancellationToken
            )
        );
    }
}
