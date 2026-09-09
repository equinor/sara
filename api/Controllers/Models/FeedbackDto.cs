using api.Database.Models;

namespace api.Controllers.Models;

public class FeedbackDto
{
    public FeedbackDto(AnalysisRunFeedback feedback)
    {
        Id = feedback.Id;
        AnalysisRunId = feedback.AnalysisRunId;
        IsCorrect = feedback.IsCorrect;
    }

    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public bool IsCorrect { get; set; }
}

public class UpsertFeedbackRequest
{
    public bool IsCorrect { get; set; }
}

public class FeedbackHistoryDto
{
    public Guid Id { get; init; }
    public Guid AnalysisRunId { get; init; }
    public Guid AnalysisId { get; init; }
    public required string AnalysisType { get; init; }
    public int RunNumber { get; init; }
    public AnalysisRunStatus RunStatus { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public bool IsCorrect { get; init; }
}

public class FeedbackSummaryDto
{
    public required int WindowHours { get; init; }
    public required DateTime Since { get; init; }
    public required DateTime GeneratedAt { get; init; }
    public int TotalRuns { get; init; }
    public int Reviewed { get; init; }
    public int Correct { get; init; }
    public int Incorrect { get; init; }
    public double CorrectnessRate => Reviewed == 0 ? 0 : (double)Correct / Reviewed;
    public double ReviewRate => TotalRuns == 0 ? 0 : (double)Reviewed / TotalRuns;
    public required List<FeedbackAnalysisTypeStatDto> PerAnalysisType { get; init; }
    public required List<FeedbackTrendBucketDto> Trend { get; init; }
}

public class FeedbackAnalysisTypeStatDto
{
    public required string AnalysisType { get; init; }
    public int TotalRuns { get; init; }
    public int Reviewed { get; init; }
    public int Correct { get; init; }
    public int Incorrect { get; init; }
    public double CorrectnessRate => Reviewed == 0 ? 0 : (double)Correct / Reviewed;
    public double ReviewRate => TotalRuns == 0 ? 0 : (double)Reviewed / TotalRuns;
}

public class FeedbackTrendBucketDto
{
    public required DateTime BucketStart { get; init; }
    public required DateTime BucketEnd { get; init; }
    public int Correct { get; init; }
    public int Incorrect { get; init; }
}

public class FeedbackTrendBucketDetailsDto
{
    public required DateTime BucketStart { get; init; }
    public required DateTime BucketEnd { get; init; }
    public required List<FeedbackTrendAnalysisTypeStatDto> PerAnalysisType { get; init; }
}

public class FeedbackTrendAnalysisTypeStatDto
{
    public required string AnalysisType { get; init; }
    public int Correct { get; init; }
    public int Incorrect { get; init; }
}
