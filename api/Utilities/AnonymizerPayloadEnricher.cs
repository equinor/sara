using api.Database.Models;

namespace api.Utilities;

public class AnonymizerPayloadEnricher(ILogger<AnonymizerPayloadEnricher> logger)
    : ITriggerPayloadEnricher
{
    public string WorkflowType => "anonymizer";

    public Task<Dictionary<string, object>> EnrichAsync(
        Workflow workflow,
        IReadOnlyList<InspectionRecord> inspectionRecords
    )
    {
        if (workflow.InputBlobStorageLocations.Count == 0)
        {
            throw new InvalidOperationException(
                $"AnonymizerPayloadEnricher invoked for workflow {workflow.Id} with no InputBlobStorageLocations"
            );
        }

        if (workflow.OutputBlobStorageLocation is null)
        {
            throw new InvalidOperationException(
                $"AnonymizerPayloadEnricher invoked for workflow {workflow.Id} with no OutputBlobStorageLocation"
            );
        }

        if (workflow.InputBlobStorageLocations.Count > 1)
        {
            logger.LogWarning(
                "AnonymizerPayloadEnricher invoked with {Count} input locations for workflow {WorkflowId} — "
                    + "this enricher only handles single inputs, using the first.",
                workflow.InputBlobStorageLocations.Count,
                workflow.Id
            );
        }

        var rawInput = workflow.InputBlobStorageLocations[0];

        var preProcessedBlobStorageLocation = new BlobStorageLocation
        {
            StorageAccount = workflow.OutputBlobStorageLocation.StorageAccount,
            BlobContainer = rawInput.BlobContainer,
            BlobName = ReplaceFileEnding(rawInput.BlobName, ".tiff"),
        };

        return Task.FromResult(
            new Dictionary<string, object>
            {
                ["preProcessedBlobStorageLocation"] = preProcessedBlobStorageLocation,
            }
        );
    }

    private static string ReplaceFileEnding(string blobName, string newExtension)
    {
        var lastDot = blobName.LastIndexOf('.');
        var lastSlash = blobName.LastIndexOf('/');
        if (lastDot <= lastSlash)
        {
            return blobName + newExtension;
        }
        return blobName[..lastDot] + newExtension;
    }
}
