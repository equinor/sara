using System.Text.Json;
using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using api.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("[controller]")]
public class PlantDataController(
    ILogger<PlantDataController> logger,
    IPlantDataService plantDataService
) : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// List all plant data from database
    /// </summary>
    /// <remarks>
    /// <para> This query gets all plant data </para>
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(IList<PlantData>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IList<PlantData>>> GetAllPlantData(
        [FromQuery] QueryParameters parameters
    )
    {
        PagedList<PlantData> plantData;
        try
        {
            plantData = await plantDataService.GetPlantData(parameters);
            return Ok(plantData);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of plantData from database");
            throw;
        }
    }

    [HttpPost]
    [Route("createEntry")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> CreatePlantDataEntry([FromBody] PlantDataRequest request)
    {
        if (
            string.IsNullOrWhiteSpace(request.InspectionId)
            || string.IsNullOrWhiteSpace(request.InstallationCode)
        )
        {
            return BadRequest("Missing required fields.");
        }

        try
        {
            var plantData = await plantDataService.CreatePlantDataEntry(request);
            if (plantData == null)
            {
                return StatusCode(500);
            }
            return CreatedAtAction(nameof(GetPlantDataById), new { id = plantData.Id }, plantData);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during POST of plantData to database");
            throw;
        }
    }

    /// <summary>
    /// Get PlantData by id from database
    /// </summary>
    /// <remarks>
    /// <para> This query gets plant data by id</para>
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id}")]
    [ProducesResponseType(typeof(PlantData), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PlantData>> GetPlantDataById([FromRoute] string id)
    {
        try
        {
            var plantData = await plantDataService.ReadById(id);
            if (plantData == null)
            {
                return NotFound($"Could not find plant data with id {id}");
            }
            return Ok(plantData);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of plantData from database");
            throw;
        }
    }

    /// <summary>
    /// Get PlantData by inspection id from data database
    /// </summary>
    /// <remarks>
    /// <para> This query gets plant data by inspection id</para>
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("{inspectionId}")]
    [ProducesResponseType(typeof(PlantData), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PlantDataResponse>> GetPlantDataByInspectionId(
        [FromRoute] string inspectionId
    )
    {
        try
        {
            var plantData = await plantDataService.ReadByInspectionId(inspectionId);
            if (plantData == null)
            {
                return NotFound($"Could not find plant data with inspection id {inspectionId}");
            }
            return Ok(plantData);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of plantData from database");
            throw;
        }
    }

    /// <summary>
    /// Get link to plant data from blob store by inspection id
    /// </summary>
    /// <remarks>
    /// <para> This endpoint returns a link to an anonymized plant data in blob storage. </para>
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("{inspectionId}/inspection-data-storage-location")]
    [ProducesResponseType(typeof(BlobStorageLocation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BlobStorageLocation>> DownloadUriFromInspectionId(
        [FromRoute] string inspectionId
    )
    {
        try
        {
            var plantData = await plantDataService.ReadByInspectionId(inspectionId);
            if (plantData == null)
            {
                logger.LogWarning(
                    "No plant data found for InspectionId: {InspectionId}",
                    inspectionId
                );
                return NotFound($"Could not find plant data with inspection id {inspectionId}");
            }

            var anonymizerWorkflowStatus = plantData.AnonymizerWorkflowStatus;
            logger.LogInformation(
                "Anonymization workflow status for InspectionId: {InspectionId} is {Status}",
                inspectionId,
                anonymizerWorkflowStatus
            );

            switch (anonymizerWorkflowStatus)
            {
                case WorkflowStatus.ExitSuccess:
                    var plantDataJson = JsonSerializer.Serialize(plantData, _jsonSerializerOptions);
                    logger.LogInformation(
                        "Full Plant Data for InspectionId: {InspectionId}: {PlantData}",
                        inspectionId,
                        plantDataJson
                    );
                    return Ok(plantData.AnonymizedBlobStorageLocation);

                case WorkflowStatus.NotStarted:
                    return StatusCode(
                        StatusCodes.Status202Accepted,
                        "Anonymization workflow has not started."
                    );

                case WorkflowStatus.Started:
                    return StatusCode(
                        StatusCodes.Status202Accepted,
                        "Anonymization workflow is in progress."
                    );

                case WorkflowStatus.ExitFailure:
                    return StatusCode(
                        StatusCodes.Status422UnprocessableEntity,
                        "Anonymization workflow failed."
                    );

                default:
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Unknown workflow status."
                    );
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of image from blob store");
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    // private static string GetContentType(string fileName)
    // {
    //     var extension = Path.GetExtension(fileName).ToLowerInvariant();
    //     return extension switch
    //     {
    //         ".jpg" => "image/jpeg",
    //         ".jpeg" => "image/jpeg",
    //         ".png" => "image/png",
    //         ".gif" => "image/gif",
    //         _ => "application/octet-stream",
    //     };
    // }
}
