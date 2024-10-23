using api.Models;
using api.Controllers.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using api.Utilities;

namespace api.Controllers;

[ApiController]
[Route("[controller]")]
public class InspectionDataController(
    ILogger<InspectionDataController> logger,
    IInspectionDataService inspectionDataService) : ControllerBase
{
    /// <summary>
    /// List all inspection data database
    /// </summary>
    /// <remarks>
    /// <para> This query gets all inspection data </para>
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(IList<InspectionDataResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IList<InspectionDataResponse>>> GetAllInspectionData([FromQuery] QueryParameters parameters)
    {
        PagedList<InspectionData> inspectionData;
        try
        {
            inspectionData = await inspectionDataService.GetInspectionData(parameters);
            var response = inspectionData.Select(inspection => new InspectionDataResponse(inspection));
            return Ok(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of inspectionData from database");
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
            var inspectionData = await inspectionDataService.ReadById(id);
            if (inspectionData == null)
            {
                return NotFound($"Could not find inspection data with id {id}");
            }
            var response = new InspectionDataResponse(inspectionData);
            return Ok(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of inspectionData from database");
            throw;
        }
    }

    /// <summary>
    /// Get Inspection by inspection id from data database
    /// </summary>
    /// <remarks>
    /// <para> This query gets inspection data by inspection id</para>
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("{inspectionId}")]
    [ProducesResponseType(typeof(InspectionDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InspectionDataResponse>> GetInspectionDataByInspectionId([FromRoute] string inspectionId)
    {
        try
        {
            var inspectionData = await inspectionDataService.ReadByInspectionId(inspectionId);
            if (inspectionData == null)
            {
                return NotFound($"Could not find inspection data with inspection id {inspectionId}");
            }
            var response = new InspectionDataResponse(inspectionData);
            return Ok(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of inspectionData from database");
            throw;
        }
    }

    /// <summary>
    /// Get image file from blob store
    /// </summary>
    /// <remarks>
    /// <para> This endpoint serves an image file from the blob store </para>
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("image/{inspectionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadUriFromInspectionId([FromRoute] string inspectionId)
    {
        try
        {
            var inspection = await inspectionDataService.ReadByInspectionId(inspectionId);
            if (inspection == null)
            {
                return NotFound($"Could not find inspection data with inspection id {inspectionId}");
            }

            var downloadUri = inspection.RawDataUri;

            if (downloadUri == null || !Uri.IsWellFormedUriString(downloadUri.ToString(), UriKind.Absolute))
            {
                return NotFound($"Could not find uri for inspection {inspectionId}");
            }

            return Ok(downloadUri);
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
