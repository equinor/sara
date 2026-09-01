using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

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

        var analysisRun = await context.AnalysisRuns.FirstOrDefaultAsync(r =>
            r.Id == workflow.AnalysisRunId
        );
        if (analysisRun is null)
        {
            logger.LogError(
                "AnalysisRun {AnalysisRunId} not found for workflow {WorkflowId} — cannot publish visualization_available",
                workflow.AnalysisRunId,
                workflow.Id
            );
            return;
        }

        var message = new SaraVisualizationAvailableMessage
        {
            InspectionId = inspectionRecord.InspectionId,
            WorkflowId = workflow.Id,
            AnalysisRunId = workflow.AnalysisRunId,
            AnalysisId = analysisRun.AnalysisId,
        };

        await mqttPublisherService.PublishSaraVisualizationAvailable(message);
    }
}
