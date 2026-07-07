using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Utilities;

namespace api.Services.ResultHandlers.WorkflowResultHandlers;

internal sealed class ThermalReadingResult
{
    public float Temperature { get; set; }
    public float? Confidence { get; set; }
    public string? Warning { get; set; }
}

public class ThermalReadingResultHandler(
    SaraDbContext context,
    IMqttPublisherService mqttPublisherService,
    ITimeseriesService timeseriesService,
    ILogger<ThermalReadingResultHandler> logger
) : IWorkflowResultHandler
{
    public string WorkflowType => "thermal-reading";

    public async Task OnWorkflowCompleted(Workflow workflow)
    {
        var inspectionRecord = await InspectionRecordResolver.GetSingleInspectionRecordOrNull(
            context,
            workflow,
            nameof(ThermalReadingResultHandler),
            logger
        );

        if (inspectionRecord is null)
            return;

        var result = WorkflowResultHandlerHelpers.DeserializeResult<ThermalReadingResult>(
            workflow,
            logger
        );

        var message = new SaraAnalysisResultMessage
        {
            InspectionIds = [inspectionRecord.InspectionId],
            AnalysisGroupId = null,
            WorkflowId = workflow.Id,
            AnalysisRunId = workflow.AnalysisRunId,
            AnalysisId = workflow.AnalysisRun.AnalysisId,
            AnalysisType = workflow.WorkflowType,
        };

        await mqttPublisherService.PublishSaraAnalysisResultAvailable(message);

        if (result?.Confidence is not null && result.Confidence > 0.99)
        {
            await TryUploadTimeseries(workflow, inspectionRecord, result);
        }
    }

    private async Task TryUploadTimeseries(
        Workflow workflow,
        InspectionRecord inspectionRecord,
        ThermalReadingResult? result
    )
    {
        if (result is null)
        {
            logger.LogWarning(
                "Skipping thermal-reading timeseries upload for workflow {WorkflowId}: result is null",
                workflow.Id
            );
            return;
        }

        var uploadRequest = new TriggerTimeseriesUploadRequest
        {
            Name =
                $"{inspectionRecord.InstallationCode}_{inspectionRecord.Tag}_{inspectionRecord.InspectionDescription?.Replace(" ", "-")}",
            Facility = inspectionRecord.InstallationCode,
            ExternalId = "",
            Description = "ThermalReading",
            Unit = "°C",
            AssetId = inspectionRecord.InstallationCode,
            Value = result.Temperature,
            Timestamp = inspectionRecord.Timestamp ?? DateTime.UtcNow,
            Step = true,
            Metadata = new Dictionary<string, string>
            {
                { "tag_id", inspectionRecord.Tag ?? "" },
                { "inspection_description", inspectionRecord.InspectionDescription ?? "" },
                { "robot_name", inspectionRecord.RobotName ?? "" },
            },
        };

        try
        {
            await timeseriesService.TriggerTimeseriesUpload(uploadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to upload thermal-reading datapoint to Timeseries for workflow {WorkflowId}",
                workflow.Id
            );
        }
    }
}
