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
    IAnalysisRunService service,
    IBlobStorageService blobService
) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(PagedResponse<AnalysisRun>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AnalysisRun>>> GetAll(
        [FromQuery] AnalysisRunParameters parameters
    )
    {
        if (
            parameters.StartedSince is { } startedSince
            && parameters.StartedUntil is { } startedUntil
            && startedSince > startedUntil
        )
        {
            return BadRequest("StartedSince must be earlier than or equal to StartedUntil");
        }

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
    [ProducesResponseType(typeof(AnalysisRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisRunDto>> GetById([FromRoute] Guid id)
    {
        try
        {
            var run = await service.ReadById(id);
            if (run is null)
            {
                return NotFound($"Could not find analysis run with id {id}");
            }
            return Ok(new AnalysisRunDto(run, blobService));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of analysis run by id");
            throw;
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
