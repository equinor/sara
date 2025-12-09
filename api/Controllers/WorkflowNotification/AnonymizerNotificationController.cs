using api.Controllers.Models;
using api.Database.Models;
using api.MQTT;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers.WorkflowNotification;

[ApiController]
[Route("workflow-notification/anonymizer")]
public class AnonymizerWorkflowNotificationController(
    ILogger<AnonymizerWorkflowNotificationController> logger,
    IPlantDataService plantDataService,
    IArgoWorkflowService workflowService,
    IMqttMessageService mqttMessageService
) : ControllerBase
{
    /// <summary>
    /// Notify that the anonymizer workflow has started
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("started")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlantDataResponse>> AnonymizerStarted(
        [FromBody] WorkflowStartedNotification notification
    )
    {
        logger.LogDebug(
            "Received notification that the anonymizer workflow has started for inspection id {inspectionId}",
            notification.InspectionId
        );

        PlantData updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateAnonymizerWorkflowStatus(
                notification.InspectionId,
                WorkflowStatus.Started
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occured while updating Anonymizer workflow status");
            return BadRequest(ex.Message);
        }

        return Ok(updatedPlantData);
    }

    /// <summary>
    /// Notify about the result of the anonymizer workflow
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("result")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlantDataResponse>> AnonymizerResult(
        [FromBody] AnonymizerWorkflowResultNotification notification
    )
    {
        logger.LogDebug(
            "Received notification with result from the anonymizer workflow with inspection id {id}. IsPersonInImage: {isPersonInImage}",
            notification.InspectionId,
            notification.IsPersonInImage
        );

        PlantData updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateAnonymizerResult(
                notification.InspectionId,
                notification.IsPersonInImage
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating Anonymizer result");
            return BadRequest(ex.Message);
        }

        return Ok(updatedPlantData);
    }

    /// <summary>
    /// Notify that the anonymizer workflow has exited with success or failure
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("exited")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlantDataResponse>> AnonymizerExited(
        [FromBody] WorkflowExitedNotification notification
    )
    {
        var workflowStatus = workflowService.GetWorkflowStatus(notification, "Anonymizer");

        PlantData updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateAnonymizerWorkflowStatus(
                notification.InspectionId,
                workflowStatus
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating anonymizer workflow status");
            return BadRequest(ex.Message);
        }

        if (workflowStatus == WorkflowStatus.ExitFailure)
        {
            logger.LogWarning(
                "Anonymizer workflow failure. Handler is not proceeding to trigger subsequent workflows"
            );
            return Ok(updatedPlantData);
        }

        var message = new SaraVisualizationAvailableMessage
        {
            InspectionId = notification.InspectionId,
            StorageAccount = updatedPlantData
                .Anonymization
                .SourceBlobStorageLocation
                .StorageAccount,
            BlobContainer = updatedPlantData.Anonymization.SourceBlobStorageLocation.BlobContainer,
            BlobName = updatedPlantData.Anonymization.SourceBlobStorageLocation.BlobName,
        };
        mqttMessageService.OnSaraVisualizationAvailable(message);

        if (updatedPlantData.CLOEAnalysis is not null)
        {
            await workflowService.TriggerCLOE(
                updatedPlantData.InspectionId,
                updatedPlantData.CLOEAnalysis
            );
        }

        if (updatedPlantData.FencillaAnalysis is not null)
        {
            await workflowService.TriggerFencilla(
                updatedPlantData.InspectionId,
                updatedPlantData.FencillaAnalysis
            );
        }

        return Ok(updatedPlantData);
    }
}
