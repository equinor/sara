using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

public class StidWorkflowStartedNotification
{
    public required string InspectionId { get; set; }
    public required string WorkflowName { get; set; }
}

public class StidWorkflowExitedNotification
{
    public required string InspectionId { get; set; }
    public required int StidMediaId { get; set; }
    public required string WorkflowStatus { get; set; }
    public required string WorkflowFailures { get; set; }
}

[ApiController]
[Route("[controller]")]
public class StidWorkflowController(
    IStidWorkflowService stidWorkflowService,
    ILogger<StidWorkflowController> logger
) : ControllerBase
{
    /// <summary>
    /// Updates status of stid data to started
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("notify-workflow-started")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StidDataResponse>> WorkflowStarted(
        [FromBody] StidWorkflowStartedNotification notification
    )
    {
        var updatedStidData = await stidWorkflowService.UpdateStidWorkflowStatus(
            notification.InspectionId,
            WorkflowStatus.Started
        );
        logger.LogInformation(
            "Stid uploader workflow for inspection {inspectionId} started with workflow name {workflowName}",
            notification.InspectionId,
            notification.WorkflowName
        );
        if (updatedStidData == null)
        {
            return NotFound(
                $"Could not find workflow with inspection id {notification.InspectionId}"
            );
        }
        return Ok(updatedStidData);
    }

    /// <summary>
    /// Updates status of the workflow to exit with success or failure
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("notify-workflow-exited")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StidDataResponse>> WorkflowExited(
        [FromBody] StidWorkflowExitedNotification notification
    )
    {
        WorkflowStatus status;
        int? mediaId = null;

        if (notification.WorkflowStatus == "Succeeded")
        {
            status = WorkflowStatus.ExitSuccess;
            mediaId = notification.StidMediaId;
        }
        else
        {
            logger.LogWarning(
                "Stid uploader workflow failed with status {status}",
                notification.WorkflowStatus
            );
            status = WorkflowStatus.ExitFailure;
        }

        await stidWorkflowService.UpdateStidMediaId(notification.InspectionId, mediaId);

        var updatedStidData = await stidWorkflowService.UpdateStidWorkflowStatus(
            notification.InspectionId,
            status
        );

        logger.LogInformation(
            "Stid uploader workflow for inspection {inspectionId} exited with status {status}",
            notification.InspectionId,
            notification.WorkflowStatus
        );

        logger.LogInformation(
            "StidMediaId for inspection {inspectionId} set to {mediaId}",
            notification.InspectionId,
            mediaId
        );

        return Ok(updatedStidData);
    }
}
