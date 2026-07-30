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
            Succeeded = await context.AnalysisRuns.CountAsync(r =>
                r.Status == AnalysisRunStatus.Succeeded
                && r.CompletedAt != null
                && r.CompletedAt >= since
            ),
            Failed = await context.AnalysisRuns.CountAsync(r =>
                r.Status == AnalysisRunStatus.Failed
                && r.CompletedAt != null
                && r.CompletedAt >= since
            ),
            Skipped = await context.AnalysisRuns.CountAsync(r =>
                r.Status == AnalysisRunStatus.Skipped
                && r.CompletedAt != null
                && r.CompletedAt >= since
            ),
            InProgress = await context.AnalysisRuns.CountAsync(r =>
                r.Status == AnalysisRunStatus.InProgress
            ),
            Pending = await context.AnalysisRuns.CountAsync(r =>
                r.Status == AnalysisRunStatus.Pending
            ),
        };

        var succeeded = workflowStatusCounts.Succeeded;
        var failed = workflowStatusCounts.Failed;
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

        var trend = BuildTrend(terminalWorkflows, since, now, windowHours);

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
            Stuck = stuckDtos,
            AnalysisGroupCounts = analysisGroupCounts,
            InspectionRecordsIngested = inspectionRecordsIngested,
            Trend = trend,
        };
    }

    /// <summary>
    /// Buckets terminal workflows into hourly buckets for a &lt;= 24h window,
    /// otherwise daily buckets, keeping every bucket in range (including empty).
    /// </summary>
    private static List<TrendBucket> BuildTrend(
        IReadOnlyList<TerminalWorkflow> terminalWorkflows,
        DateTime since,
        DateTime now,
        int windowHours
    )
    {
        var hourly = windowHours <= 24;
        var bucketSpan = hourly ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);

        DateTime Truncate(DateTime dt) =>
            hourly
                ? new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc)
                : new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc);

        var counts = terminalWorkflows
            .Where(w => w.Status == WorkflowStatus.Succeeded || w.Status == WorkflowStatus.Failed)
            .GroupBy(w => Truncate(w.CompletedAt))
            .ToDictionary(
                g => g.Key,
                g =>
                    (
                        Succeeded: g.Count(w => w.Status == WorkflowStatus.Succeeded),
                        Failed: g.Count(w => w.Status == WorkflowStatus.Failed)
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
                    Succeeded = c.Succeeded,
                    Failed = c.Failed,
                }
            );
        }

        return buckets;
    }
}
