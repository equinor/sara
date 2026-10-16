using api.Configurations;
using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummary(int windowHours, TimeZoneInfo timeZone);
    Task<TrendBucketDetailsDto> GetTrendBucketDetails(
        DateTime bucketStart,
        int windowHours,
        TimeZoneInfo timeZone
    );
}

public class DashboardService(
    SaraDbContext context,
    IOptions<DashboardOptions> options,
    InstallationAccess access
) : IDashboardService
{
    private readonly DashboardOptions _options = options.Value;

    private record TerminalWorkflow(
        string WorkflowType,
        WorkflowStatus Status,
        DateTime? StartedAt,
        DateTime CompletedAt
    );

    private record TerminalAnalysisRun(
        string AnalysisType,
        AnalysisRunStatus Status,
        DateTime? StartedAt,
        DateTime CompletedAt
    );

    public async Task<DashboardSummaryDto> GetSummary(int windowHours, TimeZoneInfo timeZone)
    {
        var now = DateTime.UtcNow;
        var bucketBoundaries = BuildBucketBoundaries(windowHours, timeZone, now);
        var since = bucketBoundaries[0];
        var until = bucketBoundaries[^1];
        var workflows = access.Filter(context.Workflows, AccessMode.Read);
        var analysisRuns = access.Filter(context.AnalysisRuns, AccessMode.Read);
        var analysisGroups = access.Filter(context.AnalysisGroups, AccessMode.Read);

        // Single projection of all workflows that reached a terminal state within
        // the window. Everything time-bucketed (status counts, per-type stats,
        // trend) is derived from this in memory.
        var terminalWorkflows = await workflows
            .Where(w =>
                w.CompletedAt != null
                && w.CompletedAt >= since
                && w.CompletedAt < until
                && (
                    w.Status == WorkflowStatus.Succeeded
                    || w.Status == WorkflowStatus.Failed
                    || w.Status == WorkflowStatus.Skipped
                )
            )
            .Select(w => new TerminalWorkflow(
                w.WorkflowType,
                w.Status,
                w.StartedAt,
                w.CompletedAt!.Value
            ))
            .ToListAsync();

        var terminalAnalysisRuns = await analysisRuns
            .Where(run =>
                run.CompletedAt != null
                && run.StartedAt != null
                && run.StartedAt >= since
                && run.StartedAt < until
                && (
                    run.Status == AnalysisRunStatus.Succeeded
                    || run.Status == AnalysisRunStatus.Failed
                    || run.Status == AnalysisRunStatus.Skipped
                )
            )
            .Select(run => new TerminalAnalysisRun(
                run.Analysis.AnalysisType,
                run.Status,
                run.StartedAt,
                run.CompletedAt!.Value
            ))
            .ToListAsync();

        var workflowStatusCounts = new StatusCounts
        {
            Succeeded = terminalWorkflows.Count(w => w.Status == WorkflowStatus.Succeeded),
            Failed = terminalWorkflows.Count(w => w.Status == WorkflowStatus.Failed),
            Skipped = terminalWorkflows.Count(w => w.Status == WorkflowStatus.Skipped),
            InProgress = await workflows.CountAsync(w => w.Status == WorkflowStatus.InProgress),
            Pending = await workflows.CountAsync(w => w.Status == WorkflowStatus.Pending),
        };

        var runStatusCounts = new StatusCounts
        {
            Succeeded = terminalAnalysisRuns.Count(run =>
                run.Status == AnalysisRunStatus.Succeeded
            ),
            Failed = terminalAnalysisRuns.Count(run => run.Status == AnalysisRunStatus.Failed),
            Skipped = terminalAnalysisRuns.Count(run => run.Status == AnalysisRunStatus.Skipped),
            InProgress = await analysisRuns.CountAsync(r =>
                r.Status == AnalysisRunStatus.InProgress
            ),
            Pending = await analysisRuns.CountAsync(r => r.Status == AnalysisRunStatus.Pending),
        };

        var succeeded = runStatusCounts.Succeeded;
        var failed = runStatusCounts.Failed;
        var successRate =
            (succeeded + failed) == 0 ? 0.0 : (double)succeeded / (succeeded + failed);

        var perWorkflowType = terminalWorkflows
            .GroupBy(w => w.WorkflowType)
            .Select(g =>
            {
                var typeSucceeded = g.Count(w => w.Status == WorkflowStatus.Succeeded);
                var typeFailed = g.Count(w => w.Status == WorkflowStatus.Failed);
                var typeSkipped = g.Count(w => w.Status == WorkflowStatus.Skipped);
                var durations = g.Where(w => w.StartedAt != null)
                    .Select(w => (w.CompletedAt - w.StartedAt!.Value).TotalSeconds)
                    .Where(s => s >= 0)
                    .ToList();
                return new WorkflowTypeStat
                {
                    WorkflowType = g.Key,
                    Total = g.Count(),
                    Succeeded = typeSucceeded,
                    Failed = typeFailed,
                    Skipped = typeSkipped,
                    FailureRate =
                        (typeSucceeded + typeFailed) == 0
                            ? 0.0
                            : (double)typeFailed / (typeSucceeded + typeFailed),
                    AverageDurationSeconds = durations.Count == 0 ? null : durations.Average(),
                };
            })
            .OrderByDescending(s => s.Total)
            .ToList();

        var perAnalysisType = BuildAnalysisTypeStats(terminalAnalysisRuns);

        var trend = BuildTrend(terminalAnalysisRuns, bucketBoundaries);

        var stuckThreshold = now.AddMinutes(-_options.StuckWorkflowThresholdMinutes);
        var stuck = await workflows
            .Where(w =>
                w.Status == WorkflowStatus.InProgress
                && w.StartedAt != null
                && w.StartedAt < stuckThreshold
            )
            .OrderBy(w => w.StartedAt)
            .Take(20)
            .Select(w => new
            {
                w.Id,
                w.WorkflowType,
                w.AnalysisRunId,
                w.StartedAt,
            })
            .ToListAsync();

        var stuckDtos = stuck
            .Select(w => new StuckWorkflowDto
            {
                Id = w.Id,
                WorkflowType = w.WorkflowType,
                AnalysisRunId = w.AnalysisRunId,
                StartedAt = w.StartedAt,
                MinutesRunning = w.StartedAt is { } s ? (now - s).TotalMinutes : 0,
            })
            .ToList();

        var analysisGroupCounts = new AnalysisGroupCounts
        {
            Pending = await analysisGroups.CountAsync(g => g.Status == AnalysisGroupStatus.Pending),
            Complete = await analysisGroups.CountAsync(g =>
                g.Status == AnalysisGroupStatus.Complete
            ),
            TimedOut = await analysisGroups.CountAsync(g =>
                g.Status == AnalysisGroupStatus.TimedOut
            ),
        };

        var inspectionRecordsIngested = await access
            .Filter(context.InspectionRecords, AccessMode.Read)
            .CountAsync(ir => ir.CreatedAt >= since);

        return new DashboardSummaryDto
        {
            WindowHours = windowHours,
            Since = since,
            GeneratedAt = now,
            WorkflowStatusCounts = workflowStatusCounts,
            RunStatusCounts = runStatusCounts,
            SuccessRate = successRate,
            CurrentlyRunning = new RunningCounts
            {
                Workflows = workflowStatusCounts.InProgress,
                Runs = runStatusCounts.InProgress,
            },
            PerWorkflowType = perWorkflowType,
            PerAnalysisType = perAnalysisType,
            Stuck = stuckDtos,
            AnalysisGroupCounts = analysisGroupCounts,
            InspectionRecordsIngested = inspectionRecordsIngested,
            Trend = trend,
        };
    }

    public async Task<TrendBucketDetailsDto> GetTrendBucketDetails(
        DateTime bucketStart,
        int windowHours,
        TimeZoneInfo timeZone
    )
    {
        bucketStart = bucketStart.ToUniversalTime();
        var bucketEnd = GetBucketEnd(bucketStart, windowHours, timeZone);
        var runs = await access
            .Filter(context.AnalysisRuns, AccessMode.Read)
            .Where(run =>
                run.CompletedAt != null
                && run.StartedAt != null
                && run.StartedAt >= bucketStart
                && run.StartedAt < bucketEnd
                && (
                    run.Status == AnalysisRunStatus.Succeeded
                    || run.Status == AnalysisRunStatus.Failed
                )
            )
            .Select(run => new TerminalAnalysisRun(
                run.Analysis.AnalysisType,
                run.Status,
                run.StartedAt,
                run.CompletedAt!.Value
            ))
            .ToListAsync();

        return new TrendBucketDetailsDto
        {
            BucketStart = bucketStart,
            BucketEnd = bucketEnd,
            PerAnalysisType = BuildAnalysisTypeStats(runs),
        };
    }

    /// <summary>
    /// Buckets terminal analysis runs into hourly buckets for a &lt;= 24h window,
    /// otherwise daily buckets, keeping every bucket in range (including empty).
    /// </summary>
    private static List<TrendBucket> BuildTrend(
        IReadOnlyList<TerminalAnalysisRun> terminalAnalysisRuns,
        IReadOnlyList<DateTime> bucketBoundaries
    )
    {
        var buckets = new List<TrendBucket>();
        for (var index = 0; index < bucketBoundaries.Count - 1; index++)
        {
            var bucketStart = bucketBoundaries[index];
            var bucketEnd = bucketBoundaries[index + 1];
            var runs = terminalAnalysisRuns.Where(run =>
                run.StartedAt >= bucketStart && run.StartedAt < bucketEnd
            );
            buckets.Add(
                new TrendBucket
                {
                    BucketStart = bucketStart,
                    BucketEnd = bucketEnd,
                    Succeeded = runs.Count(run => run.Status == AnalysisRunStatus.Succeeded),
                    Failed = runs.Count(run => run.Status == AnalysisRunStatus.Failed),
                }
            );
        }

        return buckets;
    }

    private static List<DateTime> BuildBucketBoundaries(
        int windowHours,
        TimeZoneInfo timeZone,
        DateTime now
    )
    {
        if (windowHours <= 24)
        {
            var currentHour = new DateTime(
                now.Year,
                now.Month,
                now.Day,
                now.Hour,
                0,
                0,
                DateTimeKind.Utc
            );
            return Enumerable
                .Range(0, windowHours + 1)
                .Select(index => currentHour.AddHours(index - windowHours + 1))
                .ToList();
        }

        var dayCount = windowHours / 24;
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(now, timeZone).Date;
        return Enumerable
            .Range(0, dayCount + 1)
            .Select(index =>
                TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(
                        localToday.AddDays(index - dayCount + 1),
                        DateTimeKind.Unspecified
                    ),
                    timeZone
                )
            )
            .ToList();
    }

    private static DateTime GetBucketEnd(
        DateTime bucketStart,
        int windowHours,
        TimeZoneInfo timeZone
    )
    {
        if (windowHours <= 24)
            return bucketStart.AddHours(1);

        var localDate = TimeZoneInfo.ConvertTimeFromUtc(bucketStart, timeZone).Date;
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate.AddDays(1), DateTimeKind.Unspecified),
            timeZone
        );
    }

    private static List<AnalysisTypeStat> BuildAnalysisTypeStats(
        IReadOnlyList<TerminalAnalysisRun> runs
    ) =>
        runs.GroupBy(run => run.AnalysisType)
            .Select(group =>
            {
                var typeSucceeded = group.Count(run => run.Status == AnalysisRunStatus.Succeeded);
                var typeFailed = group.Count(run => run.Status == AnalysisRunStatus.Failed);
                var durations = group
                    .Where(run => run.StartedAt != null)
                    .Select(run => (run.CompletedAt - run.StartedAt!.Value).TotalSeconds)
                    .Where(seconds => seconds >= 0)
                    .ToList();
                return new AnalysisTypeStat
                {
                    AnalysisType = group.Key,
                    Total = group.Count(),
                    Succeeded = typeSucceeded,
                    Failed = typeFailed,
                    Skipped = group.Count(run => run.Status == AnalysisRunStatus.Skipped),
                    FailureRate =
                        typeSucceeded + typeFailed == 0
                            ? 0.0
                            : (double)typeFailed / (typeSucceeded + typeFailed),
                    AverageDurationSeconds = durations.Count == 0 ? null : durations.Average(),
                };
            })
            .OrderByDescending(stat => stat.Total)
            .ToList();
}
