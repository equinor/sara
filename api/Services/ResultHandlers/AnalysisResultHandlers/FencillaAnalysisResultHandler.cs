using api.Database.Models;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using api.Services.Results;

namespace api.Services.ResultHandlers.AnalysisResultHandlers;

public class FencillaAnalysisResultHandler(
    IAnalysisResultRecorder recorder,
    IEmailService emailService,
    ILogger<FencillaAnalysisResultHandler> logger
) : AnalysisResultHandlerBase(recorder, logger)
{
    public override string AnalysisName => "fencilla";

    protected override string ResultWorkflowType => "fencilla";

    protected override IReadOnlyList<ExtractedValue> ExtractValues(Workflow workflow)
    {
        var result = WorkflowResultHandlerHelpers.DeserializeResult<FencillaResult>(
            workflow,
            logger
        );

        if (result is null)
            return [];

        return
        [
            ExtractedValue.Boolean(
                ResultKeys.IsBreak,
                result.IsBreak,
                result.Confidence,
                result.Warning,
                workflow.Id
            ),
        ];
    }

    protected override async Task OnValuesRecorded(
        Analysis analysis,
        AnalysisRun analysisRun,
        IReadOnlyList<RecordedResultValue> recorded
    )
    {
        var alerts = recorded.Where(row =>
            row.Value.Key == ResultKeys.IsBreak && row.Value.Severity == ResultSeverity.Alert
        );

        foreach (var alert in alerts)
        {
            try
            {
                await emailService.SendFencillaResultEmail(
                    alert.InspectionRecord.InspectionId,
                    (float)(alert.Value.Confidence ?? 0d),
                    alert.InspectionRecord.InstallationCode
                );
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Unable to send fencilla results email for InspectionId {InspectionId}: {Error}",
                    alert.InspectionRecord.InspectionId,
                    ex.Message
                );
            }
        }
    }
}
