using api.Database.Models;

namespace api.Services.ResultHandlers.AnalysisResultHandlers;

/// <summary>
/// Handler invoked once after all workflows in an <see cref="AnalysisRun"/> succeed.
/// Use for cross-step / aggregate result reporting.
///
/// No implementations are registered today — dispatch in <c>WorkflowService</c>
/// is a no-op until an implementation is registered in <c>Program.cs</c>.
/// For per-step result reporting, see <c>IWorkflowResultHandler</c>.
/// </summary>
public interface IAnalysisResultHandler
{
    public string AnalysisName { get; }

    public Task OnAnalysisCompleted(Analysis analysis, AnalysisRun analysisRun);
}
