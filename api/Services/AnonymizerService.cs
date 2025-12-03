using System.Text;
using System.Text.Json;
using api.Database.Models;

namespace api.Services;

public record TriggerArgoWorkflowAnalysisRequest(
    string InspectionId,
    BlobStorageLocation RawDataBlobStorageLocation,
    BlobStorageLocation AnonymizedBlobStorageLocation,
    BlobStorageLocation VisualizedBlobStorageLocation,
    bool ShouldRunConstantLevelOiler,
    bool ShouldRunFencilla,
    bool ShouldRunSteamTrap
);

public interface IArgoWorkflowService
{
    public Task TriggerAnalysis(
        PlantData data,
        bool shouldRunConstantLevelOiler,
        bool shouldRunFencilla,
        bool shouldRunSteamTrap
    );
}

public class ArgoWorkflowService(IConfiguration configuration, ILogger<ArgoWorkflowService> logger)
    : IArgoWorkflowService
{
    private static readonly HttpClient client = new();
    private readonly string _baseUrl =
        configuration["ArgoWorkflowAnalysisBaseUrl"]
        ?? throw new InvalidOperationException("ArgoWorkflowAnalysisBaseUrl is not configured.");
    private static readonly JsonSerializerOptions useCamelCaseOption = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task TriggerAnalysis(
        PlantData data,
        bool shouldRunConstantLevelOiler,
        bool shouldRunFencilla,
        bool shouldRunSteamTrap
    )
    {
        var postRequestData = new TriggerArgoWorkflowAnalysisRequest(
            data.InspectionId,
            data.RawDataBlobStorageLocation,
            data.AnonymizedBlobStorageLocation,
            data.VisualizedBlobStorageLocation,
            shouldRunConstantLevelOiler,
            shouldRunFencilla,
            shouldRunSteamTrap
        );

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(_baseUrl, content);

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
