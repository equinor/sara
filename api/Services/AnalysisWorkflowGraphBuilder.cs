using System.Text.Json;
using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services;

public interface IAnalysisWorkflowGraphBuilder
{
    Task<ArgoWorkflowResource> BuildArgoWorkflowAsync(AnalysisRun run);
}

/// <summary>Builds the complete Argo DAG for one analysis run.</summary>
public class AnalysisWorkflowGraphBuilder(
    SaraDbContext context,
    IOptions<AnalysisOptions> analysisOptions,
    IEnumerable<ITriggerPayloadEnricher> payloadEnrichers
) : IAnalysisWorkflowGraphBuilder
{
    public const string Entrypoint = "analysis-pipeline";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private readonly AnalysisOptions _options = analysisOptions.Value;
    private readonly Dictionary<string, ITriggerPayloadEnricher> _enrichersByType =
        payloadEnrichers.ToDictionary(
            enricher => enricher.WorkflowType,
            StringComparer.OrdinalIgnoreCase
        );

    /// <summary>Builds the complete Argo Workflow resource for an analysis run.</summary>
    public async Task<ArgoWorkflowResource> BuildArgoWorkflowAsync(AnalysisRun run)
    {
        var workflows = GetOrderedWorkflows(run);
        var inspectionRecords = await InspectionRecordResolver.GetInspectionRecords(
            context,
            workflows[0]
        );
        var tasks = await BuildDagTasksAsync(workflows, inspectionRecords);

        return BuildArgoWorkflowResource(run, tasks);
    }

    private static List<Workflow> GetOrderedWorkflows(AnalysisRun run)
    {
        var workflows = run.Workflows.OrderBy(workflow => workflow.StepNumber).ToList();
        if (workflows.Count == 0)
        {
            throw new InvalidOperationException($"Analysis run {run.Id} has no workflows");
        }
        return workflows;
    }

    /// <summary>
    /// Builds the ordered DAG tasks while carrying gate conditions and enriched outputs forward.
    /// </summary>
    private async Task<List<ArgoDagTask>> BuildDagTasksAsync(
        IReadOnlyList<Workflow> workflows,
        IReadOnlyList<InspectionRecord> inspectionRecords
    )
    {
        var gates = new List<(string TaskName, SkipRule Rule)>();
        var tasks = new List<ArgoDagTask>();
        Dictionary<string, object>? previousExtras = null;
        Workflow? previousWorkflow = null;

        foreach (var workflow in workflows)
        {
            var config = GetWorkflowConfig(workflow);
            UseAnonymizerOutputForThermalReading(workflow, previousWorkflow, previousExtras);

            var extras = _enrichersByType.TryGetValue(workflow.WorkflowType, out var enricher)
                ? await enricher.EnrichAsync(workflow, inspectionRecords)
                : [];
            var task = BuildDagTask(
                workflow,
                config,
                extras,
                inspectionRecords,
                tasks.LastOrDefault()?.Name,
                gates
            );
            tasks.Add(task);

            if (config.IsGate && config.SkipChainIf is not null)
            {
                gates.Add((task.Name, config.SkipChainIf));
            }
            previousExtras = extras;
            previousWorkflow = workflow;
        }

        return tasks;
    }

    private WorkflowConfig GetWorkflowConfig(Workflow workflow)
    {
        if (!_options.Workflows.TryGetValue(workflow.WorkflowType, out var config))
        {
            throw new InvalidOperationException(
                $"Unknown workflow type '{workflow.WorkflowType}' - not found in configuration"
            );
        }
        if (workflow.OutputBlobStorageLocation is null)
        {
            throw new InvalidOperationException(
                $"Workflow {workflow.Id} ({workflow.WorkflowType}) has no output location"
            );
        }
        return config;
    }

    /// <summary>
    /// Replaces thermal-reading input with the TIFF produced by the preceding anonymizer.
    /// </summary>
    private static void UseAnonymizerOutputForThermalReading(
        Workflow workflow,
        Workflow? previousWorkflow,
        Dictionary<string, object>? previousExtras
    )
    {
        if (
            workflow.WorkflowType != "thermal-reading"
            || previousWorkflow?.WorkflowType != "anonymizer"
            || previousExtras?.TryGetValue("preProcessedBlobStorageLocation", out var value) != true
            || value is not BlobStorageLocation preProcessed
        )
        {
            return;
        }

        workflow.InputBlobStorageLocations.Clear();
        workflow.InputBlobStorageLocations.Add(preProcessed.Clone());
    }

    private static ArgoDagTask BuildDagTask(
        Workflow workflow,
        WorkflowConfig config,
        Dictionary<string, object> extras,
        IReadOnlyList<InspectionRecord> inspectionRecords,
        string? previousTaskName,
        IReadOnlyList<(string TaskName, SkipRule Rule)> gates
    ) =>
        new()
        {
            Name = GetTaskName(workflow),
            TemplateRef = new ArgoTemplateRef
            {
                Name = config.WorkflowTemplateName,
                Template = "main",
            },
            Depends = previousTaskName is null
                ? null
                : $"{previousTaskName}.Succeeded || {previousTaskName}.Skipped || {previousTaskName}.Omitted",
            When = BuildGateExpression(gates),
            Arguments = BuildArguments(workflow, extras, inspectionRecords),
        };

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

    private static ArgoParameter Parameter(string name, string value) =>
        new() { Name = name, Value = value };

    private static ArgoArguments BuildArguments(
        Workflow workflow,
        Dictionary<string, object> extras,
        IReadOnlyList<InspectionRecord> inspectionRecords
    )
    {
        var parameters = new List<ArgoParameter>
        {
            Parameter(
                "inputBlobStorageLocations",
                JsonSerializer.Serialize(workflow.InputBlobStorageLocations, JsonOptions)
            ),
            Parameter(
                "outputBlobStorageLocation",
                JsonSerializer.Serialize(workflow.OutputBlobStorageLocation, JsonOptions)
            ),
            Parameter("extras", JsonSerializer.Serialize(extras, JsonOptions)),
        };
        if (workflow.WorkflowType == "fencilla")
        {
            parameters.Add(
                Parameter(
                    "inspectionMetadata",
                    JsonSerializer.Serialize(
                        inspectionRecords.Select(record => new
                        {
                            record.MissionName,
                            record.InspectionDescription,
                        }),
                        JsonOptions
                    )
                )
            );
        }
        return new ArgoArguments { Parameters = parameters };
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
