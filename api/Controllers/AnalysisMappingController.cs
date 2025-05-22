using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using api.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("[controller]")]
public class AnalysisMappingController(
    ILogger<AnalysisMappingController> logger,
    IAnalysisMappingService analysisMappingService
) : ControllerBase
{
    /// <summary>
    /// List all analysis mappings in the database
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(IList<AnalysisMapping>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IList<AnalysisMapping>>> GetAllTagAnalysis(
        [FromQuery] QueryParameters parameters
    )
    {
        PagedList<AnalysisMapping> analysisMappings;
        try
        {
            analysisMappings = await analysisMappingService.GetAnalysisMappings(parameters);
            return Ok(analysisMappings);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of analysis from database");
            throw;
        }
    }

    /// <summary>
    /// Get analysis mapping by id from data database
    /// </summary>
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
            var analysisMapping = await analysisMappingService.ReadById(id);
            if (analysisMapping == null)
            {
                return NotFound($"Could not find analysis mapping with id {id}");
            }
            return Ok(analysisMapping);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of analysis mapping from database");
            throw;
        }
    }

    /// <summary>
    /// Add a new analysis to an existing analysis mapping
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(AnalysisMapping), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisMapping>> CreateAnalysisMapping(
        [FromBody] string tagId,
        [FromBody] string inspectionDescription,
        [FromRoute] string? analysisType = null
    )
    {
        try
        {
            // var analysisMapping = await analysisMappingService.ReadById(analysisMappingId);
            // if (analysisMapping == null)
            // {
            //     return NotFound($"Could not find analysis mapping with id {analysisMappingId}");
            // }

            var analysisMapping = await analysisMappingService.CreateAnalysisMapping(
                tagId,
                inspectionDescription,
                Analysis.TypeFromString(analysisType)
            );

            return Ok(analysisMapping);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during creation of analysis mapping");
            throw;
        }
    }
}
