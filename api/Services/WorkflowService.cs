using System.Text;
using System.Text.Json;
using api.Controllers.WorkflowNotification;
using api.Database.Models;
using api.Utilities;

namespace api.Services;

public record TriggerAnonymizerRequest(
    string InspectionId,
    BlobStorageLocation RawDataBlobStorageLocation,
    BlobStorageLocation AnonymizedBlobStorageLocation,
    BlobStorageLocation? PreProcessedBlobStorageLocation
);

public record TriggerCLOERequest(
    string InspectionId,
    BlobStorageLocation SourceBlobStorageLocation,
    BlobStorageLocation VisualizedBlobStorageLocation
);

public record TriggerFencillaRequest(
    string InspectionId,
    BlobStorageLocation SourceBlobStorageLocation,
    BlobStorageLocation VisualizedBlobStorageLocation
);

public record TriggerThermalReadingRequest(
    string InspectionId,
    BlobStorageLocation SourceBlobStorageLocation,
    BlobStorageLocation VisualizedBlobStorageLocation,
    BlobStorageLocation ReferenceImageBlobStorageLocation,
    BlobStorageLocation ReferencePolygonBlobStorageLocation
);

public interface IArgoWorkflowService
{
    public Task TriggerAnonymizer(string inspectionId, Anonymization anonymization);
    public Task TriggerCLOE(string inspectionId, CLOEAnalysis analysis);
    public Task TriggerFencilla(string inspectionId, FencillaAnalysis analysis);
    public Task TriggerThermalReading(
        string inspectionId,
        string tagId,
        string inspectionDescription,
        string installationCode,
        ThermalReadingAnalysis analysis
    );
    public WorkflowStatus GetWorkflowStatus(
        WorkflowExitedNotification notification,
        string workflowType
    );
}

