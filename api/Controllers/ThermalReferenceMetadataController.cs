using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("[controller]")]
public class ThermalReferenceMetadataController(
    ILogger<ThermalReferenceMetadataController> logger,
    IThermalReferenceMetadataService thermalReferenceMetadataService,
    IConfiguration configuration
) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(IList<ThermalReferenceMetadata>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IList<ThermalReferenceMetadata>>> GetThermalReferenceMetadatas()
    {
        try
        {
            var thermalReferenceMetadatas =
                await thermalReferenceMetadataService.GetThermalReferenceMetadatas();
            return Ok(thermalReferenceMetadatas);
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
    [ProducesResponseType(typeof(ThermalReferenceMetadata), StatusCodes.Status200OK)]
    public async Task<ActionResult<ThermalReferenceMetadata>> GetThermalReferenceMetadataById(
        [FromRoute] Guid id
    )
    {
        try
        {
            var thermalReferenceMetadata = await thermalReferenceMetadataService.ReadById(id);
            if (thermalReferenceMetadata is null)
            {
                return NotFound($"Could not find thermal reference metadata with id {id}");
            }

            return Ok(thermalReferenceMetadata);
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
    [ProducesResponseType(typeof(ThermalReferenceMetadata), StatusCodes.Status200OK)]
    public async Task<ActionResult<ThermalReferenceMetadata>> CreateThermalReferenceMetadata(
        [FromBody] ThermalReferenceMetadataInput input
    )
    {
        try
        {
            var (imageLocation, polygonLocation) = BuildReferenceLocations(
                input.ReferenceBlobStorageDirectory
            );
            var thermalReferenceMetadata =
                await thermalReferenceMetadataService.CreateThermalReferenceMetadata(
                    input,
                    imageLocation,
                    polygonLocation
                );
            return Ok(thermalReferenceMetadata);
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

    [HttpPut("id/{id}")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(ThermalReferenceMetadata), StatusCodes.Status200OK)]
    public async Task<ActionResult<ThermalReferenceMetadata>> UpdateThermalReferenceMetadata(
        [FromRoute] Guid id,
        [FromBody] ThermalReferenceMetadataInput input
    )
    {
        try
        {
            var (imageLocation, polygonLocation) = BuildReferenceLocations(
                input.ReferenceBlobStorageDirectory
            );
            var thermalReferenceMetadata =
                await thermalReferenceMetadataService.UpdateThermalReferenceMetadata(
                    id,
                    input,
                    imageLocation,
                    polygonLocation
                );
            return Ok(thermalReferenceMetadata);
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
    public async Task<ActionResult> DeleteThermalReferenceMetadata([FromRoute] Guid id)
    {
        try
        {
            await thermalReferenceMetadataService.RemoveThermalReferenceMetadata(id);
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

    private (
        BlobStorageLocation imageLocation,
        BlobStorageLocation polygonLocation
    ) BuildReferenceLocations(BlobDirectoryInput directoryInput)
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

        var polygonLocation = new BlobStorageLocation
        {
            StorageAccount = storageAccount,
            BlobContainer = directoryInput.BlobContainer,
            BlobName = $"{directoryInput.BlobName}/reference_polygon.json",
        };

        return (imageLocation, polygonLocation);
    }
}
