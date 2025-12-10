using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using api.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers.WorkflowNotification;

[ApiController]
[Route("workflow-notification/constant-level-oiler-estimator")]
public class CLOEWorkflowNotificationController(
    ILogger<CLOEWorkflowNotificationController> logger,
    IPlantDataService plantDataService,
    IArgoWorkflowService workflowService
) : ControllerBase
{
    /// <summary>
    /// Notify that the CLOE workflow has started
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("started")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlantDataResponse>> CLOEStarted(
        [FromBody] WorkflowStartedNotification notification
    )
    {
        var inspectionId = Sanitize.SanitizeUserInput(notification.InspectionId);
        logger.LogDebug(
            "Received notification that the CLOE workflow has started for inspection id {inspectionId}",
            inspectionId
        );

        PlantData updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateCLOEWorkflowStatus(
                inspectionId,
                WorkflowStatus.Started
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating CLOE workflow status");
            return BadRequest(ex.Message);
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlantDataResponse>> CLOEResult(
        [FromBody] CLOEWorkflowResultNotification notification
    )
    {
        logger.LogDebug(
            "Received notification with result from the CLOE workflow with inspection id {id}. OilLevel: {oilLevel}",
            notification.InspectionId,
            notification.OilLevel
        );

        PlantData updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateCLOEResult(
                notification.InspectionId,
                notification.OilLevel
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
        var workflowStatus = workflowService.GetWorkflowStatus(notification, "CLOE");

        PlantData updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateCLOEWorkflowStatus(
                notification.InspectionId,
                workflowStatus
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating CLOE workflow status");
            return BadRequest(ex.Message);
        }

        return Ok(updatedPlantData);
    }
}
