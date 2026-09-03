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

        var workflow = await context
            .Workflows.AsNoTracking()
            .Include(candidate => candidate.AnalysisRun)
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == workflowId
                    && candidate.ArgoWorkflowName == name
                    && (candidate.ArgoWorkflowUid == uid || candidate.ArgoWorkflowUid == null),
                cancellationToken
            );
        if (workflow is null)
        {
            logger.LogDebug(
                "Ignoring stale Argo Workflow event for {WorkflowId} ({Uid})",
                workflowId,
                uid
            );
            return;
        }

        workflow.Status = terminalStatus;
        workflow.ResultJson = resultJson;
        workflow.ErrorMessage = errorMessage;
        workflow.ArgoWorkflowUid = uid;
        workflow.CompletedAt = DateTime.UtcNow;

        var finalized = await workflowService.FinalizeWorkflowCompleted(workflow);
        if (!finalized)
        {
            logger.LogDebug(
                "Ignoring stale or already processed Argo Workflow event for {WorkflowId} ({Uid})",
                workflowId,
                uid
            );
        }
        await workflowService.ContinueWorkflowCompleted(workflowId, name, uid, finalized);
    }
}
