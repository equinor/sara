using api.Database.Context;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IAnalysisWorkflowService
{
    Task SubmitAsync(AnalysisRun run);
}

/// <summary>Submits one generated Argo Workflow for an entire analysis run.</summary>
public class AnalysisWorkflowService(
    SaraDbContext context,
    IAnalysisWorkflowGraphBuilder graphBuilder,
    IArgoWorkflowClient argoWorkflowClient,
    ILogger<AnalysisWorkflowService> logger
) : IAnalysisWorkflowService
{
    public async Task SubmitAsync(AnalysisRun run)
    {
        try
        {
            var resource = await graphBuilder.BuildArgoWorkflowAsync(run);
            var argoName = resource.Metadata.Name!;
            await context
                .Workflows.Where(workflow => workflow.AnalysisRunId == run.Id)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(workflow => workflow.ArgoWorkflowName, argoName)
                );

            var created = await argoWorkflowClient.CreateWorkflowAsync(resource);
            await context
                .Workflows.Where(workflow => workflow.AnalysisRunId == run.Id)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(workflow => workflow.ArgoWorkflowUid, created.Uid)
                );
            logger.LogInformation(
                "Submitted Argo Workflow {ArgoWorkflowName} for AnalysisRun {AnalysisRunId} with {StepCount} steps",
                created.Name,
                run.Id,
                run.Workflows.Count
            );
        }
        catch (Exception ex)
        {
            var firstWorkflowId = run.Workflows.OrderBy(workflow => workflow.StepNumber).First().Id;
            await context
                .Workflows.Where(workflow => workflow.Id == firstWorkflowId)
                .ExecuteUpdateAsync(setters =>
                    setters
                        .SetProperty(workflow => workflow.Status, WorkflowStatus.Failed)
                        .SetProperty(workflow => workflow.ErrorMessage, ex.Message)
                        .SetProperty(workflow => workflow.CompletedAt, DateTime.UtcNow)
                );
            await context
                .AnalysisRuns.Where(candidate => candidate.Id == run.Id)
                .ExecuteUpdateAsync(setters =>
                    setters
                        .SetProperty(candidate => candidate.Status, AnalysisRunStatus.Failed)
                        .SetProperty(candidate => candidate.CompletedAt, DateTime.UtcNow)
                );
            throw new WorkflowTriggerFailedException(
                $"Failed to submit analysis run '{run.Id}' to Argo Workflows",
                ex
            );
        }
    }
}
