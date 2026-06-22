using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Utilities;

namespace api.Services.ResultHandlers.WorkflowResultHandlers;

/// <summary>
/// Publishes visualization_available for the copy-raw-to-visualized passthrough
/// workflow, which copies a raw inspection blob into the visualization base layer
/// without running an analyzer.
/// </summary>
public class CopyRawToVisualizedResultHandler(
    SaraDbContext context,
    IMqttPublisherService mqttPublisherService,
    ILogger<CopyRawToVisualizedResultHandler> logger
) : IWorkflowResultHandler
{
    public string WorkflowType => "copy-raw-to-visualized";

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
                nameof(CopyRawToVisualizedResultHandler),
                records.Count,
                workflow.WorkflowType
            );
            return;
        }

        var inspectionRecord = records[0];

        if (workflow.OutputBlobStorageLocation is not { } output)
        {
            logger.LogWarning(
                "copy-raw-to-visualized workflow {WorkflowId} has no OutputBlobStorageLocation — cannot publish visualization_available",
                workflow.Id
            );
            return;
        }

        var message = new SaraVisualizationAvailableMessage
        {
            InspectionId = inspectionRecord.InspectionId,
            WorkflowId = workflow.Id,
            AnalysisRunId = workflow.AnalysisRunId,
            AnalysisId = workflow.AnalysisRun.AnalysisId,
            StorageAccount = output.StorageAccount,
            BlobContainer = output.BlobContainer,
            BlobName = output.BlobName,
        };

        await mqttPublisherService.PublishSaraVisualizationAvailable(message);
    }
}
