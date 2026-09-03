using api.Database.Context;
using api.Database.Models;
using api.Services.ResultHandlers.AnalysisResultHandlers;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public class WorkflowTriggerFailedException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public interface IWorkflowService
{
    public Task OnWorkflowCompleted(Workflow workflow);

    public Task OnAnalysisCompleted(AnalysisRun run);

    public Task<Workflow?> ReadById(Guid id);

    public Task<PagedList<Workflow>> GetWorkflows(WorkflowParameters parameters);

    public Task RetryWorkflow(Guid id);

    public Task Delete(Guid id);
}

public class WorkflowParameters
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? WorkflowType { get; set; }
    public WorkflowStatus? Status { get; set; }
    public Guid? AnalysisRunId { get; set; }
}

public class WorkflowService(
    SaraDbContext context,
    IEnumerable<IWorkflowResultHandler> workflowResultHandlers,
    IEnumerable<IAnalysisResultHandler> analysisResultHandlers,
    IAnalysisTriggerService analysisTriggerService,
    ILogger<WorkflowService> logger
) : IWorkflowService
{
    private readonly Dictionary<string, IWorkflowResultHandler> _workflowResultHandlersByType =
        workflowResultHandlers.ToDictionary(h => h.WorkflowType, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IAnalysisResultHandler> _analysisResultHandlersByName =
        analysisResultHandlers.ToDictionary(h => h.AnalysisName, StringComparer.OrdinalIgnoreCase);

    public async Task OnWorkflowCompleted(Workflow workflow)
    {
        await DispatchWorkflowResultHandler(workflow);
    }

    public Task OnAnalysisCompleted(AnalysisRun run) => DispatchAnalysisResultHandler(run);

    private async Task DispatchWorkflowResultHandler(Workflow workflow)
    {
        if (workflow.Status != WorkflowStatus.Succeeded)
        {
            return;
        }

        if (!_workflowResultHandlersByType.TryGetValue(workflow.WorkflowType, out var handler))
        {
            logger.LogDebug(
                "No IWorkflowResultHandler registered for workflow type '{WorkflowType}' — skipping result dispatch for workflow {WorkflowId}",
                workflow.WorkflowType,
                workflow.Id
            );
            return;
        }

        try
        {
            await handler.OnWorkflowCompleted(workflow);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Workflow result handler for type '{WorkflowType}' threw while processing workflow {WorkflowId}",
                workflow.WorkflowType,
                workflow.Id
            );
        }
    }

    private async Task DispatchAnalysisResultHandler(AnalysisRun run)
    {
        var analysis = await context
            .Analyses.Include(a => a.InspectionRecords)
            .FirstOrDefaultAsync(a => a.Id == run.AnalysisId);

        if (analysis is null)
        {
            logger.LogError(
                "Analysis {AnalysisId} not found when dispatching result handler for run {AnalysisRunId}",
                run.AnalysisId,
                run.Id
            );
            return;
        }

        if (!_analysisResultHandlersByName.TryGetValue(analysis.AnalysisType, out var handler))
        {
            logger.LogDebug(
                "No IAnalysisResultHandler registered for analysis '{AnalysisName}' — skipping result dispatch for run {AnalysisRunId}",
                analysis.AnalysisType,
                run.Id
            );
            return;
        }

        try
        {
            await handler.OnAnalysisCompleted(analysis, run);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Analysis result handler for '{AnalysisName}' threw while processing run {AnalysisRunId}",
                analysis.AnalysisType,
                run.Id
            );
        }
    }

    public async Task<Workflow?> ReadById(Guid id)
    {
        return await context
            .Workflows.Include(w => w.InputBlobStorageLocations)
            .Include(w => w.AnalysisRun)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<PagedList<Workflow>> GetWorkflows(WorkflowParameters parameters)
    {
        var query = context
            .Workflows.Include(w => w.InputBlobStorageLocations)
            .Include(w => w.AnalysisRun)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.WorkflowType))
            query = query.Where(w =>
                w.WorkflowType.ToLower().Contains(parameters.WorkflowType.ToLower())
            );

        if (parameters.Status is { } status)
            query = query.Where(w => w.Status == status);

        if (parameters.AnalysisRunId is { } runId)
            query = query.Where(w => w.AnalysisRunId == runId);

        query = query.OrderByDescending(w => w.StartedAt ?? DateTime.MinValue).ThenBy(w => w.Id);

        return await PagedList<Workflow>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task RetryWorkflow(Guid id)
    {
        var workflow = await context
            .Workflows.Include(w => w.AnalysisRun)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (workflow is null)
        {
            throw new KeyNotFoundException($"Workflow with id {id} not found");
        }

        await analysisTriggerService.RerunAnalysis(workflow.AnalysisRun.AnalysisId);
    }

    public async Task Delete(Guid id)
    {
        var workflow = await context
            .Workflows.Include(w => w.InputBlobStorageLocations)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (workflow is null)
        {
            throw new KeyNotFoundException($"Workflow with id {id} not found");
        }
        if (workflow.Status == WorkflowStatus.InProgress)
        {
            throw new InvalidOperationException("Cannot delete an in-progress workflow");
        }
        context.Workflows.Remove(workflow);
        await context.SaveChangesAsync();
    }
}
