using System;
using System.Collections.Generic;
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

public class AlarmControllerTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;
    private HttpClient _client = null!;
    private AnalysisRun _run = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async ValueTask InitializeAsync()
    {
        (_container, string cs) = await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(cs);
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(cs);
        _db = new DatabaseUtilities(_context);
        _client = TestSetupHelpers.ConfigureHttpClient(_factory);

        var record = await _db.NewInspectionRecord();
        _run = await _db.NewAnalysisRun(await _db.NewAnalysis(inspectionRecords: [record]));
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private async Task<List<AnalysisResultValueDto>> GetActive()
    {
        var response = await _client.GetAsync(
            "/api/alarms/active",
            TestContext.Current.CancellationToken
        );
        response.EnsureSuccessStatusCode();

        return (
            await response.Content.ReadFromJsonAsync<List<AnalysisResultValueDto>>(
                JsonOptions,
                TestContext.Current.CancellationToken
            )
        )!;
    }

    [Fact]
    public async Task GetActiveReturnsAlarmingReadings()
    {
        await _db.NewResultValue(_run, severity: ResultSeverity.Alert);

        Assert.Equal(ResultSeverity.Alert, Assert.Single(await GetActive()).Severity);
    }

    [Fact]
    public async Task AcknowledgeRemovesTheAlarmFromTheActiveList()
    {
        var value = await _db.NewResultValue(_run, severity: ResultSeverity.Alert);

        var response = await _client.PostAsync(
            $"/api/alarms/{value.Id}/acknowledge",
            content: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetActive());
    }
}
