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
    ILogger<ThermalReadingResultHandler> logger
) : IWorkflowResultHandler
{
    public string WorkflowType => "thermal-reading";

    public async Task OnWorkflowCompleted(Workflow workflow)
    {
        var inspectionRecord = await InspectionRecordResolver.GetSingleInspectionRecordOrNull(
            context,
            workflow,
            nameof(ThermalReadingResultHandler),
            logger
        );

        if (inspectionRecord is null)
            return;

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
    }
}
