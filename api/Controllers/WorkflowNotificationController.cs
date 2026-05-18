using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Controllers;

/// <summary>
/// Notification endpoints for reporting workflow lifecycle events. Routes are
/// keyed by <see cref="Workflow.Id"/> so concurrent workflows of the same type
/// (reruns, grouped analyses) can be addressed unambiguously.
/// </summary>
[ApiController]
[Route("workflow")]
public class WorkflowNotificationController(
    ILogger<WorkflowNotificationController> logger,
    SaraDbContext context,
    IWorkflowService workflowService
) : ControllerBase
{
    /// <summary>
    /// Notify that the workflow has started executing.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("{workflowId:guid}/started")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> WorkflowStarted([FromRoute] Guid workflowId)
    {
        var workflow = await context.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId);
        if (workflow is null)
        {
            return NotFound($"Workflow {workflowId} not found");
        }

        logger.LogInformation(
            "Workflow {WorkflowType} (Id: {WorkflowId}) reported started",
            workflow.WorkflowType,
            workflow.Id
        );

        workflow.Status = WorkflowStatus.InProgress;
        workflow.StartedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Receive the workflow's result payload. The body is stored verbatim on the
    /// <see cref="Workflow"/> row and deserialized later by the per-workflow
    /// result handler.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("{workflowId:guid}/result")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> WorkflowResult(
        [FromRoute] Guid workflowId,
        [FromBody] WorkflowResultNotification notification
    )
    {
        var workflow = await context.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId);
        if (workflow is null)
        {
            return NotFound($"Workflow {workflowId} not found");
        }

        logger.LogInformation(
            "Workflow {WorkflowType} (Id: {WorkflowId}) reported result ({Length} bytes)",
            workflow.WorkflowType,
            workflow.Id,
            notification.ResultJson?.Length ?? 0
        );

        workflow.ResultJson = notification.ResultJson;
        await context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Notify that the workflow has exited. Marks the workflow as
    /// <see cref="WorkflowStatus.Succeeded"/> or <see cref="WorkflowStatus.Failed"/>,
    /// then dispatches result handlers and advances the analysis run via
    /// <see cref="IWorkflowService.OnWorkflowCompleted"/>.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("{workflowId:guid}/exited")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> WorkflowExited(
        [FromRoute] Guid workflowId,
        [FromBody] WorkflowExitedNotification notification
    )
    {
        var workflow = await context.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId);
        if (workflow is null)
        {
            return NotFound($"Workflow {workflowId} not found");
        }

        var terminalStatus = notification.ExitStatus switch
        {
            WorkflowExitStatus.Succeeded => WorkflowStatus.Succeeded,
            _ => WorkflowStatus.Failed,
        };

        logger.LogInformation(
            "Workflow {WorkflowType} (Id: {WorkflowId}) reported exit: {ExitStatus} -> {TerminalStatus}",
            workflow.WorkflowType,
            workflow.Id,
            notification.ExitStatus,
            terminalStatus
        );

        workflow.Status = terminalStatus;
        workflow.CompletedAt = DateTime.UtcNow;
        if (terminalStatus == WorkflowStatus.Failed)
        {
            workflow.ErrorMessage = notification.ErrorMessage;
        }
        await context.SaveChangesAsync();

        await workflowService.OnWorkflowCompleted(workflow.Id);

        return NoContent();
    }
}

public class WorkflowResultNotification
{
    public string? ResultJson { get; set; }
}

public enum WorkflowExitStatus
{
    Succeeded,
    Failed,
}

public class WorkflowExitedNotification
{
    public required WorkflowExitStatus ExitStatus { get; set; }
    public string? ErrorMessage { get; set; }
}
