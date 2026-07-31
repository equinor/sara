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
        var result = WorkflowResultHandlerHelpers.DeserializeResult<AnonymizerResult>(
            workflow,
            logger
        );

        await RewireNextWorkflowIfThermalReading(workflow, result);

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

    private async Task RewireNextWorkflowIfThermalReading(
        Workflow workflow,
        AnonymizerResult? result
    )
    {
        var nextWorkflow = await context
            .Workflows.Where(w =>
                w.AnalysisRunId == workflow.AnalysisRunId && w.StepNumber > workflow.StepNumber
            )
            .OrderBy(w => w.StepNumber)
            .FirstOrDefaultAsync();

        if (nextWorkflow is null || nextWorkflow.WorkflowType != "thermal-reading")
        {
            return;
        }

        if (result?.PreProcessedBlobStorageLocation is not { } preProcessed)
        {
            logger.LogError(
                "Anonymizer workflow {WorkflowId} is followed by thermal-reading workflow "
                    + "{NextWorkflowId} but result is missing preProcessedBlobStorageLocation — "
                    + "thermal-reading will run against the raw input.",
                workflow.Id,
                nextWorkflow.Id
            );
            return;
        }

        nextWorkflow.InputBlobStorageLocations = [preProcessed];
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Rewired thermal-reading workflow {NextWorkflowId} inputs to anonymizer's preProcessed TIFF",
            nextWorkflow.Id
        );
    }
}
