using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers.WorkflowNotification;


[ApiController]
[Route("workflow-notification/constant-level-oiler-estimator")]
public class CLOEWorkflowNotificationController(
    ILogger<CLOEWorkflowNotificationController> logger,
    IPlantDataService plantDataService
) : ControllerBase
{
    /// <summary>
    /// Notify that the CLOE workflow has started
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("started")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> CLOEStarted(
        [FromBody] WorkflowStartedNotification notification
    )
    {
        logger.LogDebug(
            "Received notification that the CLOE workflow has started for inspection id {inspectionId}",
            notification.InspectionId
        );

        PlantData? updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateCLOEWorkflowStatus(
                notification.InspectionId,
                WorkflowStatus.Started
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating CLOE workflow status");
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
    /// Notify about the result of the CLOE workflow
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("result")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> CLOEResult(
        [FromBody] WorkflowResultNotification<CLOEResult> notification
    )
    {
        // Raise an error if notification.Result.OilLevel is not a float between 0 and 100
        if (notification.Result.OilLevel < 0 || notification.Result.OilLevel > 100)
        {
            logger.LogError(
                "Invalid oil level {oilLevel} received for inspection id {id}. Must be between 0 and 100.",
                notification.Result.OilLevel,
                notification.InspectionId
            );
            return BadRequest(
                $"Invalid oil level {notification.Result.OilLevel} received. Must be between 0 and 100."
            );
        }


        logger.LogDebug(
            "Received notification with result from the CLOE workflow with inspection id {id}. OilLevel: {oilLevel}",
            notification.InspectionId,
            notification.Result.OilLevel
        );

        var plantData = await plantDataService.ReadByInspectionId(notification.InspectionId);
        if (plantData == null)
        {
            return NotFound(
                $"Could not find plantData with inspection id {notification.InspectionId}"
            );
        }

        // TODO: Update plantData with information that the CLOE is Done
        // var updatedPlantData = await plantDataService.UpdateCLOEWorkflowStatusAndValue(
        //     notification.InspectionId,
        //     WorkflowStatus.ExitSuccess,
        //     notification.Result.OilLevel
        // );
        PlantData? updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateCLOEResult(
                notification.InspectionId,
                notification.Result.OilLevel
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating CLOE result");
            return BadRequest(ex.Message);
        }

        return Ok(updatedPlantData);
    }

    /// <summary>
    /// Notify that the CLOE workflow has exited with success or failure
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("exited")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> CLOEExited(
        [FromBody] WorkflowExitedNotification notification
    )
    {
        logger.LogInformation(
            "Received notification that the CLOE workflow has started for inspection id {id} with workflow status: {status} and failures: {failures}",
            notification.InspectionId,
            notification.WorkflowStatus,
            notification.WorkflowFailures
        );

        PlantData? updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateCLOEWorkflowStatus(
                notification.InspectionId,
                WorkflowStatus.ExitSuccess
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating CLOE workflow status");
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
