using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Utilities;

namespace api.Services.ResultHandlers.WorkflowResultHandlers;

internal sealed class FencillaResult
{
    public bool IsBreak { get; set; }
    public float Confidence { get; set; }
    public string? Warning { get; set; }
}

public class FencillaResultHandler(
    SaraDbContext context,
    IMqttPublisherService mqttPublisherService,
    IEmailService emailService,
    ILogger<FencillaResultHandler> logger
) : IWorkflowResultHandler
{
    public string WorkflowType => "fencilla";

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
                nameof(FencillaResultHandler),
                records.Count,
                workflow.WorkflowType
            );
            return;
        }

        var inspectionRecord = records[0];
        var result = WorkflowResultHandlerHelpers.DeserializeResult<FencillaResult>(
            workflow,
            logger
        );

        var warning = result?.IsBreak == true ? "Breach detected" : result?.Warning;

        var message = new SaraAnalysisResultMessage
        {
            InspectionIds = [inspectionRecord.InspectionId],
            AnalysisGroupId = null,
            WorkflowId = workflow.Id,
            AnalysisRunId = workflow.AnalysisRunId,
            AnalysisId = workflow.AnalysisRun.AnalysisId,
            AnalysisType = workflow.WorkflowType,
            Value = result?.IsBreak.ToString(),
            Unit = "bool [isBreach]",
            Warning = warning,
            Confidence = result is null ? null : result.Confidence * 100,
        };

        if (result?.IsBreak == true && workflow.OutputBlobStorageLocation is { } output)
        {
            message.StorageAccount = output.StorageAccount;
            message.BlobContainer = output.BlobContainer;
            message.BlobName = output.BlobName;

            try
            {
                await emailService.SendFencillaResultEmail(
                    inspectionRecord.InspectionId,
                    result.Confidence,
                    inspectionRecord.InstallationCode
                );
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Unable to send fencilla results email for InspectionId {InspectionId}: {Error}",
                    inspectionRecord.InspectionId,
                    ex.Message
                );
            }
        }

        await mqttPublisherService.PublishSaraAnalysisResultAvailable(message);
    }
}
