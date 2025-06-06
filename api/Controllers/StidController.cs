using api.Controllers.Models;
using api.Database;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

public class TriggerStidRequest
{
    public required string InspectionId { get; set; }
    public required BlobStorageLocation AnonymizedBlobStorageLocation { get; set; }
    public required StidDocumentMetadata StidDocumentMetadata { get; set; }
}

[ApiController]
[Route("[controller]")]
public class StidController(IStidService stidService, IdaDbContext dbContext)
    : ControllerBase
{
    private readonly IdaDbContext dbContext = dbContext;

    /// <summary>
    /// Triggers the stid workflow. NB: Upload stid workflow should normally be triggered after anonymization.
    /// </summary>
    [HttpPost]
    [Route("trigger-sara-stid")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerStid([FromBody] TriggerStidRequest request)
    {
        var plantData = new PlantData
        {
            Id = Guid.NewGuid().ToString(),
            InspectionId = request.InspectionId,
            AnonymizedBlobStorageLocation = request.AnonymizedBlobStorageLocation,
            DateCreated = DateTime.UtcNow,
            AnonymizerWorkflowStatus = WorkflowStatus.NotStarted,
            AnalysisToBeRun = [],
            Analysis = [],
            StidDocumentMetadata = request.StidDocumentMetadata,
        };

        dbContext.PlantData.Add(plantData);
        await dbContext.SaveChangesAsync();

        await stidService.TriggerStidFunc(plantData);

        return Ok("Upload to stid workflow triggered successfully.");
    }
}
