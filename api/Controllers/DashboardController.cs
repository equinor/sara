using api.Configurations;
using api.Controllers.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace api.Controllers;

[ApiController]
[Route("dashboard")]
public class DashboardController(
    ILogger<DashboardController> logger,
    IDashboardService service,
    IOptions<DashboardOptions> options
) : ControllerBase
{
    private readonly DashboardOptions _options = options.Value;

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary([FromQuery] int sinceHours = 24)
    {
        if (!_options.AllowedWindowHours.Contains(sinceHours))
        {
            return BadRequest(
                $"sinceHours must be one of: {string.Join(", ", _options.AllowedWindowHours)}"
            );
        }

        try
        {
            return Ok(await service.GetSummary(sinceHours));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error building dashboard summary");
            throw;
        }
    }

    [HttpGet]
    [Authorize(Roles = Role.Any)]
    [Route("trend-details")]
    [ProducesResponseType(typeof(TrendBucketDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TrendBucketDetailsDto>> GetTrendDetails(
        [FromQuery] DateTime bucketStart,
        [FromQuery] int windowHours = 24
    )
    {
        if (!_options.AllowedWindowHours.Contains(windowHours))
        {
            return BadRequest(
                $"windowHours must be one of: {string.Join(", ", _options.AllowedWindowHours)}"
            );
        }

        try
        {
            return Ok(await service.GetTrendBucketDetails(bucketStart, windowHours));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error building dashboard trend bucket details");
            throw;
        }
    }
}
