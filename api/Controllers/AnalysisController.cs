using api.Controllers.Models;
using api.Database;
using api.Services;
using api.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("[controller]")]
public class AnalysisController(
    ILogger<AnalysisController> logger,
    IAnalysisService analysisService
) : ControllerBase
{
    /// <summary>
    /// List all analysis in database
    /// </summary>
    /// <remarks>
    /// <para> This query gets all analysis </para>
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(IList<AnalysisResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IList<AnalysisResponse>>> GetAllAnalysis(
        [FromQuery] QueryParameters parameters
    )
    {
        PagedList<Analysis> analysis;
        try
        {
            analysis = await analysisService.GetAnalysis(parameters);
            var response = analysis.Select(analysis => new AnalysisResponse(analysis));
            return Ok(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of analysis from database");
            throw;
        }
    }

    /// <summary>
    /// Get Analysis by id from data database
    /// </summary>
    /// <remarks>
    /// <para> This query gets analysis by id</para>
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id}")]
    [ProducesResponseType(typeof(AnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisResponse>> GetAnalysisById([FromRoute] string id)
    {
        try
        {
            var analysis = await analysisService.ReadById(id);
            if (analysis == null)
            {
                return NotFound($"Could not find analysis with id {id}");
            }
            var response = new AnalysisResponse(analysis);
            return Ok(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of analysis from database");
            throw;
        }
    }
}
