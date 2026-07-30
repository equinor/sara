using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using Api.Test.Database;
using Xunit;

namespace Api.Test.Controllers;

public class DashboardControllerTests : IAsyncLifetime
{
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;
    public required HttpClient Client;

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
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private async Task<Workflow> SeedWorkflow(
        AnalysisRun run,
        string workflowType,
        WorkflowStatus status,
        DateTime? startedAt,
        DateTime? completedAt
    )
    {
        var workflow = await _db.NewWorkflow(run, workflowType: workflowType);
        workflow.Status = status;
        workflow.StartedAt = startedAt;
        workflow.CompletedAt = completedAt;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return workflow;
    }

    private async Task<DashboardSummaryDto> GetSummary(int sinceHours)
    {
        var response = await Client.GetAsync(
            $"/api/dashboard/summary?sinceHours={sinceHours}",
            TestContext.Current.CancellationToken
        );
        Assert.True(response.IsSuccessStatusCode);
        var dto = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(
            JsonOptions,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(dto);
        return dto!;
    }

    [Fact]
    public async Task SummaryCountsTerminalWorkflowsWithinWindowAndExcludesOlderOnes()
    {
        var now = DateTime.UtcNow;
        var record = await _db.NewInspectionRecord(blobName: "test");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);

        // In window (last 24h)
        await SeedWorkflow(run, "fencilla", WorkflowStatus.Succeeded, now.AddHours(-2), now.AddHours(-1));
        await SeedWorkflow(run, "fencilla", WorkflowStatus.Failed, now.AddHours(-3), now.AddHours(-2));
        await SeedWorkflow(run, "cloe", WorkflowStatus.Succeeded, now.AddHours(-5), now.AddHours(-4));
        // Out of window
        await SeedWorkflow(run, "fencilla", WorkflowStatus.Succeeded, now.AddHours(-40), now.AddHours(-39));

        var summary = await GetSummary(24);

        Assert.Equal(2, summary.WorkflowStatusCounts.Succeeded);
        Assert.Equal(1, summary.WorkflowStatusCounts.Failed);
        Assert.Equal(2.0 / 3.0, summary.SuccessRate, 3);

        var fencilla = summary.PerWorkflowType.Find(s => s.WorkflowType == "fencilla");
        Assert.NotNull(fencilla);
        Assert.Equal(2, fencilla!.Total); // one succeeded + one failed in window
        Assert.Equal(1, fencilla.Succeeded);
        Assert.Equal(1, fencilla.Failed);
        Assert.Equal(0.5, fencilla.FailureRate, 3);
    }

    [Fact]
    public async Task SummaryReportsCurrentlyRunningAndStuckWorkflows()
    {
        var now = DateTime.UtcNow;
        var record = await _db.NewInspectionRecord(blobName: "test");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);

        // Fresh in-progress (not stuck)
        await SeedWorkflow(run, "fencilla", WorkflowStatus.InProgress, now.AddMinutes(-5), null);
        // Stuck in-progress (older than 30 min threshold)
        await SeedWorkflow(run, "cloe", WorkflowStatus.InProgress, now.AddMinutes(-90), null);

        var summary = await GetSummary(24);

        Assert.Equal(2, summary.CurrentlyRunning.Workflows);
        Assert.Single(summary.Stuck);
        Assert.Equal("cloe", summary.Stuck[0].WorkflowType);
        Assert.True(summary.Stuck[0].MinutesRunning >= 30);
    }

    [Fact]
    public async Task SummaryRejectsWindowOutsideAllowList()
    {
        var response = await Client.GetAsync(
            "/api/dashboard/summary?sinceHours=5",
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SummaryTrendHasContiguousBuckets()
    {
        var summary = await GetSummary(24);
        // Hourly buckets across a 24h window => at least 24 buckets.
        Assert.True(summary.Trend.Count >= 24);
    }
}
