using System.Text;
using System.Text.Json;
using api.Database.Models;

namespace api.Services;

public record TriggerAnonymizerRequest(
    string InspectionId,
    BlobStorageLocation RawDataBlobStorageLocation,
    BlobStorageLocation AnonymizedBlobStorageLocation
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

public interface IArgoWorkflowService
{
    public Task TriggerAnonymizer(string inspectionId, Anonymization anonymization);
    public Task TriggerCLOE(string inspectionId, Analysis analysis);
    public Task TriggerFencilla(string inspectionId, Analysis analysis);
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

    private static readonly JsonSerializerOptions useCamelCaseOption = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task TriggerAnonymizer(string inspectionId, Anonymization anonymization)
    {
        var postRequestData = new TriggerAnonymizerRequest(
            InspectionId: inspectionId,
            RawDataBlobStorageLocation: anonymization.RawDataBlobStorageLocation,
            AnonymizedBlobStorageLocation: anonymization.AnonymizedBlobStorageLocation
        );

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

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

    public async Task TriggerCLOE(string inspectionId, Analysis analysis)
    {
        var postRequestData = new TriggerCLOERequest(
            InspectionId: inspectionId,
            SourceBlobStorageLocation: analysis.SourceBlobStorageLocation,
            VisualizedBlobStorageLocation: analysis.VisualizedBlobStorageLocation
        );

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

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

    public async Task TriggerFencilla(string inspectionId, Analysis analysis)
    {
        var postRequestData = new TriggerFencillaRequest(
            InspectionId: inspectionId,
            SourceBlobStorageLocation: analysis.SourceBlobStorageLocation,
            VisualizedBlobStorageLocation: analysis.VisualizedBlobStorageLocation
        );

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

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
}
