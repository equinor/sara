using api.Configurations;
using api.Database.Models;
using Microsoft.Extensions.Options;

namespace api.Utilities;

public class AnonymizerPayloadEnricher(
    IOptions<AnalysisOptions> analysisOptions,
    ILogger<AnonymizerPayloadEnricher> logger
) : ITriggerPayloadEnricher
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
            // Preprocessing uploads to the anonymizer destination, not the raw input account.
            StorageAccount = analysisOptions.Value.Workflows[WorkflowType].OutputStorageAccount,
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
