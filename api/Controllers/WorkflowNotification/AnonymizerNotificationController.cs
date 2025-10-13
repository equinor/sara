using api.Controllers.Models;
using api.Database.Models;
using api.MQTT;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers.WorkflowNotification;

public class AnonymizerResult
{
    public required bool IsAnonymized { get; set; }
}

[ApiController]
[Route("workflow-notification/anonymizer")]
public class AnonymizerWorkflowNotificationController(
    ILogger<AnonymizerWorkflowNotificationController> logger,
    IPlantDataService plantDataService,
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> AnonymizerStarted(
        [FromBody] WorkflowStartedNotification notification
    )
    {
        logger.LogDebug(
            "Received notification that the anonymizer workflow has started for inspection id {inspectionId}",
            notification.InspectionId
        );

        var updatedPlantData = await plantDataService.UpdateAnonymizerWorkflowStatus(
            notification.InspectionId,
            WorkflowStatus.Started
        );
        if (updatedPlantData == null)
        {
            return NotFound(
                $"Could not find workflow with inspection id {notification.InspectionId}"
            );
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> AnonymizerResult(
        [FromBody] WorkflowResultNotification<AnonymizerResult> notification
    )
    {
        logger.LogDebug(
            "Received notification with result from the anonymizer workflow with inspection id {id}. IsAnonymized: {isAnonymized}",
            notification.InspectionId,
            notification.Result.IsAnonymized
        );

        var plantData = await plantDataService.ReadByInspectionId(notification.InspectionId);
        if (plantData == null)
        {
            return NotFound(
                $"Could not find plantData with inspection id {notification.InspectionId}"
            );
        }

        var message = new SaraVisualizationAvailableMessage
        {
            InspectionId = notification.InspectionId,
            StorageAccount = plantData.Anonymization.AnonymizedBlobStorageLocation.StorageAccount,
            BlobContainer = plantData.Anonymization.AnonymizedBlobStorageLocation.BlobContainer,
            BlobName = plantData.Anonymization.AnonymizedBlobStorageLocation.BlobName,
        };

        mqttMessageService.OnSaraVisualizationAvailable(message);

        // TODO: Update plantData with information that the Anonymizer is Done
        var updatedPlantData = await plantDataService.UpdateAnonymizerWorkflowStatus(
            notification.InspectionId,
            WorkflowStatus.ExitSuccess
        );

        return Ok(updatedPlantData);
    }

    /// <summary>
    /// Notify that the anonymizer workflow has exited with success or failure
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("exited")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> AnonymizerExited(
        [FromBody] WorkflowExitedNotification notification
    )
    {
        logger.LogInformation(
            "Received notification that the anonymizer workflow has started for inspection id {id} with workflow status: {status} and failures: {failures}",
            notification.InspectionId,
            notification.WorkflowStatus,
            notification.WorkflowFailures
        );

        var updatedPlantData = await plantDataService.UpdateAnonymizerWorkflowStatus(
            notification.InspectionId,
            WorkflowStatus.ExitSuccess
        );
        if (updatedPlantData == null)
        {
            return NotFound(
                $"Could not find workflow with inspection id {notification.InspectionId}"
            );
        }

        // Trigger the analysis on the PlantData

        return Ok(updatedPlantData);
    }
}
