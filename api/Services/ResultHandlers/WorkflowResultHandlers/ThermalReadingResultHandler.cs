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
        var records = await InspectionRecordResolver.GetInspectionRecords(context, workflow);

        if (records.Count == 0)
        {
            logger.LogWarning(
                "Workflow {WorkflowType} (Id: {WorkflowId}) has no resolvable InspectionRecord — skipping result handling",
                workflow.WorkflowType,
                workflow.Id
            );
            return;
        }

        if (records.Count > 1)
        {
            logger.LogWarning(
                "Per-record handler '{HandlerType}' invoked on group analysis ({Count} records) — "
                    + "skipping publish. A group-aware IWorkflowResultHandler must be registered for "
                    + "'{WorkflowType}' if it runs on group analyses.",
                nameof(ThermalReadingResultHandler),
                records.Count,
                workflow.WorkflowType
            );
            return;
        }

        var inspectionRecord = records[0];
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
            Value = result?.Temperature.ToString("F2"),
            Unit = "celsius [temperature]",
            Confidence = result?.Confidence is null ? null : result.Confidence * 100,
            Warning = result?.Warning,
        };

        if (workflow.OutputBlobStorageLocation is { } output)
        {
            message.StorageAccount = output.StorageAccount;
            message.BlobContainer = output.BlobContainer;
            message.BlobName = output.BlobName;
        }

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
