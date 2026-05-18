using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("analysis-group")]
public class AnalysisGroupController(
    ILogger<AnalysisGroupController> logger,
    IAnalysisGroupService service
) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(PagedResponse<AnalysisGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AnalysisGroup>>> GetAll(
        [FromQuery] AnalysisGroupParameters parameters
    )
    {
        try
        {
            var page = await service.GetGroups(parameters);
            return Ok(
                new PagedResponse<AnalysisGroup>
                {
                    Items = page,
                    PageNumber = page.CurrentPage,
                    PageSize = page.PageSize,
                    TotalCount = page.TotalCount,
                    TotalPages = page.TotalPages,
                }
            );
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of analysis groups");
            throw;
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id:guid}")]
    [ProducesResponseType(typeof(AnalysisGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalysisGroup>> GetById([FromRoute] Guid id)
    {
        var group = await service.ReadById(id);
        if (group is null)
        {
            return NotFound($"Could not find analysis group with id {id}");
        }
        return Ok(group);
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
