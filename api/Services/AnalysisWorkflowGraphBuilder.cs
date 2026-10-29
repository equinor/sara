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

/// <summary>Builds the complete Argo DAG for one analysis run from the configured definitions.</summary>
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

    /// <summary>Plans every step's input and output locations and builds the ordered DAG tasks.</summary>
    private async Task<List<ArgoDagTask>> BuildDagTasksAsync(
        AnalysisRun run,
        IReadOnlyList<(StepConfig Step, Workflow Workflow)> steps,
        IReadOnlyList<InspectionRecord> inspectionRecords
    )
    {
        var gates = new List<(string TaskName, SkipRule Rule)>();
        var tasks = new List<ArgoDagTask>();
        var outputsByStep = new Dictionary<string, Dictionary<string, BlobStorageLocation>>();
        var sources = new ValueSources(inspectionRecords, referenceMetadataService);
        var tag = inspectionRecords[0].Tag ?? "no-tag"; // Assumes all records are for the same tag.
        var outputNamePrefix =
            $"{run.StartedAt!.Value:yyyy-MM-dd}/{run.StartedAt.Value:HH-mm-ss}/tag__{tag}";
        List<BlobStorageLocation> defaultInputs = [.. steps[0].Workflow.InputBlobStorageLocations];

        foreach (var (step, workflow) in steps)
        {
            var config = _options.Workflows[step.Workflow];
            List<BlobStorageLocation> inputs = step.InputReference is { } input
                ? [outputsByStep[input.StepId][input.Output]]
                : defaultInputs;
            if (inputs.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Step '{step.StepId}' of analysis run {run.Id} has no input"
                );
            }
            workflow.InputBlobStorageLocations.Clear();
            workflow.InputBlobStorageLocations.AddRange(
                inputs.Select(location => location.Clone())
            );

            var outputs = PlanOutputs(run, workflow, config, inputs[0], outputNamePrefix);
            outputsByStep[step.StepId] = outputs;
            var extras = await sources.ResolveAsync(step, config.Extras, outputs);
            var parameters = await sources.ResolveAsync(step, config.Parameters, outputs);

            var task = new ArgoDagTask
            {
                Name = GetTaskName(workflow),
                TemplateRef = new ArgoTemplateRef { Name = config.Template, Template = "main" },
                Depends = tasks.LastOrDefault()?.Name is { } previous
                    ? $"{previous}.Succeeded || {previous}.Skipped || {previous}.Omitted"
                    : null,
                When = BuildGateExpression(gates),
                Arguments = new ArgoArguments
                {
                    Parameters =
                    [
                        Parameter("inputBlobStorageLocations", inputs),
                        Parameter(
                            "outputBlobStorageLocation",
                            outputs[WorkflowConfig.ResultOutput]
                        ),
                        Parameter("extras", extras),
                        .. parameters.Select(parameter =>
                            Parameter(parameter.Key, parameter.Value)
                        ),
                    ],
                },
            };
            tasks.Add(task);

            if (config.SkipChainIf is { } skipRule)
            {
                gates.Add((task.Name, skipRule));
            }
            else
            {
                defaultInputs = [outputs[WorkflowConfig.ResultOutput]];
            }
        }

        return tasks;
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

    // Strings are passed as-is; everything else as JSON.
    private static ArgoParameter Parameter(string name, object value) =>
        new()
        {
            Name = name,
            Value = value as string ?? JsonSerializer.Serialize(value, JsonOptions),
        };

    /// <summary>Resolves configured values, fetching reference metadata at most once per run.</summary>
    private sealed class ValueSources(
        IReadOnlyList<InspectionRecord> inspectionRecords,
        IReferencePolygonMetadataService referenceMetadataService
    )
    {
        private Task<ReferencePolygonMetadata?>? _reference;

        public async Task<Dictionary<string, object>> ResolveAsync(
            StepConfig step,
            Dictionary<string, ValueConfig> values,
            Dictionary<string, BlobStorageLocation> outputs
        )
        {
            Dictionary<string, object> resolved = [];
            foreach (var (key, value) in values)
            {
                object? result = value switch
                {
                    { Output: { } output } => outputs[output],
                    { Value: { } constant } => constant,
                    { Source: ValueSource.InspectionMetadata } => inspectionRecords
                        .Select(record => new { record.MissionName, record.InspectionDescription })
                        .ToList(),
                    { Source: ValueSource.ReferenceImage } => (
                        await GetReferenceAsync()
                    )?.ReferenceImageBlobStorageLocation,
                    { Source: ValueSource.ReferencePolygon } => (
                        await GetReferenceAsync()
                    )?.Polygon,
                    _ => null,
                };

                if (result is not null)
                {
                    resolved[key] = result;
                }
                else if (!value.Optional)
                {
                    var record = inspectionRecords[0];
                    throw new InvalidOperationException(
                        $"Step '{step.StepId}' needs '{key}' but no {value.Source} was found for "
                            + $"installationCode '{record.InstallationCode}', tagId '{record.Tag}', "
                            + $"inspectionDescription '{record.InspectionDescription}'"
                    );
                }
            }
            return resolved;
        }

        private Task<ReferencePolygonMetadata?> GetReferenceAsync() =>
            _reference ??= FindReferenceAsync();

        private async Task<ReferencePolygonMetadata?> FindReferenceAsync()
        {
            var record = inspectionRecords[0];
            if (
                string.IsNullOrWhiteSpace(record.InstallationCode)
                || string.IsNullOrWhiteSpace(record.Tag)
                || string.IsNullOrWhiteSpace(record.InspectionDescription)
            )
            {
                throw new InvalidOperationException(
                    $"InspectionRecord {record.InspectionId} needs InstallationCode, Tag and InspectionDescription to find reference metadata"
                );
            }

            return await referenceMetadataService.ReadByUniqueKey(
                record.InstallationCode,
                record.Tag,
                record.InspectionDescription
            );
        }
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
