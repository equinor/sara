using api.Database.Models;
using api.Services.Results;

namespace api.Services.ResultHandlers.AnalysisResultHandlers;

public abstract class AnalysisResultHandlerBase(IAnalysisResultRecorder recorder, ILogger logger)
    : IAnalysisResultHandler
{
    public abstract string AnalysisName { get; }

    protected abstract string ResultWorkflowType { get; }

    protected abstract IReadOnlyList<ExtractedValue> ExtractValues(Workflow workflow);

    public async Task OnAnalysisCompleted(Analysis analysis, AnalysisRun analysisRun)
    {
        var workflow = FindResultWorkflow(analysisRun);
        if (workflow is null)
        {
            logger.LogWarning(
                "No '{WorkflowType}' workflow found on run {AnalysisRunId} — no result values recorded",
                ResultWorkflowType,
                analysisRun.Id
            );
            return;
        }

        var values = ExtractValues(workflow);
        if (values.Count == 0)
        {
            logger.LogWarning(
                "Extracted no result values from workflow {WorkflowId} on run {AnalysisRunId}",
                workflow.Id,
                analysisRun.Id
            );
            return;
        }

        await recorder.Record(analysis, analysisRun, values);
    }

    private Workflow? FindResultWorkflow(AnalysisRun run) =>
        run
            .Workflows.Where(candidate =>
                candidate.WorkflowType.Equals(
                    ResultWorkflowType,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .OrderByDescending(candidate => candidate.StepNumber)
            .FirstOrDefault();
}
