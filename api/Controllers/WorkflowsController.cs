using api.Controllers.Models;
using api.Services;
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
public class WorkflowsController(ILogger<InspectionDataController> logger, IInspectionDataService inspectionDataService) : ControllerBase
{

    /// <summary>
    /// Get Inspection by id from data database
    /// </summary>
    /// <remarks>
    /// <para> This query gets inspection data by id</para>
    /// </remarks>
    [HttpPut]
    [Route("notify-workflow-started")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<InspectionDataResponse>> WorkflowStarted([FromBody] WorkflowStartedNotification notification)
    {
        var updatedInspectionData = await inspectionDataService.UpdateAnonymizerWorkflowStatus(notification.InspectionId, WorkflowStatus.Started);
        if (updatedInspectionData == null)
        {
            return NotFound();
        }
        return Ok(updatedInspectionData);
    }

    [HttpPut]
    [Route("notify-workflow-exited")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<InspectionDataResponse>> WorkflowExited([FromBody] WorkflowExitedNotification notification)
    {

        WorkflowStatus status;

        if (notification.WorkflowStatus == "Succeded") // TODO: Check that this is what Argo Workflows actually returns for workflow success flag
        {
            status = WorkflowStatus.ExitSuccess;
        }
        else
        {
            status = WorkflowStatus.ExitFailure;
        }

        var updatedInspectionData = await inspectionDataService.UpdateAnonymizerWorkflowStatus(notification.InspectionId, status);
        if (updatedInspectionData == null)
        {
            return NotFound();
        }
        return Ok(updatedInspectionData);
    }
}
