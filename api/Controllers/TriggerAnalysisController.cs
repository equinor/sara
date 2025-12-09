using api.Controllers.Models;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("[controller]")]
public class TriggerAnalysisController(
    IArgoWorkflowService argoWorkflowService,
    IPlantDataService plantDataService,
    ILogger<TriggerAnalysisController> logger
) : ControllerBase
{
    private readonly ILogger<TriggerAnalysisController> _logger = logger;

    /// <summary>
    /// Trigger the anonymization workflow for existing PlantData entry, by PlantData ID.
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
            "Triggering anonymization workflow from controller for InspectionId: {InspectionId}",
            plantData.InspectionId
        );

        if (
            plantData.Anonymization.Status == WorkflowStatus.Started
            || plantData.Anonymization.Status == WorkflowStatus.ExitSuccess
        )
        {
            return Conflict("Anonymization has already been triggered and will not run again.");
        }

        await argoWorkflowService.TriggerAnonymizer(
            plantData.InspectionId,
            plantData.Anonymization
        );

        return Ok("Anonymization workflow triggered successfully.");
    }
}
