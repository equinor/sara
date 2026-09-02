using System.Text.Json;
using api.Database.Context;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IArgoWorkflowEventProcessor
{
    Task Process(ArgoWorkflowResource resource, CancellationToken cancellationToken = default);
}

public class ArgoWorkflowEventProcessor(
    SaraDbContext context,
    IWorkflowService workflowService,
    ILogger<ArgoWorkflowEventProcessor> logger
) : IArgoWorkflowEventProcessor
{
    public async Task Process(
        ArgoWorkflowResource resource,
        CancellationToken cancellationToken = default
    )
    {
        var phase = resource.Status?.Phase;
        if (phase is not ("Succeeded" or "Failed" or "Error"))
        {
            return;
        }

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
            logger.LogWarning("Ignoring Argo Workflow with missing or invalid SARA identity");
            return;
        }

        string? resultJson = null;
        string? errorMessage = resource.Status?.Message;
        var terminalStatus = WorkflowStatus.Failed;
        if (phase == "Succeeded")
        {
            resultJson = resource
                .Status?.Outputs?.Parameters.FirstOrDefault(parameter => parameter.Name == "result")
                ?.Value;
            try
            {
                if (resultJson is null)
                {
                    throw new JsonException("Result parameter is missing");
                }
                using var _ = JsonDocument.Parse(resultJson);
                terminalStatus = WorkflowStatus.Succeeded;
                errorMessage = null;
            }
            catch (JsonException ex)
            {
                resultJson = null;
                errorMessage = $"Workflow succeeded without a valid JSON result: {ex.Message}";
            }
        }

        var claimed = await context
            .Database.CreateExecutionStrategy()
            .ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(
                    cancellationToken
                );
                var updated = await context
                    .Workflows.Where(workflow =>
                        workflow.Id == workflowId
                        && workflow.ArgoWorkflowName == name
                        && (workflow.ArgoWorkflowUid == uid || workflow.ArgoWorkflowUid == null)
                        && workflow.Status == WorkflowStatus.InProgress
                    )
                    .ExecuteUpdateAsync(
                        setters =>
                            setters
                                .SetProperty(workflow => workflow.Status, terminalStatus)
                                .SetProperty(workflow => workflow.ResultJson, resultJson)
                                .SetProperty(workflow => workflow.ErrorMessage, errorMessage)
                                .SetProperty(workflow => workflow.ArgoWorkflowUid, uid)
                                .SetProperty(workflow => workflow.CompletedAt, DateTime.UtcNow),
                        cancellationToken
                    );

                if (updated == 0)
                {
                    return false;
                }

                // AnalysisRun is included because result handlers read
                // workflow.AnalysisRun.AnalysisId. This query is AsNoTracking,
                // so nothing fixes the navigation up afterwards; without the
                // include it is null and the handler throws.
                var workflow = await context
                    .Workflows.AsNoTracking()
                    .Include(workflow => workflow.AnalysisRun)
                    .SingleAsync(workflow => workflow.Id == workflowId, cancellationToken);
                await workflowService.OnWorkflowCompleted(workflow);
                await transaction.CommitAsync(cancellationToken);
                return true;
            });

        if (!claimed)
        {
            logger.LogDebug(
                "Ignoring stale or already processed Argo Workflow event for {WorkflowId} ({Uid})",
                workflowId,
                uid
            );
        }
    }
}
