using api.Configurations;
using api.Database.Models;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using api.Services.Results;

namespace api.Services.ResultHandlers.AnalysisResultHandlers;

public class ThermalReadingAnalysisResultHandler(
    IAnalysisResultRecorder recorder,
    ILogger<ThermalReadingAnalysisResultHandler> logger
) : AnalysisResultHandlerBase(recorder, logger)
{
    public override string AnalysisName => "thermal-reading";

    protected override string ResultWorkflowType => "thermal-reading";

    protected override IReadOnlyList<ExtractedValue> ExtractValues(Workflow workflow)
    {
        var result = WorkflowResultHandlerHelpers.DeserializeResult<ThermalReadingResult>(
            workflow,
            logger
        );

        if (result is null)
            return [];

        return
        [
            ExtractedValue.Numeric(
                ResultKeys.Temperature,
                result.Temperature,
                ResultUnits.DegreesCelsius,
                result.Confidence,
                result.Warning,
                workflow.Id
            ),
        ];
    }
}
