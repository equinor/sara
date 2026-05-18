using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Utilities;

namespace api.Services.ResultHandlers.WorkflowResultHandlers;

internal sealed class CLOEResult
{
    public float OilLevel { get; set; }
    public float Confidence { get; set; }
    public string? Warning { get; set; }
}

public class CLOEResultHandler(
    SaraDbContext context,
    IMqttPublisherService mqttPublisherService,
    ILogger<CLOEResultHandler> logger
) : IWorkflowResultHandler
{
    public string WorkflowType => "cloe";

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
                nameof(CLOEResultHandler),
                records.Count,
                workflow.WorkflowType
            );
            return;
        }

        var inspectionRecord = records[0];
        var result = WorkflowResultHandlerHelpers.DeserializeResult<CLOEResult>(workflow, logger);

        var message = new SaraAnalysisResultMessage
        {
            InspectionIds = [inspectionRecord.InspectionId],
            AnalysisGroupId = null,
            WorkflowId = workflow.Id,
            AnalysisRunId = workflow.AnalysisRunId,
            AnalysisId = workflow.AnalysisRun.AnalysisId,
            AnalysisType = workflow.WorkflowType,
            Value = result?.OilLevel.ToString("F2"),
            Unit = "fraction [oilLevel]",
            Confidence = result is null ? null : result.Confidence * 100,
            Warning = result?.Warning,
        };

        if (workflow.OutputBlobStorageLocation is { } output)
        {
            message.StorageAccount = output.StorageAccount;
            message.BlobContainer = output.BlobContainer;
            message.BlobName = output.BlobName;
        }

        await mqttPublisherService.PublishSaraAnalysisResultAvailable(message);
    }
}
