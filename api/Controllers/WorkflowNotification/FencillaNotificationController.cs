using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers.WorkflowNotification;


[ApiController]
[Route("workflow-notification/fencilla")]
public class FencillaWorkflowNotificationController(
    ILogger<FencillaWorkflowNotificationController> logger,
    IPlantDataService plantDataService
) : ControllerBase
{
    /// <summary>
    /// Notify that the Fencilla workflow has started
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("started")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> FencillaStarted(
        [FromBody] WorkflowStartedNotification notification
    )
    {
        logger.LogDebug(
            "Received notification that the Fencilla workflow has started for inspection id {inspectionId}",
            notification.InspectionId
        );

        PlantData? updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateFencillaWorkflowStatus(
                notification.InspectionId,
                WorkflowStatus.Started
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating Fencilla workflow status");
            return BadRequest(ex.Message);
        }

        if (updatedPlantData == null)
        {
            return NotFound(
                $"Could not find workflow with inspection id {notification.InspectionId}"
            );
        }
        return Ok(updatedPlantData);
    }

    /// <summary>
    /// Notify about the result of the Fencilla workflow
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("result")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> FencillaResult(
        [FromBody] WorkflowResultNotification<FencillaResult> notification
    )
    {
        if (notification.Result.IsBreak.GetType() != typeof(bool))
        {
            logger.LogError(
                "Invalid IsBreak value {isBreak} received for inspection id {id}. Must be a boolean.",
                notification.Result.IsBreak,
                notification.InspectionId
            );
            return BadRequest(
                $"Invalid IsBreak value {notification.Result.IsBreak} received. Must be a boolean."
            );
        }

        if (notification.Result.Confidence < 0 || notification.Result.Confidence > 1)
        {
            logger.LogError(
                "Invalid Confidence value {confidence} received for inspection id {id}. Must be between 0 and 1.",
                notification.Result.Confidence,
                notification.InspectionId
            );
            return BadRequest(
                $"Invalid Confidence value {notification.Result.Confidence} received. Must be between 0 and 1."
            );
        }

        logger.LogDebug(
            "Received notification with result from the Fencilla workflow with inspection id {id}. IsBreak: {isBreak}, Confidence: {confidence}",
            notification.InspectionId,
            notification.Result.IsBreak,
            notification.Result.Confidence
        );

        var plantData = await plantDataService.ReadByInspectionId(notification.InspectionId);
        if (plantData == null)
        {
            return NotFound(
                $"Could not find plantData with inspection id {notification.InspectionId}"
            );
        }


        PlantData? updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateFencillaResult(
                notification.InspectionId,
                notification.Result.IsBreak,
                notification.Result.Confidence
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating Fencilla result");
            return BadRequest(ex.Message);
        }

        return Ok(updatedPlantData);
    }

    /// <summary>
    /// Notify that the Fencilla workflow has exited with success or failure
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("exited")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> FencillaExited(
        [FromBody] WorkflowExitedNotification notification
    )
    {
        logger.LogInformation(
            "Received notification that the Fencilla workflow has started for inspection id {id} with workflow status: {status} and failures: {failures}",
            notification.InspectionId,
            notification.WorkflowStatus,
            notification.WorkflowFailures
        );

        PlantData? updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateFencillaWorkflowStatus(
                notification.InspectionId,
                WorkflowStatus.ExitSuccess
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating Fencilla workflow status");
            return BadRequest(ex.Message);
        }

        if (updatedPlantData == null)
        {
            return NotFound(
                $"Could not find workflow with inspection id {notification.InspectionId}"
            );
        }
        return Ok(updatedPlantData);
    }
}
