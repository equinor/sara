using api.Controllers.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("feedback")]
public class FeedbackController(
    ILogger<FeedbackController> logger,
    IFeedbackService feedbackService
) : ControllerBase
{
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
