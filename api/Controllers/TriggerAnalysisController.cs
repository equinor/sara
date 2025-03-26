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
    IAnalysisMappingService analysisMappingService,
    IdaDbContext dbContext,
    ILogger<TriggerAnalysisController> logger
) : ControllerBase
{
    private readonly IdaDbContext dbContext = dbContext;

    private readonly ILogger<TriggerAnalysisController> _logger = logger;

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
            Id = Guid.NewGuid().ToString(),
            InspectionId = request.InspectionId,
            InstallationCode = request.InstallationCode,
            RawDataBlobStorageLocation = request.RawDataBlobStorageLocation,
            AnonymizedBlobStorageLocation = request.AnonymizedBlobStorageLocation,
            VisualizedBlobStorageLocation = request.VisualizedBlobStorageLocation,
            DateCreated = DateTime.UtcNow,
            AnonymizerWorkflowStatus = WorkflowStatus.NotStarted,
            AnalysisToBeRun = [],
            Analysis = [],
        };

        dbContext.PlantData.Add(plantData);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Triggering analysis workflow from controller for InspectionId: {InspectionId}",
            request.InspectionId
        );

        var analysesToBeRun =
            await analysisMappingService.GetAnalysisTypeFromInspectionDescriptionAndTag(
                request.InspectionId,
                request.InstallationCode
            );

        var shouldRunConstantLevelOiler = false;
        if (analysesToBeRun.Contains(AnalysisType.ConstantLevelOiler))
        {
            shouldRunConstantLevelOiler = true;
        }
        await argoWorkflowService.TriggerAnalysis(plantData, shouldRunConstantLevelOiler);

        return Ok("Analysis workflow triggered successfully.");
    }
}
