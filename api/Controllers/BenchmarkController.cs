using api.Controllers.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("benchmark")]
[Authorize(Policy = InstallationAccess.AdminPolicy)]
public class BenchmarkController(IBenchmarkService benchmarkService) : ControllerBase
{
    private const string DatasetImageCacheControl = "private, max-age=600";

    [HttpGet("datasets")]
    [ProducesResponseType(typeof(IReadOnlyList<BenchmarkDatasetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BenchmarkDatasetDto>>> GetDatasets(
        CancellationToken cancellationToken
    ) => Ok(await benchmarkService.GetDatasetsAsync(cancellationToken));

    [HttpGet("jobs")]
    [ProducesResponseType(typeof(IReadOnlyList<BenchmarkJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BenchmarkJobDto>>> GetJobs(
        CancellationToken cancellationToken
    ) => Ok(await benchmarkService.GetJobsAsync(cancellationToken));

    [HttpPost("runs")]
    [ProducesResponseType(typeof(BenchmarkJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BenchmarkJobDto>> TriggerRun(
        [FromBody] TriggerBenchmarkRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var job = await benchmarkService.TriggerBenchmarkAsync(request, cancellationToken);
            return Accepted(job);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("results")]
    [ProducesResponseType(typeof(IReadOnlyList<BenchmarkResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<BenchmarkResultDto>>> GetResults(
        [FromQuery] string datasetId,
        [FromQuery] string version,
        [FromQuery] string split,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Ok(
                await benchmarkService.GetResultsAsync(datasetId, version, split, cancellationToken)
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("results/{runId}")]
    [ProducesResponseType(typeof(BenchmarkRunDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BenchmarkRunDetailDto>> GetRunDetail(
        [FromRoute] string runId,
        [FromQuery] string datasetId,
        [FromQuery] string version,
        [FromQuery] string split,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Ok(
                await benchmarkService.GetRunDetailAsync(
                    datasetId,
                    version,
                    split,
                    runId,
                    cancellationToken
                )
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("results/{runId}/images/{imageId:int}")]
    [ProducesResponseType(typeof(BenchmarkImageComparisonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BenchmarkImageComparisonDto>> GetImageComparison(
        [FromRoute] string runId,
        [FromRoute] int imageId,
        [FromQuery] string datasetId,
        [FromQuery] string version,
        [FromQuery] string split,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return Ok(
                await benchmarkService.GetImageComparisonAsync(
                    datasetId,
                    version,
                    split,
                    runId,
                    imageId,
                    cancellationToken
                )
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("results/{runId}/images/{imageId:int}/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDatasetImage(
        [FromRoute] string runId,
        [FromRoute] int imageId,
        [FromQuery] string datasetId,
        [FromQuery] string version,
        [FromQuery] string split,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var image = await benchmarkService.GetDatasetImageAsync(
                datasetId,
                version,
                split,
                imageId,
                cancellationToken
            );
            Response.Headers.CacheControl = DatasetImageCacheControl;
            return File(image.Content, image.ContentType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
