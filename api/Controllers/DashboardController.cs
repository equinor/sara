using api.Controllers.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("dashboard")]
public class DashboardController(ILogger<DashboardController> logger, IDashboardService service)
    : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = InstallationAccess.ObserverPolicy)]
    [Route("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(
        [FromQuery] int sinceHours = 24,
        [FromQuery] string timeZone = "UTC"
    )
    {
        if (
            !DashboardWindow.TryParse(
                sinceHours,
                nameof(sinceHours),
                timeZone,
                out var parsedTimeZone,
                out var error
            )
        )
            return BadRequest(error);

        try
        {
            return Ok(await service.GetSummary(sinceHours, parsedTimeZone));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error building dashboard summary");
            throw;
        }
    }

    [HttpGet]
    [Authorize(Policy = InstallationAccess.ObserverPolicy)]
    [Route("trend-details")]
    [ProducesResponseType(typeof(TrendBucketDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TrendBucketDetailsDto>> GetTrendDetails(
        [FromQuery] DateTime bucketStart,
        [FromQuery] int windowHours = 24,
        [FromQuery] string timeZone = "UTC"
    )
    {
        if (
            !DashboardWindow.TryParse(
                windowHours,
                nameof(windowHours),
                timeZone,
                out var parsedTimeZone,
                out var error
            )
        )
            return BadRequest(error);

        try
        {
            return Ok(
                await service.GetTrendBucketDetails(bucketStart, windowHours, parsedTimeZone)
            );
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error building dashboard trend bucket details");
            throw;
        }
    }
}
