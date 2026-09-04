using System;
using System.Linq;
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
    public async Task SummaryCountsAnalysisRunsWithoutInflatingCountsForWorkflowSteps()
    {
        var now = DateTime.UtcNow;
        var cloe = await _db.NewAnalysis(type: "cloe");
        var succeededRun = await _db.NewAnalysisRun(cloe);
        succeededRun.Status = AnalysisRunStatus.Succeeded;
        succeededRun.StartedAt = now.AddHours(-2);
        succeededRun.CompletedAt = now.AddHours(-1);
        await SeedWorkflow(
            succeededRun,
            "anonymizer",
            WorkflowStatus.Succeeded,
            now.AddHours(-2),
            now.AddMinutes(-90)
        );
        await SeedWorkflow(
            succeededRun,
            "cloe",
            WorkflowStatus.Succeeded,
            now.AddMinutes(-90),
            now.AddHours(-1)
        );

        var fencilla = await _db.NewAnalysis(type: "fencilla");
        var failedRun = await _db.NewAnalysisRun(fencilla);
        failedRun.Status = AnalysisRunStatus.Failed;
        failedRun.StartedAt = now.AddHours(-3);
        failedRun.CompletedAt = now.AddHours(-2);
        await SeedWorkflow(
            failedRun,
            "fencilla",
            WorkflowStatus.Failed,
            now.AddHours(-3),
            now.AddHours(-2)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var summary = await GetSummary(24);

        Assert.Equal(2, summary.WorkflowStatusCounts.Succeeded);
        Assert.Equal(1, summary.WorkflowStatusCounts.Failed);
        Assert.Equal(1, summary.RunStatusCounts.Succeeded);
        Assert.Equal(1, summary.RunStatusCounts.Failed);
        Assert.Equal(0.5, summary.SuccessRate, 3);

        Assert.Contains(summary.PerWorkflowType, stat => stat.WorkflowType == "anonymizer");
        Assert.DoesNotContain(summary.PerAnalysisType, stat => stat.AnalysisType == "anonymizer");
        Assert.Contains(
            summary.PerAnalysisType,
            stat => stat.AnalysisType == "cloe" && stat.Succeeded == 1
        );
        Assert.Contains(
            summary.PerAnalysisType,
            stat => stat.AnalysisType == "fencilla" && stat.Failed == 1
        );
        Assert.Equal(2, summary.Trend.Sum(bucket => bucket.Succeeded + bucket.Failed));
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
        Assert.All(
            summary.Trend,
            bucket => Assert.Equal(TimeSpan.FromHours(1), bucket.BucketEnd - bucket.BucketStart)
        );
    }

    [Fact]
    public async Task TrendDetailsGroupsCompletedRunsByAnalysisTypeForRequestedBucket()
    {
        var bucketStart = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);
        var cloe = await _db.NewAnalysis(type: "cloe");
        var succeeded = await _db.NewAnalysisRun(cloe);
        succeeded.Status = AnalysisRunStatus.Succeeded;
        succeeded.CompletedAt = bucketStart.AddMinutes(10);
        var failed = await _db.NewAnalysisRun(cloe, runNumber: 2);
        failed.Status = AnalysisRunStatus.Failed;
        failed.CompletedAt = bucketStart.AddMinutes(20);
        var thermal = await _db.NewAnalysis(type: "thermal-reading");
        var outsideBucket = await _db.NewAnalysisRun(thermal);
        outsideBucket.Status = AnalysisRunStatus.Succeeded;
        outsideBucket.CompletedAt = bucketStart.AddHours(1);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await Client.GetAsync(
            $"/api/dashboard/trend-details?bucketStart={Uri.EscapeDataString(bucketStart.ToString("O"))}&windowHours=24",
            TestContext.Current.CancellationToken
        );
        Assert.True(response.IsSuccessStatusCode);
        var details = await response.Content.ReadFromJsonAsync<TrendBucketDetailsDto>(
            JsonOptions,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(details);
        var cloeDetails = Assert.Single(details.PerAnalysisType);
        Assert.Equal("cloe", cloeDetails.AnalysisType);
        Assert.Equal(1, cloeDetails.Succeeded);
        Assert.Equal(1, cloeDetails.Failed);
    }
}
