using api.Controllers.Models;
using api.Database;
using api.Services;
using api.MQTT;
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
public class WorkflowsController(IInspectionDataService inspectionDataService, IMqttMessageService mqttMessageService) : ControllerBase
{
    /// <summary>
    /// Updates status of inspection data to started
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("notify-workflow-started")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InspectionDataResponse>> WorkflowStarted(
        [FromBody] WorkflowStartedNotification notification
    )
    {
        var updatedInspectionData = await inspectionDataService.UpdateAnonymizerWorkflowStatus(
            notification.InspectionId,
            WorkflowStatus.Started
        );
        if (updatedInspectionData == null)
        {
            return NotFound(
                $"Could not find workflow with inspection id {notification.InspectionId}"
            );
        }
        return Ok(updatedInspectionData);
    }

    /// <summary>
    /// Updates status of inspection data to exit with success or failure
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("notify-workflow-exited")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InspectionDataResponse>> WorkflowExited(
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

        var updatedInspectionData = await inspectionDataService.UpdateAnonymizerWorkflowStatus(
            notification.InspectionId,
            status
        );
        if (updatedInspectionData == null)
        {
            return NotFound(
                $"Could not find workflow with inspection id {notification.InspectionId}"
            );
        }

        var message = new IdaVisualizationAvailableMessage
        {
            InspectionId = notification.InspectionId,
            StorageAccount = updatedInspectionData.AnonymizedBlobStorageLocation.StorageAccount,
            BlobContainer = updatedInspectionData.AnonymizedBlobStorageLocation.BlobContainer,
            BlobName = updatedInspectionData.AnonymizedBlobStorageLocation.BlobName
        };

        mqttMessageService.OnIdaVisualizationAvailable(message);

        return Ok(updatedInspectionData);
    }
}
