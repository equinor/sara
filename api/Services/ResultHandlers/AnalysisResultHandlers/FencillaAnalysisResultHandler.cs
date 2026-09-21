using api.Database.Models;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using api.Services.Results;

namespace api.Services.ResultHandlers.AnalysisResultHandlers;

public class FencillaAnalysisResultHandler(
    IAnalysisResultRecorder recorder,
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
}
