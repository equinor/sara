namespace api.Controllers.Models;

/// <summary>
/// Aggregated snapshot of pipeline activity for the SARA overview dashboard.
/// Terminal counts (Succeeded/Failed/Skipped) are bucketed by CompletedAt within
/// the requested window; live counts (InProgress/Pending) reflect the current state.
/// </summary>
public class DashboardSummaryDto
{
    public required int WindowHours { get; init; }
    public required DateTime Since { get; init; }
    public required DateTime GeneratedAt { get; init; }

    public required StatusCounts WorkflowStatusCounts { get; init; }
    public required StatusCounts RunStatusCounts { get; init; }

    /// <summary>Succeeded / (Succeeded + Failed) over the window; 0 when none finished.</summary>
    public required double SuccessRate { get; init; }

    public required RunningCounts CurrentlyRunning { get; init; }

    public required List<WorkflowTypeStat> PerWorkflowType { get; init; }

    public required List<StuckWorkflowDto> Stuck { get; init; }

    public required AnalysisGroupCounts AnalysisGroupCounts { get; init; }

    public required int InspectionRecordsIngested { get; init; }

    public required List<TrendBucket> Trend { get; init; }
}

public class StatusCounts
{
    public int Pending { get; init; }
    public int InProgress { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public int Total => Pending + InProgress + Succeeded + Failed + Skipped;
}

public class RunningCounts
{
    public int Workflows { get; init; }
    public int Runs { get; init; }
}

public class WorkflowTypeStat
{
    public required string WorkflowType { get; init; }
    public int Total { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public double FailureRate { get; init; }
    public double? AverageDurationSeconds { get; init; }
}

public class StuckWorkflowDto
{
    public required Guid Id { get; init; }
    public required string WorkflowType { get; init; }
    public Guid AnalysisRunId { get; init; }
    public DateTime? StartedAt { get; init; }
    public double MinutesRunning { get; init; }
}

public class AnalysisGroupCounts
{
    public int Pending { get; init; }
    public int Complete { get; init; }
    public int TimedOut { get; init; }
}

public class TrendBucket
{
    public required DateTime BucketStart { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
}
