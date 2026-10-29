using System.Text.Json;
using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using api.Utilities;

namespace api.Services;

public interface IAnalysisWorkflowGraphBuilder
{
    Task<ArgoWorkflowResource> BuildArgoWorkflowAsync(AnalysisRun run);
}

/// <summary>Builds the complete Argo DAG for one analysis run.</summary>
public class AnalysisWorkflowGraphBuilder(
    SaraDbContext context,
    AnalysisAndWorkflowDefinitions definitions,
    AnalysisPlanner planner
) : IAnalysisWorkflowGraphBuilder
{
    public const string Entrypoint = "analysis-pipeline";

    /// <summary>Builds the Argo resource and updates workflow inputs for the caller to persist.</summary>
    public async Task<ArgoWorkflowResource> BuildArgoWorkflowAsync(AnalysisRun run)
    {
        var workflows = run.Workflows.OrderBy(workflow => workflow.StepNumber).ToList();
        if (workflows.Count == 0)
        {
            throw new InvalidOperationException($"Analysis run {run.Id} has no workflows");
        }

        if (!definitions.Analyses.TryGetValue(run.Analysis.AnalysisType, out var analysis))
        {
            throw new InvalidOperationException(
                $"Unknown analysis type '{run.Analysis.AnalysisType}', no definition found"
            );
        }

        if (analysis.Steps.Count != workflows.Count)
        {
            throw new InvalidOperationException(
                $"Analysis run {run.Id} has {workflows.Count} workflows but '{analysis.Name}' defines {analysis.Steps.Count} steps"
            );
        }

        var inspectionRecords = await InspectionRecordResolver.GetInspectionRecords(
            context,
            workflows[0]
        );
        var plannedWorkflowSteps = await planner.PlanAsync(
            analysis,
            run.Id,
            run.StartedAt!.Value,
            inspectionRecords
        );

        return BuildArgoWorkflowResource(run, BuildDagTasks(workflows, plannedWorkflowSteps));
    }

    /// <summary>Builds the ordered DAG tasks while carrying gate conditions forward.</summary>
    private List<ArgoDagTask> BuildDagTasks(
        IReadOnlyList<Workflow> workflows,
        IReadOnlyList<PlannedWorkflowStep> plannedWorkflowSteps
    )
    {
        var gates = new List<(string TaskName, SkipRule Rule)>();
        var tasks = new List<ArgoDagTask>();

        foreach (var (workflow, plannedWorkflowStep) in workflows.Zip(plannedWorkflowSteps))
        {
            var definition = plannedWorkflowStep.Definition;
            var previousTaskName = tasks.LastOrDefault()?.Name;

            workflow.InputBlobStorageLocations.Clear();
            workflow.InputBlobStorageLocations.AddRange(
                plannedWorkflowStep.InputBlobStorageLocations
            );

            var task = new ArgoDagTask
            {
                Name = GetTaskName(workflow),
                TemplateRef = new ArgoTemplateRef
                {
                    Name = definition.TemplateName,
                    Template = "main",
                },
                Depends = previousTaskName is null
                    ? null
                    : $"{previousTaskName}.Succeeded || {previousTaskName}.Skipped || {previousTaskName}.Omitted",
                When = BuildGateExpression(gates),
                Arguments = new ArgoArguments { Parameters = plannedWorkflowStep.Parameters },
            };
            tasks.Add(task);

            if (definition.SkipChainIf is not null)
            {
                gates.Add((task.Name, definition.SkipChainIf));
            }
        }

        return tasks;
    }

    private static ArgoWorkflowResource BuildArgoWorkflowResource(
        AnalysisRun run,
        List<ArgoDagTask> tasks
    ) =>
        new()
        {
            Metadata = new ArgoObjectMetadata
            {
                Name = GetArgoWorkflowName(run.Analysis.AnalysisType, run.Id),
                Labels = new Dictionary<string, string>
                {
                    [ArgoWorkflowClient.ManagedByLabel] = "sara",
                    [ArgoWorkflowClient.AnalysisRunIdLabel] = run.Id.ToString(),
                },
            },
            Spec = new ArgoWorkflowSpec
            {
                Entrypoint = Entrypoint,
                Templates =
                [
                    new ArgoTemplate
                    {
                        Name = Entrypoint,
                        Dag = new ArgoDag { Tasks = tasks },
                    },
                ],
            },
        };

    public static string GetArgoWorkflowName(string analysisType, Guid analysisRunId) =>
        $"{ToDnsLabel(analysisType)}-{analysisRunId:N}";

    public static string GetTaskName(Workflow workflow) =>
        $"{ToDnsLabel(workflow.WorkflowType)}-{workflow.Id:N}";

    private static string ToDnsLabel(string value)
    {
        var normalized = new string(
            value
                .ToLowerInvariant()
                .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
                .ToArray()
        ).Trim('-');
        return normalized.Length > 30 ? normalized[..30].TrimEnd('-') : normalized;
    }

    /// <summary>Combines preceding gate outputs into an Argo expression for a DAG task.</summary>
    private static string? BuildGateExpression(
        IReadOnlyList<(string TaskName, SkipRule Rule)> gates
    )
    {
        if (gates.Count == 0)
        {
            return null;
        }

        return "{{="
            + string.Join(
                " && ",
                gates.Select(gate =>
                    $"jsonpath(tasks['{gate.TaskName}'].outputs.parameters.result, '$.{gate.Rule.ResultJsonKeyToCheckForSkipBoolean}') != {ToExpressionLiteral(gate.Rule.Value)}"
                )
            )
            + "}}";
    }

    /// <summary>Converts a configured comparison value to an Argo expression literal.</summary>
    private static string ToExpressionLiteral(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            if (
                document.RootElement.ValueKind
                is JsonValueKind.True
                    or JsonValueKind.False
                    or JsonValueKind.Number
                    or JsonValueKind.Null
            )
            {
                return document.RootElement.GetRawText();
            }
        }
        catch (JsonException) { }

        return JsonSerializer.Serialize(value);
    }
}
