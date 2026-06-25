using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using api.Services.ResultHandlers.AnalysisResultHandlers;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services;

public interface IWorkflowService
{
    public Task OnWorkflowCompleted(Guid workflowId);

    public Task<Workflow?> ReadById(Guid id);

    public Task<PagedList<Workflow>> GetWorkflows(WorkflowParameters parameters);

    public Task RetryWorkflow(Guid id);

    public Task Delete(Guid id);

    /// <summary>
    /// Dispatch the registered <see cref="IAnalysisResultHandler"/> for a
    /// run's analysis. Exposed for use by
    /// <see cref="HostedServices.ArgoWorkflowReconciler"/> when a run is
    /// healed/finalised outside the normal notify-exited callback chain.
    /// </summary>
    public Task DispatchAnalysisResultHandler(AnalysisRun run);
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
    IOptions<AnalysisOptions> analysisOptions,
    IEnumerable<ITriggerPayloadEnricher> payloadEnrichers,
    IEnumerable<IWorkflowResultHandler> workflowResultHandlers,
    IEnumerable<IAnalysisResultHandler> analysisResultHandlers,
    IWorkflowGraphBuilder workflowGraphBuilder,
    IArgoWorkflowSubmitter argoWorkflowSubmitter,
    ILogger<WorkflowService> logger
) : IWorkflowService
{
    private const string AnalysisRunIdLabel = "sara.equinor.com/analysis-run-id";

    private readonly AnalysisOptions _options = analysisOptions.Value;

    private readonly Dictionary<string, ITriggerPayloadEnricher> _enrichersByType =
        payloadEnrichers.ToDictionary(e => e.WorkflowType, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IWorkflowResultHandler> _workflowResultHandlersByType =
        workflowResultHandlers.ToDictionary(h => h.WorkflowType, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IAnalysisResultHandler> _analysisResultHandlersByName =
        analysisResultHandlers.ToDictionary(h => h.AnalysisName, StringComparer.OrdinalIgnoreCase);

    public async Task OnWorkflowCompleted(Guid workflowId)
    {
        var workflow = await context
            .Workflows.Include(w => w.AnalysisRun)
            .FirstOrDefaultAsync(w => w.Id == workflowId);
        if (workflow is null)
        {
            logger.LogError("Workflow {WorkflowId} not found when handling completion", workflowId);
            return;
        }

        var run = await context
            .AnalysisRuns.Include(r => r.Workflows)
            .FirstAsync(r => r.Id == workflow.AnalysisRunId);

        if (workflow.Status == WorkflowStatus.Failed)
        {
            run.Status = AnalysisRunStatus.Failed;
            run.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            logger.LogWarning(
                "AnalysisRun {AnalysisRunId} failed at workflow {WorkflowType} (step {StepNumber})",
                run.Id,
                workflow.WorkflowType,
                workflow.StepNumber
            );
            return;
        }

        await DispatchWorkflowResultHandler(workflow);

        // Under direct Argo submission Argo drives step-to-step transitions.
        // SARA only needs to detect "is this the last successful step?" and
        // finalise the run. Gate-induced skips of trailing steps surface as
        // Argo Omitted nodes and are finalised by ArgoWorkflowReconciler.
        var hasMoreSteps = run.Workflows.Any(w => w.StepNumber > workflow.StepNumber);
        if (hasMoreSteps)
        {
            return;
        }

        run.Status = AnalysisRunStatus.Succeeded;
        run.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        logger.LogInformation(
            "AnalysisRun {AnalysisRunId} completed successfully after workflow {WorkflowType} (step {StepNumber})",
            run.Id,
            workflow.WorkflowType,
            workflow.StepNumber
        );

        await DispatchAnalysisResultHandler(run);
    }

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

    public async Task DispatchAnalysisResultHandler(AnalysisRun run)
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

        if (!_analysisResultHandlersByName.TryGetValue(analysis.Name, out var handler))
        {
            logger.LogDebug(
                "No IAnalysisResultHandler registered for analysis '{AnalysisName}' — skipping result dispatch for run {AnalysisRunId}",
                analysis.Name,
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
                analysis.Name,
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

    /// <summary>
    /// Retry a workflow by resubmitting a partial Argo Workflow CR that
    /// contains only this step and all downstream steps in the chain. The DB
    /// rows for this workflow plus any Skipped/Failed siblings are reset to
    /// Pending; the run is moved back to InProgress. Any prior Argo CR(s)
    /// labelled with the run's id are deleted first so the reconciler does
    /// not observe two CRs per run.
    /// </summary>
    public async Task RetryWorkflow(Guid id)
    {
        var workflow = await context
            .Workflows.Include(w => w.InputBlobStorageLocations)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (workflow is null)
        {
            throw new KeyNotFoundException($"Workflow with id {id} not found");
        }

        var run = await context
            .AnalysisRuns.Include(r => r.Workflows)
                .ThenInclude(w => w.InputBlobStorageLocations)
            .Include(r => r.Analysis)
            .FirstAsync(r => r.Id == workflow.AnalysisRunId);

        var now = DateTime.UtcNow;

        // Reset this workflow and every downstream sibling whose row is not
        // in Pending. We do not touch upstream successful siblings — the
        // partial CR starts execution at workflow.StepNumber.
        var rowsToReset = run
            .Workflows.Where(w =>
                w.StepNumber >= workflow.StepNumber && w.Status != WorkflowStatus.Pending
            )
            .ToList();

        foreach (var w in rowsToReset)
        {
            w.Status = WorkflowStatus.Pending;
            w.StartedAt = null;
            w.CompletedAt = null;
            w.ErrorMessage = null;
            w.ResultJson = null;
        }

        run.Status = AnalysisRunStatus.InProgress;
        run.CompletedAt = null;
        run.SkipReason = null;

        await context.SaveChangesAsync();

        // Delete the previous CR(s) for this run so the reconciler doesn't
        // see two CRs for the same analysis-run-id.
        var labelSelector = $"{AnalysisRunIdLabel}={run.Id}";
        try
        {
            var deleted = await argoWorkflowSubmitter.DeleteByLabelAsync(labelSelector);
            if (deleted > 0)
            {
                logger.LogInformation(
                    "Deleted {Count} prior Argo Workflow CR(s) for AnalysisRun {AnalysisRunId} before retry",
                    deleted,
                    run.Id
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to delete prior Argo Workflow CR(s) for AnalysisRun {AnalysisRunId}; proceeding with retry anyway",
                run.Id
            );
        }

        var orderedAllWorkflows = run.Workflows.OrderBy(w => w.StepNumber).ToList();
        var inspectionRecords = await context
            .Analyses.Where(a => a.Id == run.AnalysisId)
            .SelectMany(a => a.InspectionRecords)
            .ToListAsync();

        var extrasByWorkflowId = new Dictionary<Guid, Dictionary<string, object>>();
        foreach (var step in orderedAllWorkflows.Where(w => w.StepNumber >= workflow.StepNumber))
        {
            if (_enrichersByType.TryGetValue(step.WorkflowType, out var enricher))
            {
                extrasByWorkflowId[step.Id] = await enricher.EnrichAsync(step, inspectionRecords);
            }
        }

        var manifest = workflowGraphBuilder.BuildFromStep(
            run,
            run.Analysis.Name,
            orderedAllWorkflows,
            extrasByWorkflowId,
            fromStepNumber: workflow.StepNumber
        );

        // Mark every reset step InProgress before submitting so the
        // reconciler treats them as active until per-step notifiers
        // overwrite them.
        foreach (var w in rowsToReset)
        {
            w.Status = WorkflowStatus.InProgress;
            w.StartedAt = now;
        }
        await context.SaveChangesAsync();

        var crName = await argoWorkflowSubmitter.SubmitAsync(manifest);
        logger.LogInformation(
            "Resubmitted AnalysisRun {AnalysisRunId} as Argo Workflow '{CrName}' starting from step {StepNumber}",
            run.Id,
            crName,
            workflow.StepNumber
        );
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
        context.Workflows.Remove(workflow);
        await context.SaveChangesAsync();
    }
}
