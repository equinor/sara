using System.Text.Json;
using api.Database.Context;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

/// <summary>
/// Processes Argo Workflow events so terminal workflow states (Succeeded|Failed|Error) are reflected in SARA.
/// Implementations associate an event with its SARA workflow, safely ignore stale or
/// duplicate events, persist the result, and trigger subsequent completion handling.
/// Non-terminal workflow events (Running|Pending) are ignored.
/// </summary>
public interface IArgoWorkflowEventProcessor
{
    Task HandleWorkflowEventAsync(
        ArgoWorkflowResource resource,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Applies terminal Argo Workflow events to their corresponding SARA workflows and triggers
/// completion handling exactly once.
/// </summary>
public class ArgoWorkflowEventProcessor(
    SaraDbContext context,
    IWorkflowService workflowService,
    ILogger<ArgoWorkflowEventProcessor> logger
) : IArgoWorkflowEventProcessor
{
    public async Task HandleWorkflowEventAsync(
        ArgoWorkflowResource resource,
        CancellationToken cancellationToken = default
    )
    {
        var terminalPhase = ReadTerminalWorkflowPhase(resource.Status?.Phase);
        if (terminalPhase is null)
        {
            return;
        }

        var identity = ReadWorkflowIdentity(resource);
        if (identity is null)
        {
            logger.LogWarning("Ignoring Argo Workflow with missing or invalid SARA identity");
            return;
        }

        var completion = ReadWorkflowCompletion(resource, terminalPhase.Value);
        var claimed = await ClaimWorkflowAndHandleCompletionAsync(
            identity.Value,
            completion,
            cancellationToken
        );

        if (!claimed)
        {
            logger.LogDebug(
                "Ignoring stale or already processed Argo Workflow event for {WorkflowId} ({Uid})",
                identity.Value.WorkflowId,
                identity.Value.Uid
            );
        }
    }

    private static TerminalArgoWorkflowPhase? ReadTerminalWorkflowPhase(string? phase) =>
        phase switch
        {
            "Succeeded" => TerminalArgoWorkflowPhase.Succeeded,
            "Failed" => TerminalArgoWorkflowPhase.Failed,
            "Error" => TerminalArgoWorkflowPhase.Error,
            _ => null,
        };

    private static WorkflowIdentity? ReadWorkflowIdentity(ArgoWorkflowResource resource)
    {
        if (
            resource.Metadata.Name is not { } name
            || resource.Metadata.Uid is not { } uid
            || !resource.Metadata.Labels.TryGetValue(
                ArgoWorkflowClient.WorkflowIdLabel,
                out var workflowIdText
            )
            || !Guid.TryParse(workflowIdText, out var workflowId)
        )
        {
            return null;
        }

        return new WorkflowIdentity(workflowId, name, uid);
    }

    /// <summary>
    /// Converts the terminal Argo state into a SARA completion. A successful Argo workflow is
    /// treated as failed when its required result parameter is missing or is not valid JSON.
    /// </summary>
    private static WorkflowCompletion ReadWorkflowCompletion(
        ArgoWorkflowResource resource,
        TerminalArgoWorkflowPhase phase
    )
    {
        if (phase != TerminalArgoWorkflowPhase.Succeeded)
        {
            return new WorkflowCompletion(WorkflowStatus.Failed, null, resource.Status?.Message);
        }

        var resultJson = resource
            .Status?.Outputs?.Parameters.FirstOrDefault(parameter => parameter.Name == "result")
            ?.Value;
        try
        {
            if (resultJson is null)
            {
                throw new JsonException("Result parameter is missing");
            }

            using var _ = JsonDocument.Parse(resultJson);
            return new WorkflowCompletion(WorkflowStatus.Succeeded, resultJson, null);
        }
        catch (JsonException ex)
        {
            return new WorkflowCompletion(
                WorkflowStatus.Failed,
                null,
                $"Workflow succeeded without a valid JSON result: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Atomically claims an in-progress workflow and runs completion handling. Keeping both
    /// operations in one transaction allows a later relist to retry if completion handling fails.
    /// </summary>
    private Task<bool> ClaimWorkflowAndHandleCompletionAsync(
        WorkflowIdentity identity,
        WorkflowCompletion completion,
        CancellationToken cancellationToken
    ) =>
        context
            .Database.CreateExecutionStrategy()
            .ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(
                    cancellationToken
                );
                if (!await TryClaimWorkflowAsync(identity, completion, cancellationToken))
                {
                    return false;
                }

                var workflow = await LoadWorkflowForCompletionAsync(
                    identity.WorkflowId,
                    cancellationToken
                );
                await workflowService.OnWorkflowCompleted(workflow);
                await transaction.CommitAsync(cancellationToken);
                return true;
            });

    /// <summary>
    /// Ensures that each workflow completion is handled only once, even though Kubernetes watches
    /// may deliver the same event multiple times or events from an older Argo execution. The update
    /// is conditional so competing processor instances cannot both complete the workflow. A missing
    /// stored UID is accepted to recover workflows created before their Argo UID was persisted.
    /// </summary>
    private async Task<bool> TryClaimWorkflowAsync(
        WorkflowIdentity identity,
        WorkflowCompletion completion,
        CancellationToken cancellationToken
    )
    {
        var updated = await context
            .Workflows.Where(workflow =>
                workflow.Id == identity.WorkflowId
                && workflow.ArgoWorkflowName == identity.Name
                && (workflow.ArgoWorkflowUid == identity.Uid || workflow.ArgoWorkflowUid == null)
                && workflow.Status == WorkflowStatus.InProgress
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(workflow => workflow.Status, completion.Status)
                        .SetProperty(workflow => workflow.ResultJson, completion.ResultJson)
                        .SetProperty(workflow => workflow.ErrorMessage, completion.ErrorMessage)
                        .SetProperty(workflow => workflow.ArgoWorkflowUid, identity.Uid)
                        .SetProperty(workflow => workflow.CompletedAt, DateTime.UtcNow),
                cancellationToken
            );

        return updated > 0;
    }

    /// <summary>
    /// Loads the claimed workflow with the analysis run required by workflow result handlers.
    /// </summary>
    private Task<Workflow> LoadWorkflowForCompletionAsync(
        Guid workflowId,
        CancellationToken cancellationToken
    ) =>
        context
            .Workflows.AsNoTracking()
            .Include(workflow => workflow.AnalysisRun)
            .SingleAsync(workflow => workflow.Id == workflowId, cancellationToken);

    private readonly record struct WorkflowIdentity(Guid WorkflowId, string Name, string Uid);

    private readonly record struct WorkflowCompletion(
        WorkflowStatus Status,
        string? ResultJson,
        string? ErrorMessage
    );

    private enum TerminalArgoWorkflowPhase
    {
        Succeeded,
        Failed,
        Error,
    }
}
