using System.Text.Json;
using api.Controllers.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("[controller]")]
public class TimeSeriesDataController(
    ILogger<TimeSeriesDataController> logger,
    ITimeseriesService timeseriesService
) : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Fetches a single CO2 concentration from sara-timeseries
    /// </summary>
    /// <remarks>
    /// <para> This query gets a single CO2 concentration </para>
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = Role.Any)]
    [Route("CO2")]
    [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<double>> GetCO2ConcentrationFromSARATimeSeries(
        [FromBody] FetchCO2MeasurementRequest fetchRequest
    )
    {
        logger.LogInformation(
            "Received request to fetch CO2 concentration from Timeseries for Facility: {Facility}, TaskStartTime: {TaskStartTime}, TaskEndTime: {TaskEndTime}, InspectionName: {InspectionName}",
            fetchRequest.Facility,
            fetchRequest.TaskStartTime,
            fetchRequest.TaskEndTime,
            fetchRequest.InspectionName
        );
        var co2Value = await timeseriesService.FetchCO2ConcentrationFromTimeseries(fetchRequest);

        if (co2Value == null)
        {
            return NotFound("CO2 concentration not found for the given parameters.");
        }
        return Ok(co2Value);
    }
}
