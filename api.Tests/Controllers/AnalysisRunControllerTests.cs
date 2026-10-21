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
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Controllers;

public class AnalysisRunControllerTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        (_container, string connectionString) =
            await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(connectionString);
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(connectionString);
        _db = new DatabaseUtilities(_context);
        _client = TestSetupHelpers.ConfigureHttpClient(_factory);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task NoAccessForInstallation()
    {
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "Role.Observer");

        var response = await _client.GetAsync(
            "/api/analysis-run",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListsExcludeUnauthorizedAndMixedInstallationsBeforePagination()
    {
        var allowedRecord = await _db.NewInspectionRecord(installationCode: "TST");
        var deniedRecord = await _db.NewInspectionRecord(
            installationCode: "NON",
            inspectionId: "denied"
        );
        var allowedAnalysis = await _db.NewAnalysis(
            type: "cloe",
            inspectionRecords: [allowedRecord]
        );
        var deniedAnalysis = await _db.NewAnalysis(type: "cloe", inspectionRecords: [deniedRecord]);
        var mixedAnalysis = await _db.NewAnalysis(
            type: "cloe",
            inspectionRecords: [allowedRecord, deniedRecord]
        );
        await _db.NewAnalysisRun(mixedAnalysis);
        await _db.NewAnalysisRun(await _db.NewAnalysis());
        var allowedRun = await _db.NewAnalysisRun(allowedAnalysis);
        var deniedRun = await _db.NewAnalysisRun(deniedAnalysis);
        allowedRun.StartedAt = DateTime.UtcNow.AddHours(-1);
        deniedRun.StartedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "Role.Observer.TST");

        var response = await _client.GetAsync(
            "/api/analysis-run?PageSize=1",
            TestContext.Current.CancellationToken
        );
        response.EnsureSuccessStatusCode();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<AnalysisRun>>(
            options,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(page);
        Assert.Equal([allowedRun.Id], page.Items.Select(run => run.Id));
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.TotalPages);

        var recordJson = await _client.GetStringAsync(
            $"/api/inspection-record/id/{allowedRecord.Id}",
            TestContext.Current.CancellationToken
        );
        Assert.DoesNotContain(mixedAnalysis.Id.ToString(), recordJson);
        Assert.DoesNotContain(deniedRecord.Id.ToString(), recordJson);
        var delete = await _client.DeleteAsync(
            $"/api/inspection-record/id/{allowedRecord.Id}",
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Theory]
    [InlineData("Role.Observer.TST", HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData("Role.Observer.NON", HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("Role.Observer.NON,Role.Observer.TST", HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData("Role.Admin", HttpStatusCode.OK, HttpStatusCode.NoContent)]
    public async Task InstallationRolesControlReadsAndWrites(
        string roles,
        HttpStatusCode readStatus,
        HttpStatusCode writeStatus
    )
    {
        var record = await _db.NewInspectionRecord(installationCode: "tst");
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        _client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        var path = $"/api/analysis-run/id/{run.Id}";

        using var read = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(readStatus, read.StatusCode);
        using var delete = await _client.DeleteAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(writeStatus, delete.StatusCode);
        Assert.Equal(
            writeStatus == HttpStatusCode.Forbidden,
            await _context.AnalysisRuns.AnyAsync(
                r => r.Id == run.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetAllFiltersByAnalysisType()
    {
        var cloe = await _db.NewAnalysis(type: "cloe");
        var fencilla = await _db.NewAnalysis(type: "fencilla");
        var cloeRun = await _db.NewAnalysisRun(cloe);
        await _db.NewAnalysisRun(fencilla);

        var response = await _client.GetAsync(
            "/api/analysis-run?AnalysisType=cloe",
            TestContext.Current.CancellationToken
        );
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<AnalysisRun>>(
            options,
            TestContext.Current.CancellationToken
        );

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(page);
        Assert.Equal([cloeRun.Id], page.Items.Select(run => run.Id));
    }

    [Fact]
    public async Task GetAllFiltersStartedAtWithInclusiveBounds()
    {
        var since = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);
        var until = since.AddHours(1);
        var analysis = await _db.NewAnalysis();
        var before = await _db.NewAnalysisRun(analysis, 1);
        before.StartedAt = since.AddMilliseconds(-1);
        var atSince = await _db.NewAnalysisRun(analysis, 2);
        atSince.StartedAt = since;
        var atUntil = await _db.NewAnalysisRun(analysis, 3);
        atUntil.StartedAt = until;
        var after = await _db.NewAnalysisRun(analysis, 4);
        after.StartedAt = until.AddMilliseconds(1);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.GetAsync(
            $"/api/analysis-run?StartedSince={Uri.EscapeDataString(since.ToString("O"))}&StartedUntil={Uri.EscapeDataString(until.ToString("O"))}",
            TestContext.Current.CancellationToken
        );
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<AnalysisRun>>(
            options,
            TestContext.Current.CancellationToken
        );

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(page);
        Assert.Equal([atUntil.Id, atSince.Id], page.Items.Select(run => run.Id));
    }

    [Fact]
    public async Task GetAllRejectsReversedStartedAtRange()
    {
        var response = await _client.GetAsync(
            "/api/analysis-run?StartedSince=2026-09-04T11:00:00Z&StartedUntil=2026-09-04T10:00:00Z",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
