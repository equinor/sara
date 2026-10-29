using System.Text.Json;
using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using api.Utilities;
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
    IReferencePolygonMetadataService referenceMetadataService
) : IAnalysisWorkflowGraphBuilder
{
    public const string Entrypoint = "analysis-pipeline";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private readonly AnalysisOptions _options = analysisOptions.Value;

    /// <summary>Builds the Argo resource and updates workflow inputs for the caller to persist.</summary>
    public async Task<ArgoWorkflowResource> BuildArgoWorkflowAsync(AnalysisRun run)
    {
        var workflows = GetOrderedWorkflows(run);
        var steps = GetSteps(run, workflows);
        var inspectionRecords = await InspectionRecordResolver.GetInspectionRecords(
            context,
            workflows[0]
        );
        var tasks = await BuildDagTasksAsync(run, steps, inspectionRecords);

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

    private List<(StepConfig Step, Workflow Workflow)> GetSteps(
        AnalysisRun run,
        List<Workflow> workflows
    )
    {
        var analysisType = run.Analysis.AnalysisType;
        if (!_options.Analyses.TryGetValue(analysisType, out var analysis))
        {
            throw new InvalidOperationException(
                $"Unknown analysis type '{analysisType}' - not found in configuration"
            );
        }

        var steps = analysis.Steps.Zip(workflows).ToList();
        if (
            analysis.Steps.Count != workflows.Count
            || steps.Any(pair => pair.First.Workflow != pair.Second.WorkflowType)
        )
        {
            throw new InvalidOperationException(
                $"Workflows of analysis run {run.Id} do not match the configured steps of '{analysisType}'"
            );
        }
        return steps;
    }

    /// <summary>
    /// Builds the ordered DAG tasks while carrying gate conditions and planned outputs forward.
    /// </summary>
    private async Task<List<ArgoDagTask>> BuildDagTasksAsync(
        AnalysisRun run,
        IReadOnlyList<(StepConfig Step, Workflow Workflow)> steps,
        IReadOnlyList<InspectionRecord> inspectionRecords
    )
    {
        var gates = new List<(string TaskName, SkipRule Rule)>();
        var tasks = new List<ArgoDagTask>();
        var outputsByStep = new Dictionary<string, Dictionary<string, BlobStorageLocation>>();
        var referenceMetadata = await FindReferenceMetadataIfNeededAsync(
            steps,
            inspectionRecords[0]
        );
        var currentInputs = steps[0].Workflow.InputBlobStorageLocations.ToList();
        var tag = inspectionRecords[0].Tag ?? "no-tag"; // Assumes all records are for the same tag.
        var date = run.StartedAt!.Value.ToString("yyyy-MM-dd");
        var time = run.StartedAt.Value.ToString("HH-mm-ss");
        var outputNamePrefix = $"{date}/{time}/tag__{tag}";

        foreach (var (step, workflow) in steps)
        {
            var config = GetWorkflowConfig(workflow);
            List<BlobStorageLocation> inputs = step.InputReference is { } reference
                ? [outputsByStep[reference.StepId][reference.Output]]
                : currentInputs;
            if (workflow != steps[0].Workflow)
            {
                workflow.InputBlobStorageLocations.Clear();
                workflow.InputBlobStorageLocations.AddRange(inputs.Select(input => input.Clone()));
            }

            var outputs = PlanOutputs(run, workflow, config, inputs[0], outputNamePrefix);
            outputsByStep[step.StepId] = outputs;
            var requestedOutput = outputs[WorkflowConfig.ResultOutput];

            var extras = WorkflowExtrasResolver.Resolve(config.Extras, outputs, referenceMetadata);
            var task = BuildDagTask(
                workflow,
                config,
                requestedOutput,
                extras,
                tasks.LastOrDefault()?.Name,
                gates
            );
            tasks.Add(task);

            if (config.IsGate && config.SkipChainIf is not null)
            {
                gates.Add((task.Name, config.SkipChainIf));
            }
            if (!config.IsGate)
            {
                currentInputs = [requestedOutput];
            }
        }

        return tasks;
    }

    private async Task<ReferencePolygonMetadata?> FindReferenceMetadataIfNeededAsync(
        IReadOnlyList<(StepConfig Step, Workflow Workflow)> steps,
        InspectionRecord record
    )
    {
        var workflowConfigs = steps.Select(step => GetWorkflowConfig(step.Workflow));
        if (!WorkflowExtrasResolver.NeedsReferenceMetadata(workflowConfigs))
        {
            return null;
        }

        return await WorkflowExtrasResolver.FindReferenceMetadataAsync(
            record,
            referenceMetadataService
        );
    }

    private WorkflowConfig GetWorkflowConfig(Workflow workflow)
    {
        if (!_options.Workflows.TryGetValue(workflow.WorkflowType, out var config))
        {
            throw new InvalidOperationException(
                $"Unknown workflow type '{workflow.WorkflowType}' - not found in configuration"
            );
        }
        return config;
    }

    private Dictionary<string, BlobStorageLocation> PlanOutputs(
        AnalysisRun run,
        Workflow workflow,
        WorkflowConfig config,
        BlobStorageLocation input,
        string outputNamePrefix
    ) =>
        config.Outputs.ToDictionary(
            output => output.Key,
            output =>
            {
                var extension = output.Value.Extension ?? Path.GetExtension(input.BlobName);
                var suffix = output.Key == WorkflowConfig.ResultOutput ? "" : $"__{output.Key}";
                return new BlobStorageLocation
                {
                    StorageAccount = _options.StorageAccounts[output.Value.StorageAccount],
                    BlobContainer = input.BlobContainer,
                    BlobName = output.Value.KeepInputName
                        ? Path.ChangeExtension(input.BlobName, extension)
                        : $"{outputNamePrefix}__workflowtype__{workflow.WorkflowType}__analysisrunid__{run.Id}{suffix}{extension}",
                };
            }
        );

    private static ArgoDagTask BuildDagTask(
        Workflow workflow,
        WorkflowConfig config,
        BlobStorageLocation requestedOutput,
        Dictionary<string, object> extras,
        string? previousTaskName,
        IReadOnlyList<(string TaskName, SkipRule Rule)> gates
    ) =>
        new()
        {
            Name = GetTaskName(workflow),
            TemplateRef = new ArgoTemplateRef { Name = config.Template, Template = "main" },
            Depends = previousTaskName is null
                ? null
                : $"{previousTaskName}.Succeeded || {previousTaskName}.Skipped || {previousTaskName}.Omitted",
            When = BuildGateExpression(gates),
            Arguments = BuildArguments(workflow, requestedOutput, extras),
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
        BlobStorageLocation requestedOutput,
        Dictionary<string, object> extras
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
                JsonSerializer.Serialize(requestedOutput, JsonOptions)
            ),
            Parameter("extras", JsonSerializer.Serialize(extras, JsonOptions)),
        };
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
