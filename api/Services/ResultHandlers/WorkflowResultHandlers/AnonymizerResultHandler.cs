using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Utilities;

namespace api.Services.ResultHandlers.WorkflowResultHandlers;

internal sealed class AnonymizerResult
{
    public bool IsPersonInImage { get; set; }
    public BlobStorageLocation? OutputBlobStorageLocation { get; set; }
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
        // Deserialize for side-effect of validating ResultJson shape and
        // logging via the helper; the per-MQTT message below only needs the
        // workflow's persisted OutputBlobStorageLocation.
        _ = WorkflowResultHandlerHelpers.DeserializeResult<AnonymizerResult>(workflow, logger);

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
