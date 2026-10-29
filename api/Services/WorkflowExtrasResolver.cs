using api.Configurations;
using api.Database.Models;

namespace api.Services;

/// <summary>Resolves the configured extras of a workflow into the values sent to Argo.</summary>
internal static class WorkflowExtrasResolver
{
    public static bool NeedsReferenceMetadata(IEnumerable<WorkflowConfig> workflows) =>
        workflows.Any(workflow =>
            workflow.Extras.Values.Any(value =>
                value.Source is ValueSource.ReferenceImage or ValueSource.ReferencePolygon
            )
        );

    public static async Task<ReferencePolygonMetadata?> FindReferenceMetadataAsync(
        InspectionRecord record,
        IReferencePolygonMetadataService referenceMetadataService
    )
    {
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

    public static Dictionary<string, object> Resolve(
        Dictionary<string, ValueConfig> extras,
        Dictionary<string, BlobStorageLocation> outputs,
        ReferencePolygonMetadata? reference
    )
    {
        Dictionary<string, object> resolved = [];
        foreach (var (key, value) in extras)
        {
            object? result = value switch
            {
                { Output: { } output } => outputs[output],
                { Value: { } constant } => constant,
                { Source: ValueSource.ReferenceImage } =>
                    reference?.ReferenceImageBlobStorageLocation,
                { Source: ValueSource.ReferencePolygon } => reference?.Polygon,
                _ => null,
            };

            if (result is not null)
            {
                resolved[key] = result;
            }
        }
        return resolved;
    }
}
