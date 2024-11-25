using System.Text;
using System.Text.Json;
using api.Models;

namespace api.Services;

public class TriggerArgoAnonymizerRequest(string inspectionId, BlobStorageLocation rawDataBlobStorageLocation, BlobStorageLocation anonymizedBlobStorageLocation)
{
    public string InspectionId { get; } = inspectionId;
    public BlobStorageLocation RawDataBlobStorageLocation { get; } = rawDataBlobStorageLocation;
    public BlobStorageLocation AnonymizedBlobStorageLocation { get; } = anonymizedBlobStorageLocation;
}

public interface IAnonymizerService
{
    public Task TriggerAnonymizerFunc(InspectionData data);
}

public class AnonymizerService(IConfiguration configuration) : IAnonymizerService
{
    private static readonly HttpClient client = new();
    private readonly string _baseUrl = configuration["AnonymizerBaseUrl"]
                   ?? throw new InvalidOperationException("AnonymizerBaseUrl is not configured.");
    private static readonly JsonSerializerOptions useCamelCaseOption = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task TriggerAnonymizerFunc(InspectionData data)
    {
        var postRequestData = new TriggerArgoAnonymizerRequest(data.InspectionId, data.RawDataBlobStorageLocation, data.AnonymizedBlobStorageLocation);

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(_baseUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Function triggered successfully.");
        }
        else
        {
            Console.WriteLine("Failed to trigger function.");
        }
    }
}
