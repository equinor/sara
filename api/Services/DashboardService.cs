using api.Configurations;
using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummary(int windowHours);
    Task<TrendBucketDetailsDto> GetTrendBucketDetails(DateTime bucketStart, int windowHours);
}

public class DashboardService(SaraDbContext context, IOptions<DashboardOptions> options)
    : IDashboardService
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

    public async Task<DashboardSummaryDto> GetSummary(int windowHours)
    {
        var now = DateTime.UtcNow;
        var since = now.AddHours(-windowHours);

        // Single projection of all workflows that reached a terminal state within
        // the window. Everything time-bucketed (status counts, per-type stats,
        // trend) is derived from this in memory.
        var terminalWorkflows = await context
            .Workflows.Where(w =>
                w.CompletedAt != null
                && w.CompletedAt >= since
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

        var terminalAnalysisRuns = await context
            .AnalysisRuns.Where(run =>
                run.CompletedAt != null
                && run.CompletedAt >= since
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
            InProgress = await context.Workflows.CountAsync(w =>
                w.Status == WorkflowStatus.InProgress
            ),
            Pending = await context.Workflows.CountAsync(w => w.Status == WorkflowStatus.Pending),
        };

        var runStatusCounts = new StatusCounts
        {
            Succeeded = terminalAnalysisRuns.Count(run =>
                run.Status == AnalysisRunStatus.Succeeded
            ),
            Failed = terminalAnalysisRuns.Count(run => run.Status == AnalysisRunStatus.Failed),
            Skipped = terminalAnalysisRuns.Count(run => run.Status == AnalysisRunStatus.Skipped),
            InProgress = await context.AnalysisRuns.CountAsync(r =>
                r.Status == AnalysisRunStatus.InProgress
            ),
            Pending = await context.AnalysisRuns.CountAsync(r =>
                r.Status == AnalysisRunStatus.Pending
            ),
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

        var trend = BuildTrend(terminalAnalysisRuns, since, now, windowHours);

        var stuckThreshold = now.AddMinutes(-_options.StuckWorkflowThresholdMinutes);
        var stuck = await context
            .Workflows.Where(w =>
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
            Pending = await context.AnalysisGroups.CountAsync(g =>
                g.Status == AnalysisGroupStatus.Pending
            ),
            Complete = await context.AnalysisGroups.CountAsync(g =>
                g.Status == AnalysisGroupStatus.Complete
            ),
            TimedOut = await context.AnalysisGroups.CountAsync(g =>
                g.Status == AnalysisGroupStatus.TimedOut
            ),
        };

        var inspectionRecordsIngested = await context.InspectionRecords.CountAsync(ir =>
            ir.CreatedAt >= since
        );

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
        int windowHours
    )
    {
        bucketStart = bucketStart.ToUniversalTime();
        var bucketEnd = bucketStart + GetBucketSpan(windowHours);
        var runs = await context
            .AnalysisRuns.Where(run =>
                run.CompletedAt != null
                && run.CompletedAt >= bucketStart
                && run.CompletedAt < bucketEnd
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
        DateTime since,
        DateTime now,
        int windowHours
    )
    {
        var hourly = windowHours <= 24;
        var bucketSpan = GetBucketSpan(windowHours);

        DateTime Truncate(DateTime dt) =>
            hourly
                ? new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc)
                : new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc);

        var counts = terminalAnalysisRuns
            .Where(run =>
                run.Status == AnalysisRunStatus.Succeeded || run.Status == AnalysisRunStatus.Failed
            )
            .GroupBy(run => Truncate(run.CompletedAt))
            .ToDictionary(
                g => g.Key,
                g =>
                    (
                        Succeeded: g.Count(run => run.Status == AnalysisRunStatus.Succeeded),
                        Failed: g.Count(run => run.Status == AnalysisRunStatus.Failed)
                    )
            );

        var buckets = new List<TrendBucket>();
        for (var bucket = Truncate(since); bucket <= now; bucket += bucketSpan)
        {
            counts.TryGetValue(bucket, out var c);
            buckets.Add(
                new TrendBucket
                {
                    BucketStart = bucket,
                    BucketEnd = bucket + bucketSpan,
                    Succeeded = c.Succeeded,
                    Failed = c.Failed,
                }
            );
        }

        return buckets;
    }

    private static TimeSpan GetBucketSpan(int windowHours) =>
        windowHours <= 24 ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);

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
