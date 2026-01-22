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
    [Route("trigger-anonymizer/{plantDataId}")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TriggerAnonymizer([FromRoute] string plantDataId)
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

    /// <summary>
    /// Trigger the analysis workflow for existing PlantData entry, by PlantData ID.
    /// </summary>
    [HttpPost]
    [Route("trigger-analysis/{plantDataId}")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TriggerAnalysis([FromRoute] string plantDataId)
    {
        try
        {
            var plantData = await plantDataService.ReadById(plantDataId);
            if (plantData == null)
            {
                return NotFound($"Could not find plant data with id {plantDataId}");
            }

            _logger.LogInformation(
                "Triggering analysis workflows from controller for InspectionId: {InspectionId}",
                plantData.InspectionId
            );

            if (plantData.Anonymization?.Status == WorkflowStatus.NotStarted)
            {
                await argoWorkflowService.TriggerAnonymizer(plantDataId, plantData.Anonymization);
                return Ok(
                    "Triggering anonymization workflow which will trigger analysis workflows."
                );
            }

            if (plantData.Anonymization?.Status == WorkflowStatus.Started)
            {
                return Conflict(
                    "Anonymization is still in progress. Analysis workflows will be triggered once it completes."
                );
            }

            if (plantData.Anonymization?.Status == WorkflowStatus.ExitFailure)
            {
                return Conflict("Cannot trigger analysis workflows because anonymization failed.");
            }

            var analysesToRun = new List<string>();
            if (plantData.CLOEAnalysis?.Status == WorkflowStatus.NotStarted)
            {
                await argoWorkflowService.TriggerCLOE(
                    plantData.InspectionId,
                    plantData.CLOEAnalysis
                );
                analysesToRun.Add("CLOE analysis");
            }
            if (plantData.FencillaAnalysis?.Status == WorkflowStatus.NotStarted)
            {
                await argoWorkflowService.TriggerFencilla(
                    plantData.InspectionId,
                    plantData.FencillaAnalysis
                );
                analysesToRun.Add("Fencilla analysis");
            }

            if (
                plantData.ThermalReadingAnalysis?.Status == WorkflowStatus.NotStarted
                && plantData.Tag != null
                && plantData.InspectionDescription != null
            )
            {
                await argoWorkflowService.TriggerThermalReading(
                    plantData.InspectionId,
                    plantData.Tag,
                    plantData.InspectionDescription,
                    plantData.InstallationCode,
                    plantData.ThermalReadingAnalysis
                );
                analysesToRun.Add("Thermal Reading analysis");
            }
            if (analysesToRun.Count == 0)
            {
                return Ok(
                    $"No analysis workflows configured for plant data with Id {plantDataId}."
                );
            }
            return Ok($"Triggered analysis workflows: {string.Join(", ", analysesToRun)}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(
                ex,
                "Plant data not found for PlantDataId: {PlantDataId}",
                plantDataId
            );
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error occurred while triggering analysis workflows for PlantDataId: {PlantDataId}",
                plantDataId
            );
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred."
            );
        }
    }
}
