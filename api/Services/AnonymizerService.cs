using System.Text;
using System.Text.Json;
using api.Database;

namespace api.Services;

public class TriggerArgoWorkflowAnalysisRequest(
    string inspectionId,
    BlobStorageLocation rawDataBlobStorageLocation,
    BlobStorageLocation anonymizedBlobStorageLocation,
    BlobStorageLocation visualizedBlobStorageLocation,
    bool shouldRunConstantLevelOiler
)
{
    public string InspectionId { get; } = inspectionId;
    public BlobStorageLocation RawDataBlobStorageLocation { get; } = rawDataBlobStorageLocation;
    public BlobStorageLocation AnonymizedBlobStorageLocation { get; } =
        anonymizedBlobStorageLocation;
    public BlobStorageLocation VisualizedBlobStorageLocation { get; } =
        visualizedBlobStorageLocation;
    public bool ShouldRunConstantLevelOiler { get; } = shouldRunConstantLevelOiler;
}

public interface IArgoWorkflowService
{
    public Task TriggerAnalysis(PlantData data, bool shouldRunConstantLevelOiler);
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

    public async Task TriggerAnalysis(PlantData data, bool shouldRunConstantLevelOiler)
    {
        var postRequestData = new TriggerArgoWorkflowAnalysisRequest(
            data.InspectionId,
            data.RawDataBlobStorageLocation,
            data.AnonymizedBlobStorageLocation,
            data.AnonymizedBlobStorageLocation, // TODO: Change this to data.VisualizedBlobStorageLocation when the PlantData is update with this field
            shouldRunConstantLevelOiler
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
