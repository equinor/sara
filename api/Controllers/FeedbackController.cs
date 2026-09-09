using api.Configurations;
using api.Controllers.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace api.Controllers;

[ApiController]
[Route("feedback")]
public class FeedbackController(
    ILogger<FeedbackController> logger,
    IFeedbackService feedbackService,
    IOptions<DashboardOptions> dashboardOptions
) : ControllerBase
{
    private readonly DashboardOptions _dashboardOptions = dashboardOptions.Value;

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(typeof(PagedResponse<FeedbackHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<FeedbackHistoryDto>>> GetAll(
        [FromQuery] FeedbackParameters parameters
    )
    {
        if (
            parameters.StartedSince is { } startedSince
            && parameters.StartedUntil is { } startedUntil
            && startedSince > startedUntil
        )
            return BadRequest("StartedSince must be earlier than or equal to StartedUntil");

        var page = await feedbackService.GetAll(parameters);
        return Ok(
            new PagedResponse<FeedbackHistoryDto>
            {
                Items = page,
                PageNumber = page.CurrentPage,
                PageSize = page.PageSize,
                TotalCount = page.TotalCount,
                TotalPages = page.TotalPages,
            }
        );
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("summary")]
    [ProducesResponseType(typeof(FeedbackSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeedbackSummaryDto>> GetSummary(
        [FromQuery] int sinceHours = 168,
        [FromQuery] string timeZone = "UTC",
        [FromQuery] string? analysisType = null
    )
    {
        if (!_dashboardOptions.AllowedWindowHours.Contains(sinceHours))
            return BadRequest(
                $"sinceHours must be one of: {string.Join(", ", _dashboardOptions.AllowedWindowHours)}"
            );

        TimeZoneInfo parsedTimeZone;
        try
        {
            parsedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return BadRequest($"Unknown time zone '{timeZone}'");
        }
        catch (InvalidTimeZoneException)
        {
            return BadRequest($"Invalid time zone '{timeZone}'");
        }

        return Ok(await feedbackService.GetSummary(sinceHours, parsedTimeZone, analysisType));
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("trend-details")]
    [ProducesResponseType(typeof(FeedbackTrendBucketDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeedbackTrendBucketDetailsDto>> GetTrendDetails(
        [FromQuery] DateTime bucketStart,
        [FromQuery] int windowHours,
        [FromQuery] string timeZone = "UTC",
        [FromQuery] string? analysisType = null
    )
    {
        if (!_dashboardOptions.AllowedWindowHours.Contains(windowHours))
            return BadRequest(
                $"windowHours must be one of: {string.Join(", ", _dashboardOptions.AllowedWindowHours)}"
            );

        TimeZoneInfo parsedTimeZone;
        try
        {
            parsedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return BadRequest($"Unknown time zone '{timeZone}'");
        }
        catch (InvalidTimeZoneException)
        {
            return BadRequest($"Invalid time zone '{timeZone}'");
        }

        return Ok(
            await feedbackService.GetTrendBucketDetails(
                bucketStart,
                windowHours,
                parsedTimeZone,
                analysisType
            )
        );
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("analysis-run/{runId:guid}")]
    [ProducesResponseType(typeof(FeedbackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedbackDto>> GetByRunId([FromRoute] Guid runId)
    {
        var feedback = await feedbackService.GetByRunId(runId);
        if (feedback is null)
            return NotFound($"No feedback found for analysis run with id {runId}");

        return Ok(new FeedbackDto(feedback));
    }

    [HttpPut]
    [Authorize(Roles = Role.User)]
    [Route("analysis-run/{runId:guid}")]
    [ProducesResponseType(typeof(FeedbackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedbackDto>> Upsert(
        [FromRoute] Guid runId,
        [FromBody] UpsertFeedbackRequest request
    )
    {
        try
        {
            var feedback = await feedbackService.Upsert(runId, request);
            return Ok(new FeedbackDto(feedback));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error upserting feedback for analysis run {RunId}", runId);
            throw;
        }
    }

    [HttpDelete]
    [Authorize(Roles = Role.User)]
    [Route("analysis-run/{runId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid runId)
    {
        try
        {
            await feedbackService.Delete(runId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
