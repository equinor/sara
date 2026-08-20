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
