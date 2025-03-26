using api.Controllers.Models;
using api.Database;
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

public class WorkflowExitedNotification
{
    public required string InspectionId { get; set; }
    public required string WorkflowStatus { get; set; }
}

[ApiController]
[Route("[controller]")]
public class WorkflowsController(
    IPlantDataService plantDataService,
    IMqttMessageService mqttMessageService
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
    /// Updates status of plant data to exit with success or failure
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("notify-workflow-exited")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> WorkflowExited(
        [FromBody] WorkflowExitedNotification notification
    )
    {
        WorkflowStatus status;

        if (notification.WorkflowStatus == "Succeeded")
        {
            status = WorkflowStatus.ExitSuccess;
        }
        else
        {
            status = WorkflowStatus.ExitFailure;
        }

        var updatedPlantData = await plantDataService.UpdateAnonymizerWorkflowStatus(
            notification.InspectionId,
            status
        );
        if (updatedPlantData == null)
        {
            return NotFound(
                $"Could not find workflow with inspection id {notification.InspectionId}"
            );
        }

        var message = new IdaVisualizationAvailableMessage
        {
            InspectionId = notification.InspectionId,
            StorageAccount = updatedPlantData.AnonymizedBlobStorageLocation.StorageAccount,
            BlobContainer = updatedPlantData.AnonymizedBlobStorageLocation.BlobContainer,
            BlobName = updatedPlantData.AnonymizedBlobStorageLocation.BlobName,
        };

        mqttMessageService.OnIdaVisualizationAvailable(message);

        return Ok(updatedPlantData);
    }
}
