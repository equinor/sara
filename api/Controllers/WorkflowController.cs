using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("workflow")]
public class WorkflowController(
    ILogger<WorkflowController> logger,
    IWorkflowService service,
    IBlobStorageService blobService
) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(PagedResponse<Workflow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<WorkflowDto>>> GetAll(
        [FromQuery] WorkflowParameters parameters
    )
    {
        try
        {
            var page = await service.GetWorkflows(parameters);
            var pageDtos = page.Select((p) => new WorkflowDto(p, blobService)).ToList();
            return Ok(
                new PagedResponse<WorkflowDto>
                {
                    Items = pageDtos,
                    PageNumber = page.CurrentPage,
                    PageSize = page.PageSize,
                    TotalCount = page.TotalCount,
                    TotalPages = page.TotalPages,
                }
            );
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of workflows");
            throw;
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id:guid}")]
    [ProducesResponseType(typeof(Workflow), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowDto>> GetById([FromRoute] Guid id)
    {
        var workflow = await service.ReadById(id);
        if (workflow is null)
        {
            return NotFound($"Could not find workflow with id {id}");
        }
        var workflowDto = new WorkflowDto(workflow, blobService);
        return Ok(workflowDto);
    }

    [HttpPost]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id:guid}/retry")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Retry([FromRoute] Guid id)
    {
        try
        {
            await service.RetryWorkflow(id);
            return Accepted();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        try
        {
            await service.Delete(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
