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
        var inspectionRecord = await InspectionRecordResolver.GetSingleInspectionRecordOrNull(
            context,
            workflow,
            nameof(CopyRawToVisualizedResultHandler),
            logger
        );

        if (inspectionRecord is null)
            return;

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
