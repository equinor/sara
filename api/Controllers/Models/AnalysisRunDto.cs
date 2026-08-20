using api.Database.Models;
using api.Services;

namespace api.Controllers.Models;

public class AnalysisRunDto
{
    public AnalysisRunDto(AnalysisRun run, IBlobStorageService blobService)
    {
        Id = run.Id;
        AnalysisId = run.AnalysisId;
        RunNumber = run.RunNumber;
        Status = run.Status;
        StartedAt = run.StartedAt;
        CompletedAt = run.CompletedAt;
        SkipReason = run.SkipReason;
        Workflows = run.Workflows.Select(w => new WorkflowDto(w, blobService)).ToList();
        Feedback = run.Feedback is { } f ? new FeedbackDto(f) : null;
    }

    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }
    public int RunNumber { get; set; }
    public AnalysisRunStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? SkipReason { get; set; }
    public List<WorkflowDto> Workflows { get; set; } = [];
    public FeedbackDto? Feedback { get; set; }
}
