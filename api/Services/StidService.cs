using System.Text;
using System.Text.Json;
using api.Database;

namespace api.Services;

public class TriggerArgoStidRequest(
    string inspectionId,
    BlobStorageLocation anonymizedBlobStorageLocation,
    StidDocumentMetadata stidDocumentMetadata
)
{
    public string InspectionId { get; } = inspectionId;
    public BlobStorageLocation AnonymizedBlobStorageLocation { get; } =
        anonymizedBlobStorageLocation;

    public StidDocumentMetadata StidDocumentMetadata { get; } = stidDocumentMetadata;
}

public interface IStidService
{
    public Task TriggerStidFunc(PlantData data);
}

public class StidService(IConfiguration configuration) : IStidService
{
    private static readonly HttpClient client = new();
    private readonly string _baseUrl =
        configuration["StidBaseUrl"]
        ?? throw new InvalidOperationException("StidBaseUrl is not configured.");
    private static readonly JsonSerializerOptions useCamelCaseOption = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task TriggerStidFunc(PlantData data)
    {
        var postRequestData = new TriggerArgoStidRequest(
            data.InspectionId,
            data.AnonymizedBlobStorageLocation,
            data.StidDocumentMetadata
        );

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(_baseUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Stid function triggered successfully.");
        }
        else
        {
            Console.WriteLine("Failed to trigger Stid function.");
        }
    }
}
