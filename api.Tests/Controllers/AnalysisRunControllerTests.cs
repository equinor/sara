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
