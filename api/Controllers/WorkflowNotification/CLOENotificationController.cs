using api.Controllers.Models;
using api.Database.Models;
using api.MQTT;
using api.Services;
using api.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers.WorkflowNotification;

[ApiController]
[Route("workflow-notification/constant-level-oiler-estimator")]
public class CLOEWorkflowNotificationController(
    ILogger<CLOEWorkflowNotificationController> logger,
    IPlantDataService plantDataService,
    IArgoWorkflowService workflowService,
    ITimeseriesService timeseriesService,
    IMqttPublisherService mqttPublisherService
) : ControllerBase
{
    /// <summary>
    /// Notify that the CLOE workflow has started
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("started")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlantDataResponse>> CLOEStarted(
        [FromBody] WorkflowStartedNotification notification
    )
    {
        var inspectionId = Sanitize.SanitizeUserInput(notification.InspectionId);
        logger.LogDebug(
            "Received notification that the CLOE workflow has started for inspection id {inspectionId}",
            inspectionId
        );

        PlantData updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateCLOEWorkflowStatus(
                inspectionId,
                WorkflowStatus.Started
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating CLOE workflow status");
            return BadRequest(ex.Message);
        }

        return Ok(updatedPlantData);
    }

    /// <summary>
    /// Notify about the result of the CLOE workflow
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("result")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PlantDataResponse>> CLOEResult(
        [FromBody] CLOEWorkflowResultNotification notification
    )
    {
        logger.LogDebug(
            "Received notification with result from the CLOE workflow with inspection id {id}. OilLevel: {oilLevel}. Confidence: {confidence}",
            notification.InspectionId,
            notification.OilLevel,
            notification.Confidence
        );

        PlantData updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateCLOEResult(
                notification.InspectionId,
                notification.OilLevel,
                notification.Confidence
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating CLOE result");
            return BadRequest(ex.Message);
        }

        var uploadRequest = new TriggerTimeseriesUploadRequest
        {
            Name =
                $"{updatedPlantData.InstallationCode}_{updatedPlantData.Tag}_{updatedPlantData.InspectionDescription}",
            Facility = updatedPlantData.InstallationCode,
            ExternalId = "",
            Description = "CLOE-oil-level",
            Unit = "percentage",
            AssetId = updatedPlantData.InstallationCode,
            Value = notification.OilLevel,
            Timestamp = updatedPlantData.Timestamp ?? DateTime.UtcNow,
            Step = true,
            Metadata = new Dictionary<string, string>
            {
                { "Confidence", notification.Confidence.ToString() },
            },
        };
        await timeseriesService.TriggerTimeseriesUpload(uploadRequest);

        return Ok(updatedPlantData);
    }

    /// <summary>
    /// Notify that the CLOE workflow has exited with success or failure
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Role.WorkflowStatusWrite)]
    [Route("exited")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlantDataResponse>> CLOEExited(
        [FromBody] WorkflowExitedNotification notification
    )
    {
        var workflowStatus = workflowService.GetWorkflowStatus(notification, "CLOE");

        PlantData updatedPlantData;
        try
        {
            updatedPlantData = await plantDataService.UpdateCLOEWorkflowStatus(
                notification.InspectionId,
                workflowStatus
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error occurred while updating CLOE workflow status");
            return BadRequest(ex.Message);
        }

        var cloeAnalysis =
            updatedPlantData.CLOEAnalysis
            ?? throw new InvalidOperationException(
                $"CLOE analysis is not set up for plant data with inspection id {notification.InspectionId}"
            );

        const float confidenceThreshold = 0.3F;
        const float lowOilLevelThreshold = 0.05F;

        string? warning = null;
        if (
            cloeAnalysis.OilLevel < lowOilLevelThreshold
            && cloeAnalysis.Confidence >= confidenceThreshold
        )
        {
            warning = "Oil Level is below 5%";
        }

        string? value = null;
        if (cloeAnalysis.Confidence >= confidenceThreshold)
        {
            value = (cloeAnalysis.OilLevel * 100).ToString();
        }

        var message = new SaraAnalysisResultMessage
        {
            InspectionId = updatedPlantData.InspectionId,
            AnalysisType = nameof(AnalysisType.ConstantLevelOiler),
            Value = value,
            Unit = "percentage",
            Confidence = cloeAnalysis.Confidence * 100,
            Warning = warning,
            StorageAccount = cloeAnalysis.DestinationBlobStorageLocation.StorageAccount,
            BlobContainer = cloeAnalysis.DestinationBlobStorageLocation.BlobContainer,
            BlobName = cloeAnalysis.DestinationBlobStorageLocation.BlobName,
        };

        await mqttPublisherService.PublishSaraAnalysisResultAvailable(message);

        return Ok(updatedPlantData);
    }
}
