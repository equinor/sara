using System.Text.Json;
using api.Configurations;
using api.Database.Models;
using Microsoft.Extensions.Options;

namespace api.Services;

public record PlannedWorkflowStep(
    WorkflowDefinition Definition,
    List<BlobStorageLocation> InputBlobStorageLocations,
    List<ArgoParameter> Parameters
);

/// <summary>Resolves every step's inputs, planned output locations and Argo parameters from definitions.</summary>
public class AnalysisPlanner(
    AnalysisAndWorkflowDefinitions definitions,
    IOptions<AnalysisOptions> options,
    IReferencePolygonMetadataService referenceMetadataService
)
{
    // Every analyzer takes this parameter; its first location decides where outputs are placed.
    private const string PrimaryInputArgument = "inputBlobStorageLocations";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<List<PlannedWorkflowStep>> PlanAsync(
        AnalysisDefinition analysis,
        Guid runId,
        DateTime startedAt,
        IReadOnlyList<InspectionRecord> records
    )
    {
        if (records.Count == 0)
        {
            throw new InvalidOperationException("Analysis needs at least one inspection record");
        }

        var needsReference = analysis.Steps.Any(step =>
            step.Inputs.Values.Any(b => b.Source == InputSourceKind.ReferenceMetadata)
        );
        var reference = needsReference ? await FindReferenceMetadataAsync(records) : null;

        List<PlannedWorkflowStep> plannedWorkflowSteps = [];
        Dictionary<string, Dictionary<string, BlobStorageLocation>> outputsByStep = [];

        foreach (var step in analysis.Steps)
        {
            var workflow = definitions.Workflows[step.Workflow];
            var inputs = ResolveInputs(step, workflow, records, reference, outputsByStep);
            var primaryInputs = GetPrimaryInputs(workflow, inputs);
            var outputs = PlanOutputs(workflow, primaryInputs[0], records[0].Tag, runId, startedAt);

            outputsByStep[step.Id] = outputs;
            plannedWorkflowSteps.Add(
                new PlannedWorkflowStep(
                    workflow,
                    primaryInputs,
                    BuildParameters(workflow, inputs, outputs)
                )
            );
        }

        return plannedWorkflowSteps;
    }

    private static Dictionary<string, object> ResolveInputs(
        StepDefinition step,
        WorkflowDefinition workflow,
        IReadOnlyList<InspectionRecord> records,
        ReferencePolygonMetadata? reference,
        Dictionary<string, Dictionary<string, BlobStorageLocation>> outputsByStep
    )
    {
        Dictionary<string, object> inputs = [];
        foreach (var (name, binding) in step.Inputs)
        {
            object? value = binding.FromStep is not null
                ? outputsByStep[binding.FromStep][binding.Output!].Clone()
                : binding.Source switch
                {
                    InputSourceKind.InspectionImages => records
                        .Select(r => r.BlobStorageLocation.Clone())
                        .ToList(),
                    InputSourceKind.InspectionMetadata => records
                        .Select(r => new { r.MissionName, r.InspectionDescription })
                        .ToList(),
                    _ => GetReferenceField(reference, binding),
                };

            if (value is null)
            {
                if (binding.Required)
                {
                    throw new InvalidOperationException(
                        $"Step '{step.Id}' input '{name}' is required but no reference metadata was found"
                    );
                }
                continue;
            }

            inputs[name] =
                workflow.Inputs[name].Type == WorkflowInputType.BlobStorageLocationList
                && value is BlobStorageLocation single
                    ? new List<BlobStorageLocation> { single }
                    : value;
        }
        return inputs;
    }

    private async Task<ReferencePolygonMetadata?> FindReferenceMetadataAsync(
        IReadOnlyList<InspectionRecord> records
    )
    {
        var record = records[0];
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

    // The validator guarantees the field is either ReferenceImage or Polygon.
    private static object? GetReferenceField(
        ReferencePolygonMetadata? reference,
        InputBinding binding
    ) =>
        reference is null ? null
        : binding.Field == ReferenceMetadataFields.Polygon ? reference.Polygon
        : reference.ReferenceImageBlobStorageLocation.Clone();

    private static List<BlobStorageLocation> GetPrimaryInputs(
        WorkflowDefinition workflow,
        Dictionary<string, object> inputs
    )
    {
        var inputName = workflow
            .Arguments.FirstOrDefault(a => a.Name == PrimaryInputArgument)
            ?.Input;
        return
            inputName is not null
            && inputs.GetValueOrDefault(inputName)
                is List<BlobStorageLocation> { Count: > 0 } locations
            ? locations
            : throw new InvalidOperationException(
                $"Workflow '{workflow.Name}' needs a '{PrimaryInputArgument}' argument bound to a non-empty location list"
            );
    }

    private Dictionary<string, BlobStorageLocation> PlanOutputs(
        WorkflowDefinition workflow,
        BlobStorageLocation primaryInput,
        string? tag,
        Guid runId,
        DateTime startedAt
    )
    {
        var account = options.Value.Workflows[workflow.Name].OutputStorageAccount;
        var generatedPrefix = $"{startedAt:yyyy-MM-dd}/{startedAt:HH-mm-ss}/tag__{tag ?? "no-tag"}";
        var primaryExtension = Path.GetExtension(primaryInput.BlobName);

        return workflow.Outputs.ToDictionary(
            output => output.Key,
            output =>
            {
                var extension = output.Value.FileExtension ?? primaryExtension;
                return new BlobStorageLocation
                {
                    StorageAccount = account,
                    BlobContainer = primaryInput.BlobContainer,
                    BlobName =
                        output.Value.Naming == OutputNaming.InputName
                            ? Path.ChangeExtension(primaryInput.BlobName, extension)
                            : $"{generatedPrefix}__workflowtype__{workflow.Name}__analysisrunid__{runId}{extension}",
                };
            }
        );
    }

    private static List<ArgoParameter> BuildParameters(
        WorkflowDefinition workflow,
        Dictionary<string, object> inputs,
        Dictionary<string, BlobStorageLocation> outputs
    ) =>
        workflow
            .Arguments.Select(argument => new ArgoParameter
            {
                Name = argument.Name,
                Value =
                    argument.Value
                    ?? JsonSerializer.Serialize(Resolve(argument, inputs, outputs), JsonOptions),
            })
            .ToList();

    private static object? Resolve(
        ArgumentValue value,
        Dictionary<string, object> inputs,
        Dictionary<string, BlobStorageLocation> outputs
    )
    {
        if (value.Input is not null)
        {
            return inputs.GetValueOrDefault(value.Input);
        }

        if (value.Output is not null)
        {
            return outputs[value.Output];
        }

        if (value.Object is not null)
        {
            // Members without a value (optional inputs) are left out entirely.
            return value
                .Object.Select(member =>
                    (member.Key, Value: Resolve(member.Value, inputs, outputs))
                )
                .Where(member => member.Value is not null)
                .ToDictionary(member => member.Key, member => member.Value);
        }

        return value.Value;
    }
}
