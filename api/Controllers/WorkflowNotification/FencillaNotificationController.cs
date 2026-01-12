using api.Controllers.Models;
using api.Database.Models;
using api.MQTT;
using api.Services;
using api.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers.WorkflowNotification;

[ApiController]
[Route("workflow-notification/fencilla")]
public class FencillaWorkflowNotificationController(
    ILogger<FencillaWorkflowNotificationController> logger,
    IPlantDataService plantDataService,
    IArgoWorkflowService workflowService,
    IMqttPublisherService mqttPublisherService
) : ControllerBase
{
    /// <summary>
    /// Notify that the Fencilla workflow has started
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("started")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlantDataResponse>> FencillaStarted(
        [FromBody] WorkflowStartedNotification notification
    )
    {
        var inspectionId = Sanitize.SanitizeUserInput(notification.InspectionId);
        logger.LogDebug(
            "Received notification that the Fencilla workflow has started for inspection id {inspectionId}",
            inspectionId
        );

        PlantData updatedPlantData;
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

        return Ok(updatedPlantData);
    }

    /// <summary>
    /// Notify about the result of the Fencilla workflow
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("result")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlantDataResponse>> FencillaResult(
        [FromBody] FencillaWorkflowResultNotification notification
    )
    {
        logger.LogDebug(
            "Received notification with result from the Fencilla workflow with inspection id {id}. IsBreak: {isBreak}, Confidence: {confidence}",
            notification.InspectionId,
            notification.IsBreak,
            notification.Confidence
        );

        PlantData updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateFencillaResult(
                notification.InspectionId,
                notification.IsBreak,
                notification.Confidence
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PlantDataResponse>> FencillaExited(
        [FromBody] WorkflowExitedNotification notification
    )
    {
        var workflowStatus = workflowService.GetWorkflowStatus(notification, "Fencilla");

        PlantData updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateFencillaWorkflowStatus(
                notification.InspectionId,
                workflowStatus
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating Fencilla workflow status");
            return BadRequest(ex.Message);
        }

        var fencillaAnalysis =
            updatedPlantData.FencillaAnalysis
            ?? throw new InvalidOperationException(
                $"Fencilla analysis is not set up for plant data with inspection id {notification.InspectionId}"
            );

        string? warning = null;
        if (fencillaAnalysis.IsBreak != null)
        {
            if ((bool)fencillaAnalysis.IsBreak)
            {
                warning = "Breach detected";
            }
        }

        var message = new SaraAnalysisResultMessage
        {
            InspectionId = updatedPlantData.InspectionId,
            AnalysisType = nameof(AnalysisType.Fencilla),
            Value = fencillaAnalysis.IsBreak.ToString(),
            Unit = "bool [isBreach]",
            Warning = warning,
            Confidence = fencillaAnalysis.Confidence,
            StorageAccount = fencillaAnalysis.DestinationBlobStorageLocation.StorageAccount,
            BlobContainer = fencillaAnalysis.DestinationBlobStorageLocation.BlobContainer,
            BlobName = fencillaAnalysis.DestinationBlobStorageLocation.BlobName,
        };

        await mqttPublisherService.PublishSaraAnalysisResultAvailable(message);

        return Ok(updatedPlantData);
    }
}
