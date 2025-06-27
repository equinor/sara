using api.Controllers.Models;
using api.Database.Models;
using api.MQTT;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

public class WorkflowStartedNotification
{
    public required string InspectionId { get; set; }
    public required string WorkflowName { get; set; }
}

public class AnonymizerDoneNotification
{
    public required string InspectionId { get; set; }
}

public class ConstantLevelOilerDoneNotification
{
    public required string InspectionId { get; set; }
    public required float OilLevel { get; set; }
}

public class FencillaDoneNotification
{
    public required string InspectionId { get; set; }
    public required bool IsBreak { get; set; }
    public required float Confidence { get; set; }
}

public class WorkflowExitedNotification
{
    public required string InspectionId { get; set; }
    public required string WorkflowStatus { get; set; }
    public required string WorkflowFailures { get; set; }
}

[ApiController]
[Route("[controller]")]
public class WorkflowsController(
    ILogger<WorkflowsController> logger,
    IPlantDataService plantDataService,
    IMqttMessageService mqttMessageService,
    TimeseriesServiceUploadOilLevel omniaUploadService
) : ControllerBase
{
    /// <summary>
    /// Updates status of plant data to started
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("notify-workflow-started")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> WorkflowStarted(
        [FromBody] WorkflowStartedNotification notification
    )
    {
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
    /// Updates status of plant data to anonymized success or failure
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("notify-anonymizer-done")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> AnonymizerDone(
        [FromBody] AnonymizerDoneNotification notification
    )
    {
        // TODO: Update plantData with information that the Anonymizer is Done
        logger.LogInformation(
            "Completed anonymization of plantData with inspection id {id}",
            notification.InspectionId
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
            StorageAccount = plantData.AnonymizedBlobStorageLocation.StorageAccount,
            BlobContainer = plantData.AnonymizedBlobStorageLocation.BlobContainer,
            BlobName = plantData.AnonymizedBlobStorageLocation.BlobName,
        };

        mqttMessageService.OnSaraVisualizationAvailable(message);

        var updatedPlantData = await plantDataService.UpdateAnonymizerWorkflowStatus(
            notification.InspectionId,
            WorkflowStatus.ExitSuccess
        );

        return Ok(updatedPlantData);
    }

    /// <summary>
    /// Registers CLO oil level on plant data and uploads to Omnia Timeseries.
    /// </summary>
    [HttpPut("notify-constant-level-oiler-done")]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> ConstantLevelOilerDone(
        [FromBody] ConstantLevelOilerDoneNotification notification
    )
    {
        logger.LogInformation(
            "Completed Constant Level Oiler analysis for plantData with inspection id {id} and oil level {oilLevel}",
            notification.InspectionId,
            notification.OilLevel
        );

        var ok = await omniaUploadService.UploadCLODataAsync(notification);
        if (!ok)
            return NotFound(
                $"Could not find plantData with inspection id {notification.InspectionId}"
            );

        // Should workflow status be updated here or other place?

        // this is not really updated now
        var updated = await plantDataService.ReadByInspectionId(notification.InspectionId);
        return Ok(updated);
    }

    /// <summary>
    /// TODO: Register fencilla results on plant data
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("notify-fencilla-done")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PlantDataResponse> FencillaDone(
        [FromBody] FencillaDoneNotification notification
    )
    {
        // TODO: Update plantData with information that Fencilla is Done
        logger.LogInformation(
            "Completed Fencilla analysis for plantData with inspection id {id} and break found is {IsBreak} with confidence {Confidence}",
            notification.InspectionId,
            notification.IsBreak,
            notification.Confidence
        );

        return Ok();
    }

    /// <summary>
    /// Updates status of the workflow to exit with success or failure
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("notify-workflow-exited")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PlantDataResponse> WorkflowExited(
        [FromBody] WorkflowExitedNotification notification
    )
    {
        // WorkflowStatus status;

        // if (notification.WorkflowStatus == "Succeeded")
        // {
        //     status = WorkflowStatus.ExitSuccess;
        // }
        // else
        // {
        //     logger.LogWarning(
        //         "Workflow failed with status {status} and failures {failures}",
        //         notification.WorkflowStatus,
        //         notification.WorkflowFailures
        //     );
        //     status = WorkflowStatus.ExitFailure;
        // }

        // var updatedPlantData = await plantDataService.UpdateAnonymizerWorkflowStatus(
        //     notification.InspectionId,
        //     status
        // );

        // TODO Add a new field in PlantData to hold WorkflowStatus and update this here
        // instead of updateing AnonymizerWorkflowStatus

        return Ok();
    }
}
