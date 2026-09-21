using api.Controllers.Models;
using api.Database.Models;
using api.Services.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("alarms")]
public class AlarmController(ILogger<AlarmController> logger, IAlarmService alarmService)
    : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("active")]
    [ProducesResponseType(typeof(List<AnalysisResultValueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<AnalysisResultValueDto>>> GetActive(
        [FromQuery] ActiveAlarmParameters parameters
    )
    {
        if (parameters.PageNumber < 1)
            return BadRequest("PageNumber must be at least 1");

        if (parameters.PageSize is < 1 or > 200)
            return BadRequest("PageSize must be between 1 and 200");

        try
        {
            var alarms = await alarmService.GetActiveAlarms(parameters);
            return Ok(alarms.Select(alarm => new AnalysisResultValueDto(alarm)).ToList());
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error reading active alarms");
            throw;
        }
    }

    [HttpPost]
    [Authorize(Roles = Role.User)]
    [Route("{resultValueId}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Acknowledge([FromRoute] Guid resultValueId)
    {
        var acknowledged = await alarmService.Acknowledge(resultValueId);

        if (!acknowledged)
            return NotFound($"No analysis result value with id {resultValueId}");

        return NoContent();
    }
}