public class ArgoWorkflowService(
    IConfiguration configuration,
    ILogger<ArgoWorkflowService> logger,
    IThermalReferenceMetadataService thermalReferenceMetadataService
) : IArgoWorkflowService
{
    private static readonly HttpClient client = new();
    private readonly string _baseUrlAnonymizer =
        configuration["ArgoWorkflowAnonymizerBaseUrl"]
        ?? throw new InvalidOperationException("ArgoWorkflowAnonymizerBaseUrl is not configured.");
    private readonly string _baseUrlCLOE =
        configuration["ArgoWorkflowCLOEBaseUrl"]
        ?? throw new InvalidOperationException("ArgoWorkflowCLOEBaseUrl is not configured.");
    private readonly string _baseUrlFencilla =
        configuration["ArgoWorkflowFencillaBaseUrl"]
        ?? throw new InvalidOperationException("ArgoWorkflowFencillaBaseUrl is not configured.");
    private readonly string _baseUrlThermalReading =
        configuration["ArgoWorkflowThermalReadingBaseUrl"]
        ?? throw new InvalidOperationException(
            "ArgoWorkflowThermalReadingBaseUrl is not configured."
        );

    private static readonly JsonSerializerOptions useCamelCaseOption = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task TriggerAnonymizer(string inspectionId, Anonymization anonymization)
    {
        var postRequestData = new TriggerAnonymizerRequest(
            InspectionId: inspectionId,
            RawDataBlobStorageLocation: anonymization.SourceBlobStorageLocation,
            AnonymizedBlobStorageLocation: anonymization.DestinationBlobStorageLocation,
            PreProcessedBlobStorageLocation: anonymization.PreProcessedBlobStorageLocation
        );

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        logger.LogInformation(
            "Triggering Anonymizer. InspectionId: {InspectionId}, "
                + "RawDataBlobStorageLocation: {RawDataBlobStorageLocation}, "
                + "AnonymizedBlobStorageLocation: {AnonymizedBlobStorageLocation}, "
                + "PreProcessedBlobStorageLocation: {PreProcessedBlobStorageLocation}",
            inspectionId,
            anonymization.SourceBlobStorageLocation,
            anonymization.DestinationBlobStorageLocation,
            anonymization.PreProcessedBlobStorageLocation
        );

        var response = await client.PostAsync(_baseUrlAnonymizer, content);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Anonymizer function triggered successfully.");
        }
        else
        {
            logger.LogError("Failed to trigger anonymizer function.");
        }
    }

    public async Task TriggerCLOE(string inspectionId, CLOEAnalysis analysis)
    {
        var postRequestData = new TriggerCLOERequest(
            InspectionId: inspectionId,
            SourceBlobStorageLocation: analysis.SourceBlobStorageLocation,
            VisualizedBlobStorageLocation: analysis.DestinationBlobStorageLocation
        );

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        logger.LogInformation(
            "Triggering CLOE. InspectionId: {InspectionId}, "
                + "SourceBlobStorageLocation: {SourceBlobStorageLocation}, "
                + "VisualizedBlobStorageLocation: {VisualizedBlobStorageLocation}",
            inspectionId,
            analysis.SourceBlobStorageLocation,
            analysis.DestinationBlobStorageLocation
        );

        var response = await client.PostAsync(_baseUrlCLOE, content);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("CLOE function triggered successfully.");
        }
        else
        {
            logger.LogError("Failed to trigger CLOE function.");
        }
    }

    public async Task TriggerFencilla(string inspectionId, FencillaAnalysis analysis)
    {
        var postRequestData = new TriggerFencillaRequest(
            InspectionId: inspectionId,
            SourceBlobStorageLocation: analysis.SourceBlobStorageLocation,
            VisualizedBlobStorageLocation: analysis.DestinationBlobStorageLocation
        );

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        logger.LogInformation(
            "Triggering Fencilla. InspectionId: {InspectionId}, "
                + "SourceBlobStorageLocation: {SourceBlobStorageLocation}, "
                + "VisualizedBlobStorageLocation: {VisualizedBlobStorageLocation}",
            inspectionId,
            analysis.SourceBlobStorageLocation,
            analysis.DestinationBlobStorageLocation
        );

        var response = await client.PostAsync(_baseUrlFencilla, content);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Fencilla function triggered successfully.");
        }
        else
        {
            logger.LogError("Failed to trigger Fencilla function.");
        }
    }

    public async Task TriggerThermalReading(
        string inspectionId,
        string tagId,
        string inspectionDescription,
        string installationCode,
        ThermalReadingAnalysis analysis
    )
    {
        var thermalReferenceMetadata = await thermalReferenceMetadataService.ReadByUniqueKey(
            installationCode,
            tagId,
            inspectionDescription
        );

        if (thermalReferenceMetadata is null)
        {
            var errorMessage =
                $"Could not find thermal reference metadata for installationCode '{installationCode}', tagId '{tagId}', inspectionDescription '{inspectionDescription}'";
            logger.LogError(errorMessage);
            throw new ApplicationException(errorMessage);
        }

        var postRequestData = new TriggerThermalReadingRequest(
            InspectionId: inspectionId,
            SourceBlobStorageLocation: analysis.SourceBlobStorageLocation,
            VisualizedBlobStorageLocation: analysis.DestinationBlobStorageLocation,
            ReferenceImageBlobStorageLocation: thermalReferenceMetadata.ReferenceImageBlobStorageLocation,
            ReferencePolygonBlobStorageLocation: thermalReferenceMetadata.ReferencePolygonBlobStorageLocation
        );

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        logger.LogInformation(
            "Triggering ThermalReading. InspectionId: {InspectionId}, "
                + "SourceBlobStorageLocation: {SourceBlobStorageLocation}, "
                + "VisualizedBlobStorageLocation: {VisualizedBlobStorageLocation}, "
                + "ReferenceImageBlobStorageLocation: {ReferenceImageBlobStorageLocation}, "
                + "ReferencePolygonBlobStorageLocation: {ReferencePolygonBlobStorageLocation}",
            inspectionId,
            analysis.SourceBlobStorageLocation,
            analysis.DestinationBlobStorageLocation,
            thermalReferenceMetadata.ReferenceImageBlobStorageLocation,
            thermalReferenceMetadata.ReferencePolygonBlobStorageLocation
        );

        var response = await client.PostAsync(_baseUrlThermalReading, content);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("ThermalReading function triggered successfully.");
        }
        else
        {
            logger.LogError("Failed to trigger ThermalReading function.");
        }
    }

    public WorkflowStatus GetWorkflowStatus(
        WorkflowExitedNotification notification,
        string workflowType
    )
    {
        var inspectionId = Sanitize.SanitizeUserInput(notification.InspectionId);
        var workflowFailures = Sanitize.SanitizeUserInput(notification.WorkflowFailures);

        if (
            notification.ExitHandlerWorkflowStatus == ExitHandlerWorkflowStatus.Failed
            || notification.ExitHandlerWorkflowStatus == ExitHandlerWorkflowStatus.Error
        )
        {
            logger.LogWarning(
                "{WorkflowType} workflow for InspectionId: {InspectionId} exited with status: {Status} and failures: {WorkflowFailures}.",
                workflowType,
                inspectionId,
                notification.ExitHandlerWorkflowStatus,
                workflowFailures
            );
            return WorkflowStatus.ExitFailure;
        }
        else
        {
            logger.LogInformation(
                "{WorkflowType} workflow for InspectionId: {InspectionId} exited successfully",
                workflowType,
                inspectionId
            );
            return WorkflowStatus.ExitSuccess;
        }
    }
}
