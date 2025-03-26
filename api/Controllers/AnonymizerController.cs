using api.Controllers.Models;
using api.Database;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

public class TriggerAnonymizerRequest
{
    public required string InspectionId { get; set; }
    public required BlobStorageLocation RawDataBlobStorageLocation { get; set; }
    public required BlobStorageLocation AnonymizedBlobStorageLocation { get; set; }
    public required string InstallationCode { get; set; }
}

[ApiController]
[Route("[controller]")]
public class AnonymizerController(IAnonymizerService anonymizerService, IdaDbContext dbContext)
    : ControllerBase
{
    private readonly IdaDbContext dbContext = dbContext;

    /// <summary>
    /// Triggers the anonymizer workflow. NB: Anonymizer workflow should normally be triggered by MQTT message
    /// </summary>
    [HttpPost]
    [Route("trigger-anonymizer")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerAnonymizer([FromBody] TriggerAnonymizerRequest request)
    {
        var plantData = new PlantData
        {
            Id = Guid.NewGuid().ToString(),
            InspectionId = request.InspectionId,
            InstallationCode = request.InstallationCode,
            RawDataBlobStorageLocation = request.RawDataBlobStorageLocation,
            AnonymizedBlobStorageLocation = request.AnonymizedBlobStorageLocation,
            DateCreated = DateTime.UtcNow,
            AnonymizerWorkflowStatus = WorkflowStatus.NotStarted,
            AnalysisToBeRun = [],
            Analysis = [],
        };

        dbContext.PlantData.Add(plantData);
        await dbContext.SaveChangesAsync();

        await anonymizerService.TriggerAnonymizerFunc(plantData);

        return Ok("Anonymizer workflow triggered successfully.");
    }
}
