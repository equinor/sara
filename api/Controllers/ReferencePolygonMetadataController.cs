using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using api.Utilities;
using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

public class CreateFromInspectionRecordRequest
{
    public required Guid InspectionRecordId { get; set; }
    public required string TagId { get; set; }
    public required string InstallationCode { get; set; }
    public required string InspectionDescription { get; set; }
    public required List<ImageCoordinate> Polygon { get; set; }
}

[ApiController]
[Route("[controller]")]
public class ReferencePolygonMetadataController(
    ILogger<ReferencePolygonMetadataController> logger,
    IReferencePolygonMetadataService referencePolygonMetadataService,
    IThermalImageService thermalImageService,
    IInspectionRecordService inspectionRecordService,
    IConfiguration configuration
) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(IList<ReferencePolygonMetadata>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IList<ReferencePolygonMetadata>>> GetReferencePolygonMetadatas()
    {
        try
        {
            var referencePolygonMetadatas =
                await referencePolygonMetadataService.GetReferencePolygonMetadatas();
            return Ok(referencePolygonMetadatas);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during GET of thermal reference metadata");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while retrieving thermal reference metadata"
            );
        }
    }

    [HttpGet("id/{id}")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(ReferencePolygonMetadata), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReferencePolygonMetadata>> GetReferencePolygonMetadataById(
        [FromRoute] Guid id
    )
    {
        try
        {
            var referencePolygonMetadata = await referencePolygonMetadataService.ReadById(id);
            if (referencePolygonMetadata is null)
            {
                return NotFound($"Could not find thermal reference metadata with id {id}");
            }

            return Ok(referencePolygonMetadata);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during GET of thermal reference metadata by id");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while retrieving the thermal reference metadata"
            );
        }
    }

    [HttpPost]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(ReferencePolygonMetadata), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReferencePolygonMetadata>> CreateReferencePolygonMetadata(
        [FromBody] ReferencePolygonMetadataInput input
    )
    {
        input.InstallationCode = Sanitize.SanitizeUserInput(input.InstallationCode);
        input.TagId = Sanitize.SanitizeUserInput(input.TagId);
        input.InspectionDescription = Sanitize.SanitizeUserInput(input.InspectionDescription);

        try
        {
            var imageLocation = BuildReferenceLocations(input.ReferenceBlobStorageDirectory);
            var referencePolygonMetadata =
                await referencePolygonMetadataService.CreateReferencePolygonMetadata(
                    input,
                    imageLocation
                );
            return Ok(referencePolygonMetadata);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Conflicting thermal reference metadata create request");
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during creation of thermal reference metadata");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while creating the thermal reference metadata"
            );
        }
    }

    [HttpPost("from-inspection-record")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(ReferencePolygonMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ReferencePolygonMetadata>> CreateFromInspectionRecord(
        [FromBody] CreateFromInspectionRecordRequest request
    )
    {
        request.TagId = Sanitize.SanitizeUserInput(request.TagId);
        request.InstallationCode = Sanitize.SanitizeUserInput(request.InstallationCode);
        request.InspectionDescription = Sanitize.SanitizeUserInput(request.InspectionDescription);

        const int MaxPolygonVertices = 100;
        if (request.Polygon.Count is < 3 or > MaxPolygonVertices)
        {
            return BadRequest($"Polygon must have between 3 and {MaxPolygonVertices} vertices.");
        }
        if (
            request.Polygon.Any(vertex =>
                vertex is null || !double.IsFinite(vertex.X) || !double.IsFinite(vertex.Y)
            )
        )
        {
            return BadRequest("Each polygon vertex must have exactly two finite coordinates.");
        }

        try
        {
            var record = await inspectionRecordService.ReadById(request.InspectionRecordId);
            if (record is null)
            {
                return NotFound(
                    $"Could not find inspection record with id {request.InspectionRecordId}"
                );
            }

            var referencePolygonMetadata =
                await referencePolygonMetadataService.CreateFromInspectionRecord(
                    record,
                    request.TagId,
                    request.InstallationCode,
                    request.InspectionDescription,
                    request.Polygon
                );
            return Ok(referencePolygonMetadata);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "Resource not found during create from inspection record");
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(
                ex,
                "Conflicting thermal reference metadata create from inspection record"
            );
            return Conflict(ex.Message);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning(ex, "Blob not found during create from inspection record");
            return NotFound("The source blob could not be found in storage");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error during creation of thermal reference metadata from inspection record"
            );
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while creating the thermal reference metadata from inspection record"
            );
        }
    }

    [HttpPut("id/{id}")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(ReferencePolygonMetadata), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReferencePolygonMetadata>> UpdateReferencePolygonMetadata(
        [FromRoute] Guid id,
        [FromBody] ReferencePolygonMetadataInput input
    )
    {
        try
        {
            var imageLocation = BuildReferenceLocations(input.ReferenceBlobStorageDirectory);
            var referencePolygonMetadata =
                await referencePolygonMetadataService.UpdateReferencePolygonMetadata(
                    id,
                    input,
                    imageLocation
                );
            return Ok(referencePolygonMetadata);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "Thermal reference metadata not found during update");
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Conflicting thermal reference metadata update request");
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during update of thermal reference metadata");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while updating the thermal reference metadata"
            );
        }
    }

    [HttpDelete("id/{id}")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DeleteReferencePolygonMetadata([FromRoute] Guid id)
    {
        try
        {
            await referencePolygonMetadataService.RemoveReferencePolygonMetadata(id);
            return Ok("Thermal reference metadata removed successfully");
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "Thermal reference metadata not found during delete");
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during removal of thermal reference metadata");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while removing the thermal reference metadata"
            );
        }
    }

    [HttpGet("id/{id}/image")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetReferencePolygonImage([FromRoute] Guid id)
    {
        try
        {
            var metadata = await referencePolygonMetadataService.ReadById(id);
            if (metadata is null)
            {
                return NotFound($"Could not find thermal reference metadata with id {id}");
            }

            var result = await thermalImageService.GetThermalImageDataAsync(
                metadata.ReferenceImageBlobStorageLocation
            );

            Response.Headers["X-Image-Width"] = result.Width.ToString();
            Response.Headers["X-Image-Height"] = result.Height.ToString();
            Response.Headers["X-Temperature-Min"] = result.MinTemperature.ToString(
                "G9",
                System.Globalization.CultureInfo.InvariantCulture
            );
            Response.Headers["X-Temperature-Max"] = result.MaxTemperature.ToString(
                "G9",
                System.Globalization.CultureInfo.InvariantCulture
            );
            Response.Headers.Append(
                "Access-Control-Expose-Headers",
                "X-Image-Width, X-Image-Height, X-Temperature-Min, X-Temperature-Max"
            );

            return File(result.FloatBytes, "application/octet-stream");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning(ex, "Reference image blob not found for id {Id}", id);
            return NotFound("The reference image blob could not be found in storage");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating thermal reference image for id {Id}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while generating the thermal reference image"
            );
        }
    }

    private BlobStorageLocation BuildReferenceLocations(BlobDirectoryInput directoryInput)
    {
        var storageAccount =
            configuration["Storage:ThermalReferenceStorageAccount"]
            ?? throw new InvalidOperationException(
                "Storage:ThermalReferenceStorageAccount is not configured"
            );

        var imageLocation = new BlobStorageLocation
        {
            StorageAccount = storageAccount,
            BlobContainer = directoryInput.BlobContainer,
            BlobName = $"{directoryInput.BlobName}/reference_image.tiff",
        };

        return imageLocation;
    }
}
