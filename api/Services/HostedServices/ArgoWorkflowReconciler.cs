using System.Text.Json.Nodes;
using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services.HostedServices;

/// <summary>
/// Periodically reconciles <see cref="AnalysisRun"/> rows against the state of
/// their Argo <c>Workflow</c> custom resources. For each in-flight run the
/// reconciler picks the most recent CR (by <c>metadata.creationTimestamp</c>)
/// labelled with <c>sara.equinor.com/analysis-run-id</c>; when that CR has
/// reached a terminal phase the reconciler:
/// <list type="bullet">
///   <item><description>walks <c>status.nodes</c> mapping Argo <c>Omitted</c>/<c>Skipped</c> to DB <see cref="WorkflowStatus.Skipped"/>,</description></item>
///   <item><description>finalises stuck <see cref="WorkflowStatus.Pending"/>/<see cref="WorkflowStatus.InProgress"/> rows (the per-step notifier failed),</description></item>
///   <item><description>marks the run <see cref="AnalysisRunStatus.Succeeded"/>, <see cref="AnalysisRunStatus.Failed"/>, or <see cref="AnalysisRunStatus.Skipped"/> (the last when downstream steps were Omitted by a gate's <c>when:</c>),</description></item>
///   <item><description>dispatches <c>IAnalysisResultHandler</c> via <see cref="IWorkflowService.DispatchAnalysisResultHandler"/> when the run is healed to a success-equivalent terminal state.</description></item>
/// </list>
/// </summary>
public class ArgoWorkflowReconciler(
    IServiceProvider serviceProvider,
    IOptions<AnalysisOptions> analysisOptions,
    ILogger<ArgoWorkflowReconciler> logger
) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(
        Math.Max(5, analysisOptions.Value.Argo.ReconcilerIntervalSeconds)
    );

    private const string AnalysisRunIdLabel = "sara.equinor.com/analysis-run-id";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "ArgoWorkflowReconciler started (interval: {Interval}s)",
            _interval.TotalSeconds
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileOnce(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ArgoWorkflowReconciler iteration failed");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("ArgoWorkflowReconciler stopped");
    }

    private async Task ReconcileOnce(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var submitter = scope.ServiceProvider.GetRequiredService<IArgoWorkflowSubmitter>();
        var context = scope.ServiceProvider.GetRequiredService<SaraDbContext>();
        var workflowService = scope.ServiceProvider.GetRequiredService<IWorkflowService>();

        var inProgressRuns = await context
            .AnalysisRuns.Where(r => r.Status == AnalysisRunStatus.InProgress)
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (inProgressRuns.Count == 0)
        {
            return;
        }

        var items = await submitter.ListByLabelAsync(AnalysisRunIdLabel, ct);
        if (items.Count == 0)
        {
            return;
        }

        var inProgressSet = inProgressRuns.ToHashSet();

        // Group CRs by run id and keep only the most recently created one per
        // run; retry submits a new CR after deleting the prior one, but
        // listing during the small window between delete and create-confirm
        // can return both — newest wins.
        var latestByRun = new Dictionary<Guid, (DateTimeOffset Created, JsonObject Cr)>();
        foreach (var item in items)
        {
            if (item is not JsonObject wf)
            {
                continue;
            }

            var labels = wf["metadata"]?["labels"] as JsonObject;
            var runIdStr = labels?[AnalysisRunIdLabel]?.GetValue<string>();
            if (!Guid.TryParse(runIdStr, out var runId) || !inProgressSet.Contains(runId))
            {
                continue;
            }

            var createdStr = wf["metadata"]?["creationTimestamp"]?.GetValue<string>();
            var created = DateTimeOffset.TryParse(createdStr, out var t)
                ? t
                : DateTimeOffset.MinValue;

            if (!latestByRun.TryGetValue(runId, out var existing) || created > existing.Created)
            {
                latestByRun[runId] = (created, wf);
            }
        }

        foreach (var (runId, entry) in latestByRun)
        {
            var phase = entry.Cr["status"]?["phase"]?.GetValue<string>();
            if (phase is null || !IsTerminal(phase))
            {
                continue;
            }

            await ReconcileRun(context, workflowService, runId, phase, entry.Cr, ct);
        }
    }

    private async Task ReconcileRun(
        SaraDbContext context,
        IWorkflowService workflowService,
        Guid runId,
        string crPhase,
        JsonObject workflowCr,
        CancellationToken ct
    )
    {
        var run = await context
            .AnalysisRuns.Include(r => r.Workflows)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);

        if (run is null || run.Status != AnalysisRunStatus.InProgress)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var message = workflowCr["status"]?["message"]?.GetValue<string>() ?? crPhase;

        // Map step-N-run node phases back to Workflow rows. We only consult
        // step-N-run (not the notify-* siblings) because the run node's phase
        // is what determines whether the workflow actually executed.
        var stepPhases = ExtractStepRunPhases(workflowCr);

        var anyGateInducedSkip = false;
        var anyFailure = false;
        var anyOmittedAfterGate = false;

        foreach (var step in run.Workflows.OrderBy(w => w.StepNumber))
        {
            if (!stepPhases.TryGetValue(step.StepNumber, out var nodePhase))
            {
                // Node not present in CR (CR built for a partial chain that
                // doesn't include this step, or status.nodes incomplete).
                continue;
            }

            switch (nodePhase)
            {
                case "Omitted":
                case "Skipped":
                    if (step.Status is WorkflowStatus.Pending or WorkflowStatus.InProgress)
                    {
                        step.Status = WorkflowStatus.Skipped;
                        step.CompletedAt ??= now;
                    }
                    anyOmittedAfterGate = true;
                    break;
                case "Failed":
                case "Error":
                    if (step.Status is WorkflowStatus.Pending or WorkflowStatus.InProgress)
                    {
                        step.Status = WorkflowStatus.Failed;
                        step.CompletedAt ??= now;
                        step.ErrorMessage ??= $"Argo node {nodePhase}: {message}";
                    }
                    anyFailure = true;
                    break;
                case "Succeeded":
                    if (step.Status is WorkflowStatus.Pending or WorkflowStatus.InProgress)
                    {
                        step.Status = WorkflowStatus.Succeeded;
                        step.CompletedAt ??= now;
                    }
                    break;
            }
        }

        // Determine if any Skipped step is the direct consequence of an
        // upstream gate matching its skip rule (i.e. the gate produced its
        // result and the downstream run node was Omitted). We treat that as
        // run.Status = Skipped to match legacy semantics.
        if (anyOmittedAfterGate && !anyFailure)
        {
            anyGateInducedSkip = TryFindGateInducedSkip(run, out var skipReason);
            if (anyGateInducedSkip)
            {
                run.SkipReason = skipReason;
            }
        }

        AnalysisRunStatus finalRunStatus;
        if (
            anyFailure
            || string.Equals(crPhase, "Failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(crPhase, "Error", StringComparison.OrdinalIgnoreCase)
        )
        {
            finalRunStatus = AnalysisRunStatus.Failed;
        }
        else if (anyGateInducedSkip)
        {
            finalRunStatus = AnalysisRunStatus.Skipped;
        }
        else
        {
            finalRunStatus = AnalysisRunStatus.Succeeded;
        }

        run.Status = finalRunStatus;
        run.CompletedAt = now;

        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Reconciler finalised AnalysisRun {AnalysisRunId} as {RunStatus} (CR phase={CrPhase})",
            runId,
            finalRunStatus,
            crPhase
        );

        if (finalRunStatus is AnalysisRunStatus.Succeeded or AnalysisRunStatus.Skipped)
        {
            await workflowService.DispatchAnalysisResultHandler(run);
        }
    }

    /// <summary>
    /// Extract the phase of each <c>step-N-run</c> node from
    /// <c>status.nodes</c>. The map's key is the integer N.
    /// </summary>
    private static Dictionary<int, string> ExtractStepRunPhases(JsonObject workflowCr)
    {
        var result = new Dictionary<int, string>();
        if (workflowCr["status"]?["nodes"] is not JsonObject nodes)
        {
            return result;
        }
        foreach (var (_, value) in nodes)
        {
            if (value is not JsonObject node)
            {
                continue;
            }
            var displayName = node["displayName"]?.GetValue<string>();
            var phase = node["phase"]?.GetValue<string>();
            if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(phase))
            {
                continue;
            }
            // displayName is e.g. "step-3-run". Filter to that exact pattern.
            if (
                displayName.StartsWith("step-", StringComparison.Ordinal)
                && displayName.EndsWith("-run", StringComparison.Ordinal)
            )
            {
                var middle = displayName.Substring(5, displayName.Length - 5 - 4);
                if (int.TryParse(middle, out var stepNumber))
                {
                    result[stepNumber] = phase;
                }
            }
        }
        return result;
    }

    private static bool TryFindGateInducedSkip(AnalysisRun run, out string? reason)
    {
        // Find the earliest gate step (Status == Succeeded) immediately
        // before a Skipped step; report it as the gate that caused the skip.
        var ordered = run.Workflows.OrderBy(w => w.StepNumber).ToList();
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            if (
                ordered[i].Status == WorkflowStatus.Succeeded
                && ordered[i + 1].Status == WorkflowStatus.Skipped
            )
            {
                reason =
                    $"{ordered[i].WorkflowType} gate caused chain skip at step {ordered[i].StepNumber}";
                return true;
            }
        }
        reason = null;
        return false;
    }

    private static bool IsTerminal(string phase) =>
        string.Equals(phase, "Succeeded", StringComparison.OrdinalIgnoreCase)
        || string.Equals(phase, "Failed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(phase, "Error", StringComparison.OrdinalIgnoreCase);
}
