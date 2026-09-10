using api.Database.Models;
using api.Services;
using Azure;

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
        foreach (var workflow in run.Workflows)
        {
            try
            {
                Workflows.Add(new WorkflowDto(workflow, blobService));
            }
            catch (Exception exception)
                when (exception.GetBaseException()
                        is RequestFailedException
                        {
                            Status: StatusCodes.Status403Forbidden,
                            ErrorCode: "AuthorizationPermissionMismatch",
                        }
                ) { }
        }
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
