using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services.ResultHandlers.WorkflowResultHandlers;

internal sealed class AnonymizerResult
{
    public bool IsPersonInImage { get; set; }
    public BlobStorageLocation? OutputBlobStorageLocation { get; set; }
    public BlobStorageLocation? PreProcessedBlobStorageLocation { get; set; }
}

public class AnonymizerResultHandler(
    SaraDbContext context,
    IMqttPublisherService mqttPublisherService,
    ILogger<AnonymizerResultHandler> logger
) : IWorkflowResultHandler
{
    public string WorkflowType => "anonymizer";

    public async Task OnWorkflowCompleted(Workflow workflow)
    {
        var inspectionRecord = await InspectionRecordResolver.GetSingleInspectionRecordOrNull(
            context,
            workflow,
            nameof(AnonymizerResultHandler),
            logger
        );

        if (inspectionRecord is null)
            return;

        if (workflow.OutputBlobStorageLocation is not { } output)
        {
            logger.LogWarning(
                "Anonymizer workflow {WorkflowId} has no OutputBlobStorageLocation — cannot publish visualization_available",
                workflow.Id
            );
            return;
        }

        // workflow.AnalysisRun is not eagerly loaded, so fetch explicitly to avoid NullReferenceException.
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
