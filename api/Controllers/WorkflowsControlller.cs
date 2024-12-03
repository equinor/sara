using api.Controllers.Models;
using api.Services;
using api.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using api.MQTT;
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
public class WorkflowsController(IInspectionDataService inspectionDataService, MqttService mqttService) : ControllerBase
{
    private readonly IInspectionDataService _inspectionDataService = inspectionDataService;
    private readonly MqttService _mqttService = mqttService;

    /// <summary>
    /// Updates status of inspection data to started
    /// </summary>
    [HttpPut]
    [AllowAnonymous] // TODO: Implement role for notifying and machine-to-machine oauth
    [Route("notify-workflow-started")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InspectionDataResponse>> WorkflowStarted([FromBody] WorkflowStartedNotification notification)
    {
        var updatedInspectionData = await _inspectionDataService.UpdateAnonymizerWorkflowStatus(notification.InspectionId, WorkflowStatus.Started);
        if (updatedInspectionData == null)
        {
            return NotFound($"Could not find workflow with inspection id {notification.InspectionId}");
        }
        return Ok(updatedInspectionData);
    }

    /// <summary>
    /// Updates status of inspection data to exit with success or failure
    /// </summary>
    [HttpPut]
    [AllowAnonymous] // TODO: Implement role for notifying and machine-to-machine oauth
    [Route("notify-workflow-exited")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InspectionDataResponse>> WorkflowExited([FromBody] WorkflowExitedNotification notification)
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

        var updatedInspectionData = await _inspectionDataService.UpdateAnonymizerWorkflowStatus(notification.InspectionId, status);
        if (updatedInspectionData == null)
        {
            return NotFound($"Could not find workflow with inspection id {notification.InspectionId}");
        }

        var mqttTopic = $"workflow/{notification.InspectionId}/status_success";
        var mqttMessage = new
        {
            notification.InspectionId,
            Status = notification.WorkflowStatus, // "Succeeded"" for success, else fail
            Timestamp = DateTime.UtcNow
        };

        await _mqttService.PublishMessageAsync(mqttTopic, mqttMessage);
        return Ok(updatedInspectionData);
    }
}
