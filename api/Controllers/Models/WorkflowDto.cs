using api.Database.Models;
using api.Services;

namespace api.Controllers.Models;

public class WorkflowDto(Workflow workflow, IBlobStorageService blobService)
{
    public Guid Id { get; set; } = workflow.Id;

    public int StepNumber { get; set; } = workflow.StepNumber;

    public string WorkflowType { get; set; } = workflow.WorkflowType;

    public List<Uri> InputBlobSAS { get; set; } =
        workflow
            .InputBlobStorageLocations.Select(
                (i) => blobService.CreateUserDelegationSASUri(i).Result
            )
            .ToList();

    public WorkflowStatus Status { get; set; } = workflow.Status;

    public Uri? OutputBlobSAS { get; set; } =
        workflow.OutputBlobStorageLocation != null
            ? blobService.CreateUserDelegationSASUri(workflow.OutputBlobStorageLocation).Result
            : null;

    public string? ResultJson { get; set; } = workflow.ResultJson;

    public DateTime? StartedAt { get; set; } = workflow.StartedAt;

    public DateTime? CompletedAt { get; set; } = workflow.CompletedAt;

    public string? ErrorMessage { get; set; } = workflow.ErrorMessage;
}
