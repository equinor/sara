using api.Database.Context;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Services.Results;

public class ActiveAlarmParameters
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public interface IAlarmService
{
    Task<List<AnalysisResultValue>> GetActiveAlarms(ActiveAlarmParameters parameters);

    Task<bool> Acknowledge(Guid resultValueId);
}

public class AlarmService(SaraDbContext context) : IAlarmService
{
    private static readonly ResultSeverity[] AlarmSeverities =
    [
        ResultSeverity.Warning,
        ResultSeverity.Alert,
    ];

    public Task<List<AnalysisResultValue>> GetActiveAlarms(ActiveAlarmParameters parameters)
    {
        var latest = context.AnalysisResultValues.Where(value =>
            !context.AnalysisResultValues.Any(newer =>
                newer.CorrelationKey == value.CorrelationKey
                && (
                    newer.MeasuredAt > value.MeasuredAt
                    || (newer.MeasuredAt == value.MeasuredAt && newer.CreatedAt > value.CreatedAt)
                )
            )
        );

        var query = latest.Where(value =>
            !value.Acknowledged && AlarmSeverities.Contains(value.Severity)
        );

        // Severity is persisted as text, so ordering on the column would sort
        // alphabetically and rank Warning above Alert. Rank explicitly instead.
        return query
            .OrderBy(value => value.Severity == ResultSeverity.Alert ? 0 : 1)
            .ThenByDescending(value => value.MeasuredAt)
            .ThenBy(value => value.Id)
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<bool> Acknowledge(Guid resultValueId)
    {
        var updated = await context
            .AnalysisResultValues.Where(value => value.Id == resultValueId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Acknowledged, true));

        return updated > 0;
    }
}
