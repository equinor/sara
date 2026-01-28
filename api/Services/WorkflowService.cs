using System.Text;
using System.Text.Json;
using api.Controllers.WorkflowNotification;
using api.Database.Models;
using api.Utilities;

namespace api.Services;

public record TriggerAnonymizerRequest(
    Guid InspectionId,
    BlobStorageLocation RawDataBlobStorageLocation,
    BlobStorageLocation AnonymizedBlobStorageLocation,
    BlobStorageLocation? PreProcessedBlobStorageLocation
);

public record TriggerCLOERequest(
    Guid InspectionId,
    BlobStorageLocation SourceBlobStorageLocation,
    BlobStorageLocation VisualizedBlobStorageLocation
);

public record TriggerFencillaRequest(
    Guid InspectionId,
    BlobStorageLocation SourceBlobStorageLocation,
    BlobStorageLocation VisualizedBlobStorageLocation
);

public record TriggerThermalReadingRequest(
    Guid InspectionId,
    string TagId,
    string InspectionDescription,
    string InstallationCode,
    BlobStorageLocation SourceBlobStorageLocation,
    BlobStorageLocation VisualizedBlobStorageLocation
);

public interface IArgoWorkflowService
{
    public Task TriggerAnonymizer(Guid inspectionId, Anonymization anonymization);
    public Task TriggerCLOE(Guid inspectionId, CLOEAnalysis analysis);
    public Task TriggerFencilla(Guid inspectionId, FencillaAnalysis analysis);
    public Task TriggerThermalReading(
        Guid inspectionId,
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

public class ArgoWorkflowService(IConfiguration configuration, ILogger<ArgoWorkflowService> logger)
    : IArgoWorkflowService
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

    public async Task TriggerAnonymizer(Guid inspectionId, Anonymization anonymization)
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
            logger.LogInformation("Function triggered successfully.");
        }
        else
        {
            logger.LogError("Failed to trigger function.");
        }
    }

    public async Task TriggerCLOE(Guid inspectionId, CLOEAnalysis analysis)
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
            logger.LogInformation("Function triggered successfully.");
        }
        else
        {
            logger.LogError("Failed to trigger function.");
        }
    }

    public async Task TriggerFencilla(Guid inspectionId, FencillaAnalysis analysis)
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
            logger.LogInformation("Function triggered successfully.");
        }
        else
        {
            logger.LogError("Failed to trigger function.");
        }
    }

    public async Task TriggerThermalReading(
        Guid inspectionId,
        string tagId,
        string inspectionDescription,
        string installationCode,
        ThermalReadingAnalysis analysis
    )
    {
        var postRequestData = new TriggerThermalReadingRequest(
            InspectionId: inspectionId,
            TagId: tagId,
            InspectionDescription: inspectionDescription,
            InstallationCode: installationCode,
            SourceBlobStorageLocation: analysis.SourceBlobStorageLocation,
            VisualizedBlobStorageLocation: analysis.DestinationBlobStorageLocation
        );

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        logger.LogInformation(
            "Triggering ThermalReading. InspectionId: {InspectionId}, TagId: {TagId}, "
                + "InspectionDescription: {InspectionDescription}, InstallationCode: {InstallationCode}, "
                + "SourceBlobStorageLocation: {SourceBlobStorageLocation}, "
                + "VisualizedBlobStorageLocation: {VisualizedBlobStorageLocation}",
            inspectionId,
            tagId,
            inspectionDescription,
            installationCode,
            analysis.SourceBlobStorageLocation,
            analysis.DestinationBlobStorageLocation
        );

        var response = await client.PostAsync(_baseUrlThermalReading, content);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Function triggered successfully.");
        }
        else
        {
            logger.LogError("Failed to trigger function.");
        }
    }

    public WorkflowStatus GetWorkflowStatus(
        WorkflowExitedNotification notification,
        string workflowType
    )
    {
        var workflowFailures = Sanitize.SanitizeUserInput(notification.WorkflowFailures);

        if (
            notification.ExitHandlerWorkflowStatus == ExitHandlerWorkflowStatus.Failed
            || notification.ExitHandlerWorkflowStatus == ExitHandlerWorkflowStatus.Error
        )
        {
            logger.LogWarning(
                "{WorkflowType} workflow for InspectionId: {InspectionId} exited with status: {Status} and failures: {WorkflowFailures}.",
                workflowType,
                notification.InspectionId,
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
                notification.InspectionId
            );
            return WorkflowStatus.ExitSuccess;
        }
    }
}
