using api.Controllers.Models;
using api.Database.Context;
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
    IAnalysisMappingService analysisMappingService,
    IPlantDataService plantDataService,
    SaraDbContext context
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
        [FromQuery] AnalysisMappingParameters parameters
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
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while retrieving analysis mappings"
            );
        }
    }

    /// <summary>
    /// Get analysis mapping by id from data database
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id}")]
    [ProducesResponseType(typeof(AnalysisMapping), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisMapping>> GetAnalysisById([FromRoute] string id)
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
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while retrieving analysis mapping"
            );
        }
    }

    /// <summary>
    /// Gets or create a new analysis mapping for a given tag and inspection description.
    /// If the mapping already exists, it adds the analysis type to the existing mapping.
    /// </summary>
    [HttpPost]
    [Route("tag/{tagId}/inspection/{inspectionDescription}/analysisType/{analysisType}")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(AnalysisMapping), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisMapping>> AddOrCreateAnalysisMapping(
        [FromRoute] string tagId,
        [FromRoute] string inspectionDescription,
        [FromRoute] AnalysisType analysisType
    )
    {
        try
        {
            var analysisMapping = await analysisMappingService.ReadByInspectionDescriptionAndTag(
                inspectionDescription,
                tagId
            );
            if (analysisMapping == null)
            {
                analysisMapping = await analysisMappingService.CreateAnalysisMapping(
                    tagId,
                    inspectionDescription,
                    analysisType
                );
            }
            else
            {
                analysisMapping = await analysisMappingService.AddAnalysisTypeToMapping(
                    analysisMapping,
                    analysisType
                );
            }
            var plantData = await plantDataService.ReadByTagIdAndInspectionDescription(
                tagId,
                inspectionDescription
            );
            if (plantData != null)
            {
                foreach (var entry in plantData)
                {
                    entry.AnalysisToBeRun = analysisMapping.AnalysesToBeRun;
                    await plantDataService.UpdateAnonymizerWorkflowStatus(
                        entry.InspectionId,
                        WorkflowStatus.NotStarted
                    );
                    context.PlantData.Update(entry);
                }
            }

            return Ok(analysisMapping);
        }
        catch (ArgumentException)
        {
            logger.LogError("Analysis type already exists in analysis mapping");
            return BadRequest("Analysis type already exists in analysis mapping");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during creation of analysis mapping");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while creating the analysis mapping"
            );
        }
    }

    /// <summary>
    /// Add a new analysis type to an existing analysis mapping
    /// </summary>
    [HttpPatch]
    [Route("analysisMappingId/{analysisMappingId}/analysisType/{analysisType?}")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(AnalysisMapping), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisMapping>> AddAnalysisTypeToMapping(
        [FromRoute] string analysisMappingId,
        [FromRoute] string analysisType
    )
    {
        var analysisTypeEnum = Analysis.TypeFromString(analysisType);
        AnalysisMapping? analysisMapping;
        if (analysisTypeEnum == null)
        {
            return BadRequest("Invalid analysis type");
        }
        try
        {
            analysisMapping = await analysisMappingService.ReadById(analysisMappingId);
            if (analysisMapping == null)
            {
                return NotFound($"Could not find analysis mapping with id {analysisMappingId}");
            }
            analysisMapping = await analysisMappingService.AddAnalysisTypeToMapping(
                analysisMapping,
                analysisTypeEnum.Value
            );

            return Ok(analysisMapping);
        }
        catch (ArgumentException)
        {
            logger.LogError("Analysis type already exists in analysis mapping");
            return BadRequest("Analysis type already exists in analysis mapping");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during creation of analysis mapping");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while creating the analysis mapping"
            );
        }
    }

    /// <summary>
    /// Remove an analysis type from an existing analysis mapping
    /// </summary>
    [HttpDelete]
    [Route("analysisMappingId/{analysisMappingId}/analysisType/{analysisType?}")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(AnalysisMapping), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisMapping>> RemoveAnalysisFromMapping(
        [FromRoute] string analysisMappingId,
        [FromRoute] string analysisType
    )
    {
        try
        {
            var analysisTypeEnum = Analysis.TypeFromString(analysisType);
            if (analysisTypeEnum == null)
            {
                return BadRequest("Invalid analysis type");
            }

            var analysisMapping = await analysisMappingService.RemoveAnalysisTypeFromMapping(
                analysisMappingId,
                analysisTypeEnum.Value
            );

            return Ok(analysisMapping);
        }
        catch (ArgumentException e)
        {
            logger.LogError(e, "Analysis type does not exist in analysis mapping");
            return BadRequest("Analysis type does not exist in analysis mapping");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during removal of analysis type from mapping");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while removing the analysis type from the mapping"
            );
        }
    }

    /// <summary>
    /// Remove an analysis mapping
    /// </summary>
    [HttpDelete]
    [Route("analysisMappingId/{analysisMappingId}")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisMapping>> RemoveAnalysisFromMapping(
        [FromRoute] string analysisMappingId
    )
    {
        try
        {
            await analysisMappingService.RemoveAnalysisMapping(analysisMappingId);

            return Ok("Analysis mapping removed successfully");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during removal of analysis mapping");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while removing the analysis mapping"
            );
        }
    }
}
