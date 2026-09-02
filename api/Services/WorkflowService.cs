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
    public Task TriggerWorkflow(Workflow workflow);

    public Task OnWorkflowCompleted(Workflow workflow);

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
    IArgoWorkflowClient argoWorkflowClient,
    ILogger<WorkflowService> logger
) : IWorkflowService
{
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

    public async Task TriggerWorkflow(Workflow workflow)
    {
        // Re-fetch from DB to guarantee a tracked instance in this context, regardless of
        // which scope or method created the workflow object passed in by the caller.
        workflow = await context
            .Workflows.Include(w => w.InputBlobStorageLocations)
            .FirstAsync(w => w.Id == workflow.Id);

        if (!_options.Workflows.TryGetValue(workflow.WorkflowType, out var workflowConfig))
        {
            throw new InvalidOperationException(
                $"Unknown workflow type '{workflow.WorkflowType}' — not found in configuration"
            );
        }

        if (workflow.InputBlobStorageLocations.Count == 0)
        {
            throw new InvalidOperationException(
                $"Workflow {workflow.Id} ({workflow.WorkflowType}) has no input blob storage locations"
            );
        }

        // Compute the intended output location at trigger time. It is passed to Argo
        // as an input parameter so the container knows where to write, but it is NOT
        // persisted to the DB here. The location is only saved once the container
        // reports it back via the result JSON, confirming a blob was actually written.
        var tag =
            await context
                .InspectionRecords.Where(ir =>
                    ir.Analyses.Any(a => a.Runs.Any(r => r.Id == workflow.AnalysisRunId))
                )
                .Select(ir => ir.Tag)
                .FirstOrDefaultAsync()
            ?? "no-tag";
        var computedOutputLocation = ComputeOutputBlobStorageLocation(
            workflow.WorkflowType,
            workflow.AnalysisRunId,
            tag,
            workflow.StartedAt ?? DateTime.UtcNow,
            workflow.InputBlobStorageLocations[0]
        );

        CreatedArgoWorkflow created;
        try
        {
            var extras = new Dictionary<string, object>();
            if (_enrichersByType.TryGetValue(workflow.WorkflowType, out var enricher))
            {
                var inspectionRecords = await InspectionRecordResolver.GetInspectionRecords(
                    context,
                    workflow
                );
                extras = await enricher.EnrichAsync(
                    workflow,
                    inspectionRecords,
                    computedOutputLocation
                );
            }

            var arguments = new Dictionary<string, string>
            {
                ["workflowId"] = workflow.Id.ToString(),
                ["workflowType"] = workflow.WorkflowType,
                ["inputBlobStorageLocations"] = JsonSerializer.Serialize(
                    workflow.InputBlobStorageLocations,
                    useCamelCaseOption
                ),
                ["outputBlobStorageLocation"] = JsonSerializer.Serialize(
                    computedOutputLocation,
                    useCamelCaseOption
                ),
                ["extras"] = JsonSerializer.Serialize(extras, useCamelCaseOption),
            };

            logger.LogInformation(
                "Triggering workflow {WorkflowType} (Id: {WorkflowId}) with {InputCount} input(s) and computed output {OutputLocation}",
                workflow.WorkflowType,
                workflow.Id,
                workflow.InputBlobStorageLocations.Count,
                computedOutputLocation
            );

            context.Entry(workflow).State = EntityState.Modified;
            workflow.ArgoWorkflowName ??= $"sara-{workflow.Id:N}";
            workflow.Status = WorkflowStatus.InProgress;
            workflow.StartedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            created = await argoWorkflowClient.CreateWorkflow(
                workflow.ArgoWorkflowName,
                workflowConfig.WorkflowTemplateName,
                workflow.Id,
                arguments
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

        workflow.ArgoWorkflowUid = created.Uid;
        await context.SaveChangesAsync();
        context.Entry(workflow).State = EntityState.Detached;

        logger.LogInformation(
            "Workflow {WorkflowType} (Id: {WorkflowId}, ArgoWorkflowName: {ArgoWorkflowName}) triggered successfully",
            workflow.WorkflowType,
            workflow.Id,
            workflow.ArgoWorkflowName
        );
    }

    /// <summary>
    /// Computes the intended output blob storage location for a workflow trigger.
    /// The result is passed to Argo as an input but is NOT persisted until the
    /// container confirms a blob was actually written (via result JSON).
    /// </summary>
    private BlobStorageLocation ComputeOutputBlobStorageLocation(
        string workflowType,
        Guid analysisRunId,
        string tag,
        DateTime triggerTime,
        BlobStorageLocation fallbackInputLocation
    )
    {
        if (!_options.Workflows.TryGetValue(workflowType, out var workflowConfig))
        {
            throw new InvalidOperationException(
                $"Unknown workflow type '{workflowType}' — not found in configuration"
            );
        }

        var extension =
            workflowConfig.OutputFileExtension ?? Path.GetExtension(fallbackInputLocation.BlobName);

        var blobContainer = !string.IsNullOrEmpty(workflowConfig.OutputBlobContainer)
            ? workflowConfig.OutputBlobContainer
            : fallbackInputLocation.BlobContainer;

        var date = triggerTime.ToString("yyyy-MM-dd");
        var time = triggerTime.ToString("HH-mm-ss");
        var blobName =
            $"{date}/{time}/tag__{tag}__workflowtype__{workflowType}__analysisrunid__{analysisRunId}{extension}";

        return new BlobStorageLocation
        {
            StorageAccount = workflowConfig.OutputStorageAccount,
            BlobContainer = blobContainer,
            BlobName = blobName,
        };
    }

    public async Task OnWorkflowCompleted(Workflow workflow)
    {
        var run = await context
            .AnalysisRuns.Include(r => r.Workflows)
                .ThenInclude(w => w.InputBlobStorageLocations)
            .FirstAsync(r => r.Id == workflow.AnalysisRunId);

        // Use the EF-tracked instance from the run to avoid duplicate-tracking conflicts.
        // The caller's workflow object may be detached or already tracked under a separate
        // entry; the run include gives us the single authoritative tracked copy. Status,
        // ResultJson, ErrorMessage, and CompletedAt are copied over because the run was
        // fetched before the controller saved those values.
        var tracked = run.Workflows.Single(w => w.Id == workflow.Id);
        tracked.Status = workflow.Status;
        tracked.ResultJson = workflow.ResultJson;
        tracked.ErrorMessage = workflow.ErrorMessage;
        tracked.CompletedAt = workflow.CompletedAt;
        workflow = tracked;

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

        // Parse outputBlobStorageLocation from the result JSON and persist it. Done before
        // dispatching result handlers so they see the populated location (e.g. AnonymizerResultHandler
        // guards on OutputBlobStorageLocation before publishing visualization_available).
        await PersistOutputBlobStorageLocationFromResultJson(workflow);

        await DispatchWorkflowResultHandler(workflow);

        if (await TrySkipChainIfGateDictates(workflow, run))
        {
            return;
        }

        // Wire the next pending workflow's inputs from the completed workflow's output.
        // - If the workflow wrote a blob, use its outputBlobStorageLocation (or
        //   preProcessedBlobStorageLocation for thermal-reading chains).
        // - If the workflow is a gate (no output blob), pass its own inputs through —
        //   a gate transforms nothing, so downstream steps need the same blob the gate received.
        var nextPending = run
            .Workflows.Where(w =>
                w.StepNumber > workflow.StepNumber && w.Status == WorkflowStatus.Pending
            )
            .OrderBy(w => w.StepNumber)
            .FirstOrDefault();

        if (nextPending is not null)
        {
            List<BlobStorageLocation>? nextInputs = null;

            if (workflow.OutputBlobStorageLocation is { } completedOutput)
            {
                var nextStepInput = nextPending.WorkflowType.Equals(
                    "thermal-reading",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? TryParsePreProcessedLocation(workflow.ResultJson) ?? completedOutput
                    : completedOutput;

                nextInputs = [nextStepInput.Clone()];

                logger.LogInformation(
                    "Wired {InputType} of workflow {WorkflowType} ({WorkflowId}) as input of next workflow {NextWorkflowType} ({NextWorkflowId})",
                    nextStepInput == completedOutput ? "output" : "preProcessed output",
                    workflow.WorkflowType,
                    workflow.Id,
                    nextPending.WorkflowType,
                    nextPending.Id
                );
            }
            else if (
                _options.Workflows.TryGetValue(workflow.WorkflowType, out var completedConfig)
                && completedConfig.IsGate
                && workflow.InputBlobStorageLocations.Count > 0
            )
            {
                nextInputs = [.. workflow.InputBlobStorageLocations.Select(b => b.Clone())];

                logger.LogInformation(
                    "Gate workflow {WorkflowType} ({WorkflowId}) has no output blob — passing its inputs through to next workflow {NextWorkflowType} ({NextWorkflowId})",
                    workflow.WorkflowType,
                    workflow.Id,
                    nextPending.WorkflowType,
                    nextPending.Id
                );
            }

            if (nextInputs is not null)
            {
                nextPending.InputBlobStorageLocations.Clear();
                foreach (var loc in nextInputs)
                    nextPending.InputBlobStorageLocations.Add(loc);
                await context.SaveChangesAsync();
            }
        }

        var nextWorkflow = run
            .Workflows.Where(w => w.StepNumber > workflow.StepNumber)
            .OrderBy(w => w.StepNumber)
            .FirstOrDefault();

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
            await TriggerWorkflow(nextWorkflow);
        }
        catch (WorkflowTriggerFailedException)
        {
            // Already logged and persisted inside TriggerWorkflow.
        }
    }

    /// <summary>
    /// Parses <c>outputBlobStorageLocation</c> from the workflow's result JSON and persists it
    /// on the workflow row. This is the authoritative moment at which the DB learns a blob was
    /// actually written — the field is null until here.
    /// </summary>
    private async Task PersistOutputBlobStorageLocationFromResultJson(Workflow workflow)
    {
        var location = TryParseBlobStorageLocationProperty(
            workflow.ResultJson,
            "outputBlobStorageLocation"
        );
        if (location is null)
            return;

        workflow.OutputBlobStorageLocation = location;
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Workflow {WorkflowType} (Id: {WorkflowId}) output blob location set from result JSON: {Location}",
            workflow.WorkflowType,
            workflow.Id,
            location
        );
    }

    /// <summary>
    /// Parses the <c>preProcessedBlobStorageLocation</c> field from a workflow result JSON
    /// string, if present and valid. Returns null when the field is absent or unparseable.
    /// </summary>
    private static BlobStorageLocation? TryParsePreProcessedLocation(string? resultJson) =>
        TryParseBlobStorageLocationProperty(resultJson, "preProcessedBlobStorageLocation");

    /// <summary>
    /// Parses a named <c>BlobStorageLocation</c> property from a result JSON string.
    /// Returns null when the property is absent, incomplete, or the JSON is unparseable.
    /// </summary>
    private static BlobStorageLocation? TryParseBlobStorageLocationProperty(
        string? resultJson,
        string propertyName
    )
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            if (!doc.RootElement.TryGetProperty(propertyName, out var locEl))
                return null;

            var storageAccount = locEl.TryGetProperty("storageAccount", out var sa)
                ? sa.GetString()
                : null;
            var blobContainer = locEl.TryGetProperty("blobContainer", out var bc)
                ? bc.GetString()
                : null;
            var blobName = locEl.TryGetProperty("blobName", out var bn) ? bn.GetString() : null;

            if (
                string.IsNullOrWhiteSpace(storageAccount)
                || string.IsNullOrWhiteSpace(blobContainer)
                || string.IsNullOrWhiteSpace(blobName)
            )
                return null;

            return new BlobStorageLocation
            {
                StorageAccount = storageAccount,
                BlobContainer = blobContainer,
                BlobName = blobName,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task MarkWorkflowFailed(Workflow workflow, string errorMessage)
    {
        workflow.Status = WorkflowStatus.Failed;
        workflow.ErrorMessage = errorMessage;
        workflow.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await OnWorkflowCompleted(workflow);
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
        var retrySuffix = Guid.NewGuid().ToString("N")[..8];
        workflow.ArgoWorkflowName = $"sara-{workflow.Id:N}-{retrySuffix}";
        workflow.ArgoWorkflowUid = null;
        workflow.OutputBlobStorageLocation = null;

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
            sibling.OutputBlobStorageLocation = null;
        }

        await context.SaveChangesAsync();
        await TriggerWorkflow(workflow);
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
