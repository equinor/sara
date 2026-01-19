using System.Text;
using System.Text.Json;
using api.MQTT;

namespace api.Services;

public record TriggerTimeseriesUploadRequest
{
    public required string Name { get; init; }
    public required string Facility { get; init; }
    public required string ExternalId { get; init; }
    public required string Description { get; init; }
    public required string Unit { get; init; }
    public required string AssetId { get; init; }
    public float Value { get; init; }
    public DateTime Timestamp { get; init; }
    public bool Step { get; init; } = true;
    public Dictionary<string, string> Metadata { get; init; } = [];
}

public interface ITimeseriesService
{
    public Task TriggerTimeseriesUpload(TriggerTimeseriesUploadRequest isarInspectionValueMessage);
}

public class TimeseriesService(IConfiguration configuration, ILogger<TimeseriesService> logger)
    : ITimeseriesService
{
    private static readonly HttpClient client = new();
    private readonly string _baseUrl =
        configuration["SARATimeseriesBaseUrl"]
        ?? throw new InvalidOperationException("SARATimeseriesBaseUrl is not configured.");
    private static readonly JsonSerializerOptions useCamelCaseOption = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task TriggerTimeseriesUpload(TriggerTimeseriesUploadRequest uploadRequest)
    {
        if (_baseUrl == "")
            return;

        var json = JsonSerializer.Serialize(uploadRequest, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(_baseUrl, content);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Uploaded to Timeseries successfully.");
        }
        else
        {
            logger.LogError("Failed to upload to Timeseries.");
        }
    }
}
