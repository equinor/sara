using System.Globalization;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Utilities;

namespace api.Services.ResultHandlers.WorkflowResultHandlers;

internal sealed class CLOEResult
{
    public float? OilLevel { get; set; }
    public float? Confidence { get; set; }
    public string? Warning { get; set; }
}

public class CLOEResultHandler(
    SaraDbContext context,
    IMqttPublisherService mqttPublisherService,
    ITimeseriesService timeseriesService,
    ILogger<CLOEResultHandler> logger
) : IWorkflowResultHandler
{
    public string WorkflowType => "cloe";

    public async Task OnWorkflowCompleted(Workflow workflow)
    {
        var inspectionRecord = await InspectionRecordResolver.GetSingleInspectionRecordOrNull(
            context,
            workflow,
            nameof(CLOEResultHandler),
            logger
        );

        if (inspectionRecord is null)
            return;

        var result = WorkflowResultHandlerHelpers.DeserializeResult<CLOEResult>(workflow, logger);

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

        await TryUploadTimeseries(workflow, inspectionRecord, result);
    }

    private async Task TryUploadTimeseries(
        Workflow workflow,
        InspectionRecord inspectionRecord,
        CLOEResult? result
    )
    {
        if (result?.OilLevel is not { } oilLevel || result.Confidence is not { } confidence)
        {
            logger.LogWarning(
                "Skipping CLOE timeseries upload for workflow {WorkflowId}: oilLevel or confidence is null",
                workflow.Id
            );
            return;
        }

        var uploadRequest = new TriggerTimeseriesUploadRequest
        {
            Name =
                $"{inspectionRecord.InstallationCode}_{inspectionRecord.Tag}_{inspectionRecord.InspectionDescription}",
            Facility = inspectionRecord.InstallationCode,
            ExternalId = "",
            Description = "CLOE-oil-level",
            Unit = "percentage",
            AssetId = inspectionRecord.InstallationCode,
            Value = oilLevel * 100,
            Timestamp = inspectionRecord.Timestamp ?? DateTime.UtcNow,
            Step = true,
            Metadata = new Dictionary<string, string>
            {
                { "Confidence", confidence.ToString(CultureInfo.InvariantCulture) },
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
                "Failed to upload CLOE oil-level datapoint to Timeseries for workflow {WorkflowId}",
                workflow.Id
            );
        }
    }
}
