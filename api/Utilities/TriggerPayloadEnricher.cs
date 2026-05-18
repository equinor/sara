using api.Database.Models;
using api.Services;

namespace api.Utilities;

public interface ITriggerPayloadEnricher
{
    public string WorkflowType { get; }

    /// <summary>
    /// Returns the per-workflow-type extras dictionary. The dictionary is
    /// serialized as the <c>extras</c> object on the Argo trigger payload;
    /// keys are passed verbatim and become fields on that nested object.
    /// Return an empty dictionary when no extras are needed.
    /// </summary>
    public Task<Dictionary<string, object>> EnrichAsync(
        Workflow workflow,
        IReadOnlyList<InspectionRecord> inspectionRecords
    );
}

public class ThermalReadingPayloadEnricher(
    IThermalReferenceMetadataService thermalReferenceMetadataService,
    ILogger<ThermalReadingPayloadEnricher> logger
) : ITriggerPayloadEnricher
{
    public string WorkflowType => "thermal-reading";

    public async Task<Dictionary<string, object>> EnrichAsync(
        Workflow workflow,
        IReadOnlyList<InspectionRecord> inspectionRecords
    )
    {
        if (inspectionRecords.Count == 0)
        {
            throw new InvalidOperationException(
                $"ThermalReadingPayloadEnricher invoked for workflow {workflow.Id} with no InspectionRecords"
            );
        }

        if (inspectionRecords.Count > 1)
        {
            logger.LogWarning(
                "ThermalReadingPayloadEnricher invoked with {Count} records for workflow {WorkflowId} — "
                    + "this enricher only handles single records, using the first.",
                inspectionRecords.Count,
                workflow.Id
            );
        }

        var inspectionRecord = inspectionRecords[0];

        if (string.IsNullOrWhiteSpace(inspectionRecord.InstallationCode))
        {
            throw new InvalidOperationException(
                $"InspectionRecord {inspectionRecord.InspectionId} is missing InstallationCode — required for thermal-reading enrichment"
            );
        }

        if (string.IsNullOrWhiteSpace(inspectionRecord.Tag))
        {
            throw new InvalidOperationException(
                $"InspectionRecord {inspectionRecord.InspectionId} is missing Tag — required for thermal-reading enrichment"
            );
        }

        if (string.IsNullOrWhiteSpace(inspectionRecord.InspectionDescription))
        {
            throw new InvalidOperationException(
                $"InspectionRecord {inspectionRecord.InspectionId} is missing InspectionDescription — required for thermal-reading enrichment"
            );
        }

        var metadata = await thermalReferenceMetadataService.ReadByUniqueKey(
            inspectionRecord.InstallationCode,
            inspectionRecord.Tag,
            inspectionRecord.InspectionDescription
        );

        if (metadata is null)
        {
            var errorMessage =
                $"Could not find thermal reference metadata for installationCode '{inspectionRecord.InstallationCode}', "
                + $"tagId '{inspectionRecord.Tag}', inspectionDescription '{inspectionRecord.InspectionDescription}'";
            logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        return new Dictionary<string, object>
        {
            ["referenceImageBlobStorageLocation"] = metadata.ReferenceImageBlobStorageLocation,
            ["referencePolygonBlobStorageLocation"] = metadata.ReferencePolygonBlobStorageLocation,
        };
    }
}
