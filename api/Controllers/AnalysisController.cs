using api.Models;
using api.Controllers.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using api.Utilities;

namespace api.Controllers;

[ApiController]
[Route("[controller]")]
public class AnalysisController(ILogger<AnalysisController> logger, IAnalysisService analysisService) : ControllerBase
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
    public async Task<ActionResult<IList<AnalysisResponse>>> GetAllInspectionData([FromQuery] QueryParameters parameters)
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
    /// Get Inspection by id from data database
    /// </summary>
    /// <remarks>
    /// <para> This query gets inspection data by id</para>
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id}")]
    [ProducesResponseType(typeof(InspectionDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InspectionDataResponse>> GetInspectionDataById([FromRoute] string id)
    {
        try
        {
            var analysis = await analysisService.ReadById(id);
            if (analysis == null)
            {
                return NotFound($"Could not find inspection data with id {id}");
            }
            var response = new AnalysisResponse(analysis);
            return Ok(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of inspectionData from database");
            throw;
        }
    }
}
