using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Services;
using api.Services.Models;
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

public class TriggerStidUploadRequest(
    string inspectionId,
    BlobStorageLocation anonymizedBlobStorageLocation,
    string tagId,
    string description
)
{
    public string InspectionId { get; } = inspectionId;
    public BlobStorageLocation AnonymizedBlobStorageLocation { get; } =
        anonymizedBlobStorageLocation;
    public string TagId { get; } = tagId;
    public string Description { get; } = description;
}

[ApiController]
[Route("[controller]")]
public class TriggerAnalysisController(
    IArgoWorkflowService argoWorkflowService,
    IAnalysisMappingService analysisMappingService,
    IStidWorkflowService stidWorkflowService,
    SaraDbContext dbContext,
    ILogger<TriggerAnalysisController> logger
) : ControllerBase
{
    private readonly SaraDbContext dbContext = dbContext;

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
            _logger.LogInformation(
                "Analysis type ConstantLevelOiler is set to be run for InspectionId: {InspectionId}",
                request.InspectionId
            );
            shouldRunConstantLevelOiler = true;
        }
        var shouldRunFencilla = false;
        if (analysesToBeRun.Contains(AnalysisType.Fencilla))
        {
            _logger.LogInformation(
                "Analysis type Fencilla is set to be run for InspectionId: {InspectionId}",
                request.InspectionId
            );
            shouldRunFencilla = true;
        }
        await argoWorkflowService.TriggerAnalysis(
            plantData,
            shouldRunConstantLevelOiler,
            shouldRunFencilla
        );

        return Ok("Analysis workflow triggered successfully.");
    }

    /// <summary>
    /// Triggers the stid upload workflow. NB: STID upload workflow should normally be triggered by MQTT message
    /// </summary>
    [HttpPost]
    [Route("trigger-stid-upload")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerUploadToStid(
        [FromBody] TriggerStidUploadRequest request
    )
    {
        await stidWorkflowService.TriggerUploadToStid(
            new StidUploadMessage
            {
                InspectionId = request.InspectionId,
                AnonymizedBlobStorageLocation = request.AnonymizedBlobStorageLocation,
            }
        );

        return Ok("Upload to stid workflow triggered successfully.");
    }
}
