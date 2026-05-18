using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("analysis-run")]
public class AnalysisRunController(
    ILogger<AnalysisRunController> logger,
    IAnalysisRunService service
) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(PagedResponse<AnalysisRun>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AnalysisRun>>> GetAll(
        [FromQuery] AnalysisRunParameters parameters
    )
    {
        try
        {
            var page = await service.GetRuns(parameters);
            return Ok(
                new PagedResponse<AnalysisRun>
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
            logger.LogError(e, "Error during GET of analysis runs");
            throw;
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id:guid}")]
    [ProducesResponseType(typeof(AnalysisRun), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalysisRun>> GetById([FromRoute] Guid id)
    {
        var run = await service.ReadById(id);
        if (run is null)
        {
            return NotFound($"Could not find analysis run with id {id}");
        }
        return Ok(run);
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
