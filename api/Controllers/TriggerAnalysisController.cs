using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("[controller]")]
public class TriggerAnalysisController(
    IArgoWorkflowService argoWorkflowService,
    IAnalysisMappingService analysisMappingService,
    IPlantDataService plantDataService,
    SaraDbContext dbContext,
    ILogger<TriggerAnalysisController> logger
) : ControllerBase
{
    private readonly SaraDbContext dbContext = dbContext;

    private readonly ILogger<TriggerAnalysisController> _logger = logger;

    /// <summary>
    /// Trigger the analysis workflow for existing PlantData entry, by PlantData ID.
    /// </summary>
    [HttpPost]
    [Route("trigger-analysis/{plantDataId}")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TriggerAnalysis([FromRoute] string plantDataId)
    {
        var plantData = await plantDataService.ReadById(plantDataId);
        if (plantData == null)
        {
            return NotFound($"Could not find plant data with id {plantDataId}");
        }

        _logger.LogInformation(
            "Triggering analysis workflow from controller for InspectionId: {InspectionId}",
            plantData.InspectionId
        );

        if (
            plantData.AnonymizerWorkflowStatus == WorkflowStatus.NotStarted
            || plantData.AnonymizerWorkflowStatus == WorkflowStatus.ExitFailure
        )
        {
            var analysesToBeRun =
                await analysisMappingService.GetAnalysisTypeFromInspectionDescriptionAndTag(
                    plantData.InspectionId,
                    plantData.InstallationCode
                );

            var shouldRunConstantLevelOiler = false;
            if (analysesToBeRun.Contains(AnalysisType.ConstantLevelOiler))
            {
                _logger.LogInformation(
                    "Analysis type ConstantLevelOiler is set to be run for InspectionId: {InspectionId}",
                    plantData.InspectionId
                );
                shouldRunConstantLevelOiler = true;
            }
            var shouldRunFencilla = false;
            if (analysesToBeRun.Contains(AnalysisType.Fencilla))
            {
                _logger.LogInformation(
                    "Analysis type Fencilla is set to be run for InspectionId: {InspectionId}",
                    plantData.InspectionId
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

        return Conflict("Analysis has already been triggered and will not run again.");
    }
}
