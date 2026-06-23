using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using api.Utilities;
using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("inspection-record")]
public class InspectionRecordController(
    ILogger<InspectionRecordController> logger,
    IInspectionRecordService inspectionRecordService,
    IThermalImageService thermalImageService,
    IBlobStorageService blobStorageService
) : ControllerBase
{
    // Workflow types whose output forms the visualization base layer for an
    // inspection (anonymized image or raw passthrough). Newest succeeded one wins.
    private static readonly HashSet<string> VisualizationWorkflowTypes = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "anonymizer",
        "copy-raw-to-visualized",
    };

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(PagedResponse<InspectionRecord>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<InspectionRecordDto>>> GetAll(
        [FromQuery] InspectionRecordParameters parameters
    )
    {
        try
        {
            var page = await inspectionRecordService.GetInspectionRecords(parameters);

            var pageDtos = page.Select(
                    (record) => new InspectionRecordDto(record, blobStorageService)
                )
                .ToList();

            return Ok(
                new PagedResponse<InspectionRecordDto>
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
            logger.LogError(e, "Error during GET of inspection records");
            throw;
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id:guid}")]
    [ProducesResponseType(typeof(InspectionRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InspectionRecordDto>> GetById([FromRoute] Guid id)
    {
        try
        {
            var record = await inspectionRecordService.ReadById(id);
            if (record is null)
            {
                return NotFound($"Could not find inspection record with id {id}");
            }
            var recordDto = new InspectionRecordDto(record, blobStorageService);
            return Ok(recordDto);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of inspection record by id");
            throw;
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("inspection-id/{inspectionId}")]
    [ProducesResponseType(typeof(InspectionRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InspectionRecordDto>> GetByInspectionId(
        [FromRoute] string inspectionId
    )
    {
        inspectionId = Sanitize.SanitizeUserInput(inspectionId);
        try
        {
            var record = await inspectionRecordService.ReadByInspectionId(inspectionId);
            if (record is null)
            {
                return NotFound(
                    $"Could not find inspection record with inspection id {inspectionId}"
                );
            }
            var recordDto = new InspectionRecordDto(record, blobStorageService);
            return Ok(recordDto);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of inspection record by inspection id");
            throw;
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("inspection-id/{inspectionId}/visualization-location")]
    [ProducesResponseType(typeof(BlobStorageLocation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BlobStorageLocation>> GetVisualizationLocation(
        [FromRoute] string inspectionId
    )
    {
        inspectionId = Sanitize.SanitizeUserInput(inspectionId);
        try
        {
            var record = await inspectionRecordService.ReadByInspectionId(inspectionId);
            if (record is null)
            {
                return NotFound(
                    $"Could not find inspection record with inspection id {inspectionId}"
                );
            }

            var visualizationWorkflow = record
                .Analyses.SelectMany(a => a.Runs)
                .SelectMany(r => r.Workflows)
                .Where(w => VisualizationWorkflowTypes.Contains(w.WorkflowType))
                .OrderByDescending(w => w.CompletedAt ?? w.StartedAt ?? DateTime.MinValue)
                .FirstOrDefault();

            if (visualizationWorkflow is null)
            {
                return NotFound(
                    $"No visualization workflow found for inspection id {inspectionId}"
                );
            }

            return visualizationWorkflow.Status switch
            {
                WorkflowStatus.Succeeded
                    when visualizationWorkflow.OutputBlobStorageLocation is not null => Ok(
                    visualizationWorkflow.OutputBlobStorageLocation
                ),
                WorkflowStatus.Succeeded => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Visualization workflow succeeded but produced no output blob location."
                ),
                WorkflowStatus.Pending => StatusCode(
                    StatusCodes.Status202Accepted,
                    "Visualization workflow has not started."
                ),
                WorkflowStatus.InProgress => StatusCode(
                    StatusCodes.Status202Accepted,
                    "Visualization workflow is in progress."
                ),
                WorkflowStatus.Failed => StatusCode(
                    StatusCodes.Status422UnprocessableEntity,
                    "Visualization workflow failed."
                ),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Unknown visualization workflow status."
                ),
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error fetching visualization location for inspection id");
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("thermal")]
    [ProducesResponseType(typeof(PagedResponse<InspectionRecord>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<InspectionRecord>>> GetThermalInspectionRecords(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20
    )
    {
        try
        {
            var page = await inspectionRecordService.GetThermalInspectionRecords(
                pageNumber,
                pageSize
            );
            return Ok(
                new PagedResponse<InspectionRecord>
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
            logger.LogError(e, "Error during GET of thermal inspection records");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while retrieving thermal inspection records"
            );
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("id/{id:guid}/thermal-image")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetThermalImage([FromRoute] Guid id)
    {
        try
        {
            var record = await inspectionRecordService.ReadById(id);
            if (record is null)
            {
                return NotFound($"Could not find inspection record with id {id}");
            }

            var thermalReadingWorkflow = record
                .Analyses.SelectMany(a => a.Runs)
                .SelectMany(r => r.Workflows)
                .Where(w =>
                    w.WorkflowType.Equals("thermal-reading", StringComparison.OrdinalIgnoreCase)
                )
                .OrderByDescending(w => w.CompletedAt ?? w.StartedAt ?? DateTime.MinValue)
                .FirstOrDefault();

            if (thermalReadingWorkflow is null)
            {
                return NotFound($"No thermal-reading workflow found for inspection record {id}");
            }

            if (
                thermalReadingWorkflow.InputBlobStorageLocations is null
                || thermalReadingWorkflow.InputBlobStorageLocations.Count == 0
            )
            {
                return NotFound(
                    $"Thermal-reading workflow has no input blob location for inspection record {id}"
                );
            }

            var preprocessedLocation = thermalReadingWorkflow.InputBlobStorageLocations[0];
            var result = await thermalImageService.GetThermalImageDataAsync(preprocessedLocation);

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
            logger.LogWarning(
                ex,
                "Preprocessed thermal image blob not found for inspection record {Id}",
                id
            );
            return NotFound("The preprocessed thermal image blob could not be found in storage");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error generating thermal image for inspection record {Id}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while generating the thermal image"
            );
        }
    }

    [HttpPost]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(InspectionRecord), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InspectionRecord>> Create(
        [FromBody] CreateInspectionRecordRequest request
    )
    {
        request.RequiredAnalysis = request.RequiredAnalysis is not null
            ? Sanitize.SanitizeUserInput(request.RequiredAnalysis)
            : null;
        if (request.AnalysisGroup is not null)
        {
            request.AnalysisGroup.AnalysisGroupId = Sanitize.SanitizeUserInput(
                request.AnalysisGroup.AnalysisGroupId
            );
            request.AnalysisGroup.AnalysisGroupAnalyses = Sanitize.SanitizeUserInput(
                request.AnalysisGroup.AnalysisGroupAnalyses
            );
        }

        try
        {
            var created = await inspectionRecordService.CreateAndTrigger(request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating inspection record");
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
            await inspectionRecordService.Delete(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
