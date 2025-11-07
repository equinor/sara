using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

public class TriggerAnalysisRequest
{
    public required string InspectionId { get; set; }
    public required BlobStorageLocation RawDataBlobStorageLocation { get; set; }
    public required BlobStorageLocation AnonymizedBlobStorageLocation { get; set; }
    public required BlobStorageLocation VisualizedBlobStorageLocation { get; set; }
    public required string InstallationCode { get; set; }
}

[ApiController]
[Route("[controller]")]
public class TriggerAnalysisController(
    IArgoWorkflowService argoWorkflowService,
    SaraDbContext dbContext
) : ControllerBase
{
    private readonly SaraDbContext dbContext = dbContext;

    /// <summary>
    /// Triggers the analysis workflow. NB: Analysis workflow should normally be triggered by MQTT message
    /// </summary>
    [HttpPost]
    [Route("trigger-analysis")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerAnalysis([FromBody] TriggerAnalysisRequest request)
    {
        var plantData = new PlantData
        {
            InspectionId = request.InspectionId,
            InstallationCode = request.InstallationCode,
            Anonymization = new Anonymization
            {
                SourceBlobStorageLocation = request.RawDataBlobStorageLocation,
                DestinationBlobStorageLocation = request.AnonymizedBlobStorageLocation,
            },
        };

        dbContext.PlantData.Add(plantData);
        await dbContext.SaveChangesAsync();

        await argoWorkflowService.TriggerAnonymizer(
            plantData.InspectionId,
            plantData.Anonymization
        );

        return Ok("Analysis workflow triggered successfully.");
    }
}
