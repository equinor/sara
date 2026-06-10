using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using api.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("inspection-record")]
public class InspectionRecordController(
    ILogger<InspectionRecordController> logger,
    IInspectionRecordService inspectionRecordService
) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(PagedResponse<InspectionRecord>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResponse<InspectionRecord>>> GetAll(
        [FromQuery] InspectionRecordParameters parameters
    )
    {
        try
        {
            var page = await inspectionRecordService.GetInspectionRecords(parameters);
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
    public async Task<ActionResult<InspectionRecord>> GetById([FromRoute] Guid id)
    {
        try
        {
            var record = await inspectionRecordService.ReadById(id);
            if (record is null)
            {
                return NotFound($"Could not find inspection record with id {id}");
            }
            return Ok(record);
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
    public async Task<ActionResult<InspectionRecord>> GetByInspectionId(
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
            return Ok(record);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during GET of inspection record by inspection id");
            throw;
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("inspection-id/{inspectionId}/anonymized-location")]
    [ProducesResponseType(typeof(BlobStorageLocation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BlobStorageLocation>> GetAnonymizedLocation(
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

            var anonymizer = record
                .Analyses.SelectMany(a => a.Runs)
                .SelectMany(r => r.Workflows)
                .Where(w => w.WorkflowType.Equals("anonymizer", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(w => w.CompletedAt ?? w.StartedAt ?? DateTime.MinValue)
                .FirstOrDefault();

            if (anonymizer is null)
            {
                return NotFound($"No anonymizer workflow found for inspection id {inspectionId}");
            }

            return anonymizer.Status switch
            {
                WorkflowStatus.Succeeded when anonymizer.OutputBlobStorageLocation is not null =>
                    Ok(anonymizer.OutputBlobStorageLocation),
                WorkflowStatus.Succeeded => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Anonymizer workflow succeeded but produced no output blob location."
                ),
                WorkflowStatus.Pending => StatusCode(
                    StatusCodes.Status202Accepted,
                    "Anonymizer workflow has not started."
                ),
                WorkflowStatus.InProgress => StatusCode(
                    StatusCodes.Status202Accepted,
                    "Anonymizer workflow is in progress."
                ),
                WorkflowStatus.Failed => StatusCode(
                    StatusCodes.Status422UnprocessableEntity,
                    "Anonymizer workflow failed."
                ),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Unknown anonymizer workflow status."
                ),
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error fetching anonymized location for inspection id");
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
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
