using System.Text;
using System.Text.Json;
using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using api.Services.ResultHandlers.AnalysisResultHandlers;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services;

public class WorkflowTriggerFailedException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public interface IWorkflowService
{
    public Task TriggerWorkflow(Guid workflowId);

    public Task OnWorkflowCompleted(Guid workflowId);

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
    IOptions<AnalysisOptions> analysisOptions,
    IEnumerable<ITriggerPayloadEnricher> payloadEnrichers,
    IEnumerable<IWorkflowResultHandler> workflowResultHandlers,
    IEnumerable<IAnalysisResultHandler> analysisResultHandlers,
    IHttpClientFactory httpClientFactory,
    ILogger<WorkflowService> logger
) : IWorkflowService
{
    public const string ArgoHttpClientName = "Argo";

    private static readonly JsonSerializerOptions useCamelCaseOption = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AnalysisOptions _options = analysisOptions.Value;

    private readonly Dictionary<string, ITriggerPayloadEnricher> _enrichersByType =
        payloadEnrichers.ToDictionary(e => e.WorkflowType, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IWorkflowResultHandler> _workflowResultHandlersByType =
        workflowResultHandlers.ToDictionary(h => h.WorkflowType, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IAnalysisResultHandler> _analysisResultHandlersByName =
        analysisResultHandlers.ToDictionary(h => h.AnalysisName, StringComparer.OrdinalIgnoreCase);

    public async Task TriggerWorkflow(Guid workflowId)
    {
        var workflow = await context.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId);
        if (workflow is null)
        {
            logger.LogError(
                "Workflow {WorkflowId} not found when attempting to trigger",
                workflowId
            );
            return;
        }

        // Owned collection (InputBlobStorageLocations) is not loaded by the
        // FirstOrDefaultAsync above, and the in-memory state can drift from
        // DB when the entity was modified earlier in the same scope (e.g.
        // by AnonymizerResultHandler.RewireNextWorkflowIfThermalReading,
        // which mutates the collection then SaveChangesAsync's; the rewire
        // is persisted correctly to DB but EF leaves stale child entries on
        // the in-memory navigation property). Detach the cached entity and
        // re-fetch with explicit Include so we ship exactly what's
        // persisted.
        context.Entry(workflow).State = EntityState.Detached;
        workflow = await context
            .Workflows.Include(w => w.InputBlobStorageLocations)
            .FirstAsync(w => w.Id == workflowId);

        if (!_options.Workflows.TryGetValue(workflow.WorkflowType, out var workflowConfig))
        {
            throw new InvalidOperationException(
                $"Unknown workflow type '{workflow.WorkflowType}' — not found in configuration"
            );
        }

        if (workflow.OutputBlobStorageLocation is null)
        {
            throw new InvalidOperationException(
                $"Workflow {workflow.Id} ({workflow.WorkflowType}) has no OutputBlobStorageLocation"
            );
        }

        try
        {
            var extras = new Dictionary<string, object>();
            if (_enrichersByType.TryGetValue(workflow.WorkflowType, out var enricher))
            {
                var inspectionRecords = await InspectionRecordResolver.GetInspectionRecords(
                    context,
                    workflow
                );
                extras = await enricher.EnrichAsync(workflow, inspectionRecords);
            }

            var payload = new Dictionary<string, object>
            {
                ["workflowId"] = workflow.Id,
                ["workflowType"] = workflow.WorkflowType,
                ["inputBlobStorageLocations"] = workflow.InputBlobStorageLocations,
                ["outputBlobStorageLocation"] = workflow.OutputBlobStorageLocation,
                ["extras"] = extras,
            };

            var json = JsonSerializer.Serialize(payload, useCamelCaseOption);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            logger.LogInformation(
                "Triggering workflow {WorkflowType} (Id: {WorkflowId}) with {InputCount} input(s) and output {OutputLocation}",
                workflow.WorkflowType,
                workflow.Id,
                workflow.InputBlobStorageLocations.Count,
                workflow.OutputBlobStorageLocation
            );

            var response = await httpClientFactory
                .CreateClient(ArgoHttpClientName)
                .PostAsync(workflowConfig.TriggerUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Argo trigger returned {(int)response.StatusCode} {response.StatusCode}: {responseBody}"
                );
            }

            workflow.Status = WorkflowStatus.InProgress;
            workflow.StartedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            logger.LogInformation(
                "Workflow {WorkflowType} (Id: {WorkflowId}) triggered successfully",
                workflow.WorkflowType,
                workflow.Id
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to trigger workflow {WorkflowType} (Id: {WorkflowId}): {ErrorMessage}",
                workflow.WorkflowType,
                workflow.Id,
                ex.Message
            );

            await MarkWorkflowFailed(workflow, ex.Message);

            throw new WorkflowTriggerFailedException(
                $"Failed to trigger workflow '{workflow.WorkflowType}'",
                ex
            );
        }
    }

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

        if (await TrySkipChainIfGateDictates(workflow, run))
        {
            return;
        }

        var nextWorkflow = run
            .Workflows.OrderBy(w => w.StepNumber)
            .FirstOrDefault(w => w.StepNumber > workflow.StepNumber);

        if (nextWorkflow is null)
        {
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
            return;
        }

        logger.LogInformation(
            "Advancing AnalysisRun {AnalysisRunId} to next workflow {NextWorkflowType} (step {NextStepNumber})",
            run.Id,
            nextWorkflow.WorkflowType,
            nextWorkflow.StepNumber
        );

        try
        {
            await TriggerWorkflow(nextWorkflow.Id);
        }
        catch (WorkflowTriggerFailedException)
        {
            // Already logged and persisted inside TriggerWorkflow.
        }
    }

    private async Task MarkWorkflowFailed(Workflow workflow, string errorMessage)
    {
        workflow.Status = WorkflowStatus.Failed;
        workflow.ErrorMessage = errorMessage;
        workflow.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await OnWorkflowCompleted(workflow.Id);
    }

    private async Task<bool> TrySkipChainIfGateDictates(Workflow workflow, AnalysisRun run)
    {
        if (
            workflow.Status != WorkflowStatus.Succeeded
            || !_options.Workflows.TryGetValue(workflow.WorkflowType, out var workflowConfig)
            || !workflowConfig.IsGate
            || workflowConfig.SkipChainIf is null
        )
        {
            return false;
        }

        var skipReason = EvaluateSkipRule(workflow, workflowConfig.SkipChainIf);
        if (skipReason is null)
        {
            return false;
        }

        var skippedWorkflows = run
            .Workflows.Where(w =>
                w.StepNumber > workflow.StepNumber && w.Status == WorkflowStatus.Pending
            )
            .ToList();

        foreach (var pending in skippedWorkflows)
        {
            pending.Status = WorkflowStatus.Skipped;
            pending.CompletedAt = DateTime.UtcNow;
        }

        run.Status = AnalysisRunStatus.Skipped;
        run.SkipReason = skipReason;
        run.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        logger.LogInformation(
            "AnalysisRun {AnalysisRunId} skipped by gate {GatingWorkflow} "
                + "(step {StepNumber}). Marked {SkippedCount} downstream workflow(s) "
                + "[{SkippedTypes}] as Skipped. Reason: {SkipReason}",
            run.Id,
            workflow.WorkflowType,
            workflow.StepNumber,
            skippedWorkflows.Count,
            string.Join(", ", skippedWorkflows.Select(w => w.WorkflowType)),
            skipReason
        );

        return true;
    }

    private string? EvaluateSkipRule(Workflow workflow, SkipRule rule)
    {
        logger.LogDebug(
            "Evaluating skip rule for gate workflow {WorkflowType} with Id: {WorkflowId}",
            workflow.WorkflowType,
            workflow.Id
        );

        string? actualValue = null;
        string? failReason = null;

        if (string.IsNullOrWhiteSpace(workflow.ResultJson))
        {
            failReason = "Gate result missing";
        }
        else
        {
            try
            {
                using var result = JsonDocument.Parse(workflow.ResultJson);
                if (
                    result.RootElement.TryGetProperty(
                        rule.ResultJsonKeyToCheckForSkipBoolean,
                        out var node
                    )
                )
                    actualValue = node.ToString();
                else
                    failReason =
                        $"Gate result missing field '{rule.ResultJsonKeyToCheckForSkipBoolean}'";
            }
            catch (JsonException)
            {
                failReason = "Gate result unparseable";
            }
        }

        if (failReason is not null)
        {
            logger.LogWarning(
                "Gate workflow {WorkflowType} with Id: {WorkflowId} cannot be evaluated, skipping chain as a precaution: {Error}",
                workflow.WorkflowType,
                workflow.Id,
                failReason
            );
            return $"{workflow.WorkflowType} gate could not be evaluated: {failReason}, skipping chain as a precaution";
        }

        var matches = string.Equals(actualValue, rule.Value, StringComparison.OrdinalIgnoreCase);

        logger.LogDebug(
            "Gate workflow {WorkflowType} with Id: {WorkflowId}: expected {Key}={Expected} and received {Key}={Actual}",
            workflow.WorkflowType,
            workflow.Id,
            rule.ResultJsonKeyToCheckForSkipBoolean,
            rule.Value,
            rule.ResultJsonKeyToCheckForSkipBoolean,
            actualValue
        );

        return matches
            ? $"{workflow.WorkflowType} gate matched: {rule.ResultJsonKeyToCheckForSkipBoolean}={rule.Value}"
            : null;
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

    public async Task RetryWorkflow(Guid id)
    {
        var workflow = await context.Workflows.FirstOrDefaultAsync(w => w.Id == id);
        if (workflow is null)
        {
            throw new KeyNotFoundException($"Workflow with id {id} not found");
        }

        workflow.Status = WorkflowStatus.Pending;
        workflow.StartedAt = null;
        workflow.CompletedAt = null;
        workflow.ErrorMessage = null;
        workflow.ResultJson = null;

        var run = await context.AnalysisRuns.FirstOrDefaultAsync(r =>
            r.Id == workflow.AnalysisRunId
        );
        if (
            run is not null
            && (run.Status == AnalysisRunStatus.Failed || run.Status == AnalysisRunStatus.Skipped)
        )
        {
            run.Status = AnalysisRunStatus.InProgress;
            run.CompletedAt = null;
            run.SkipReason = null;
        }

        var skippedSiblings = await context
            .Workflows.Where(w =>
                w.AnalysisRunId == workflow.AnalysisRunId
                && w.Id != workflow.Id
                && w.Status == WorkflowStatus.Skipped
            )
            .ToListAsync();
        foreach (var sibling in skippedSiblings)
        {
            sibling.Status = WorkflowStatus.Pending;
            sibling.StartedAt = null;
            sibling.CompletedAt = null;
            sibling.ErrorMessage = null;
            sibling.ResultJson = null;
        }

        await context.SaveChangesAsync();
        await TriggerWorkflow(id);
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
