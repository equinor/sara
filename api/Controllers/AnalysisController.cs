using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("analysis")]
public class AnalysisController(
    ILogger<AnalysisController> logger,
    IAnalysisService analysisService,
    IAnalysisTriggerService analysisTriggerService,
    IBlobStorageService blobStorageService
) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(PagedResponse<AnalysisDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<AnalysisDto>>> GetAll(
        [FromQuery] AnalysisParameters parameters
    )
    {
        try
        {
            var page = await analysisService.GetAnalyses(parameters);
            var pageDtos = page.Select((p) => new AnalysisDto(p, blobStorageService)).ToList();
            return Ok(
                new PagedResponse<AnalysisDto>
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
            logger.LogError(e, "Error during GET of analyses");
            throw;
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id:guid}")]
    [ProducesResponseType(typeof(Analysis), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisDto>> GetById([FromRoute] Guid id)
    {
        try
        {
            var analysis = await analysisService.ReadById(id);
            if (analysis is null)
            {
                return NotFound($"Could not find analysis with id {id}");
            }
            var analysisDto = new AnalysisDto(analysis, blobStorageService);
            return Ok(analysisDto);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of analysis by id");
            throw;
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("run/{runId:guid}")]
    [ProducesResponseType(typeof(AnalysisRun), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisRun>> GetRunById([FromRoute] Guid runId)
    {
        try
        {
            var run = await analysisService.ReadRunById(runId);
            if (run is null)
            {
                return NotFound($"Could not find analysis run with id {runId}");
            }
            return Ok(run);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of analysis run by id");
            throw;
        }
    }

    [HttpPost]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id:guid}/rerun")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Rerun([FromRoute] Guid id)
    {
        try
        {
            await analysisTriggerService.RerunAnalysis(id);
            return Accepted();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
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
            await analysisService.Delete(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("available")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetAvailableAnalyses()
    {
        try
        {
            var availableAnalyses = await analysisService.GetAvailableAnalyses();
            return Ok(availableAnalyses);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of available analyses");
            throw;
        }
    }
}
