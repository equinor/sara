using api.Configurations;
using api.Database.Models;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using api.Services.Results;

namespace api.Services.ResultHandlers.AnalysisResultHandlers;

public class CLOEAnalysisResultHandler(
    IAnalysisResultRecorder recorder,
    ILogger<CLOEAnalysisResultHandler> logger
) : AnalysisResultHandlerBase(recorder, logger)
{
    public override string AnalysisName => "cloe";

    protected override string ResultWorkflowType => "cloe";

    protected override IReadOnlyList<ExtractedValue> ExtractValues(Workflow workflow)
    {
        var result = WorkflowResultHandlerHelpers.DeserializeResult<CLOEResult>(workflow, logger);

        if (result?.OilLevel is not { } oilLevel)
            return [];

        return
        [
            ExtractedValue.Numeric(
                ResultKeys.OilLevel,
                // The model reports a 0-1 ratio; stored canonically as a percentage.
                oilLevel * 100d,
                ResultUnits.Percent,
                result.Confidence,
                result.Warning,
                workflow.Id
            ),
        ];
    }
}
