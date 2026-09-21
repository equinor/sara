using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.Services.Results;
using Api.Test.Database;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services.Results;

public class AlarmServiceTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;
    private AnalysisRun _run = null!;

    private static readonly DateTime Noon = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public async ValueTask InitializeAsync()
    {
        (_container, string cs) = await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(cs);
        _ = _factory.Services;
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(cs);
        _db = new DatabaseUtilities(_context);

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

    private async Task<List<AnalysisResultValue>> ActiveAlarms()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope
            .ServiceProvider.GetRequiredService<IAlarmService>()
            .GetActiveAlarms(new ActiveAlarmParameters());
    }

    [Fact]
    public async Task AlarmingLatestReadingIsReturned()
    {
        await _db.NewResultValue(_run, severity: ResultSeverity.Alert, measuredAt: Noon);

        Assert.Equal(ResultSeverity.Alert, Assert.Single(await ActiveAlarms()).Severity);
    }

    [Fact]
    public async Task NewerHealthyReadingAutoClearsTheAlarm()
    {
        await _db.NewResultValue(_run, severity: ResultSeverity.Alert, measuredAt: Noon);
        await _db.NewResultValue(_run, severity: ResultSeverity.Ok, measuredAt: Noon.AddHours(1));

        Assert.Empty(await ActiveAlarms());
    }

    [Fact]
    public async Task LateArrivingOlderHealthyReadingDoesNotClearNewerAlarm()
    {
        await _db.NewResultValue(
            _run,
            severity: ResultSeverity.Alert,
            measuredAt: Noon.AddHours(1)
        );
        await _db.NewResultValue(_run, severity: ResultSeverity.Ok, measuredAt: Noon);

        Assert.Single(await ActiveAlarms());
    }

    [Fact]
    public async Task AcknowledgedReadingIsNoLongerActive()
    {
        var value = await _db.NewResultValue(_run, severity: ResultSeverity.Alert);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAlarmService>().Acknowledge(value.Id);
        }

        Assert.Empty(await ActiveAlarms());
    }

    [Fact]
    public async Task NewBadReadingAfterAcknowledgementReRaises()
    {
        await _db.NewResultValue(
            _run,
            severity: ResultSeverity.Alert,
            measuredAt: Noon,
            acknowledged: true
        );
        var fresh = await _db.NewResultValue(
            _run,
            severity: ResultSeverity.Alert,
            measuredAt: Noon.AddHours(1)
        );

        Assert.Equal(fresh.Id, Assert.Single(await ActiveAlarms()).Id);
    }

    [Fact]
    public async Task AlertsAreRankedAboveWarnings()
    {
        await _db.NewResultValue(
            _run,
            tag: "warn-tag",
            severity: ResultSeverity.Warning,
            measuredAt: Noon.AddHours(2)
        );
        await _db.NewResultValue(
            _run,
            tag: "alert-tag",
            severity: ResultSeverity.Alert,
            measuredAt: Noon
        );

        var alarms = await ActiveAlarms();

        Assert.Equal(ResultSeverity.Alert, alarms[0].Severity);
        Assert.Equal(ResultSeverity.Warning, alarms[1].Severity);
    }
}
