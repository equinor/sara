using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

public record FetchCO2MeasurementRequest
{
    [JsonPropertyName("facility")]
    public required string Facility { get; init; }

    [JsonPropertyName("task_start_time")]
    public required string TaskStartTime { get; init; }

    [JsonPropertyName("task_end_time")]
    public required string TaskEndTime { get; init; }

    [JsonPropertyName("inspection_name")]
    public required string InspectionName { get; init; }
}

public interface ITimeseriesService
{
    public Task TriggerTimeseriesUpload(TriggerTimeseriesUploadRequest isarInspectionValueMessage);
    public Task<double?> FetchCO2ConcentrationFromTimeseries(
        FetchCO2MeasurementRequest fetchRequest
    );
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

        var url = $"{_baseUrl}/timeseries/datapoint";

        var json = JsonSerializer.Serialize(uploadRequest, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, content);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Uploaded to Timeseries successfully.");
        }
        else
        {
            logger.LogError("Failed to upload to Timeseries.");
        }
    }

    public async Task<double?> FetchCO2ConcentrationFromTimeseries(
        FetchCO2MeasurementRequest fetchRequest
    )
    {
        if (_baseUrl == "")
            return null;

        var url = $"{_baseUrl}/timeseries/get-co2-concentration";

        var json = JsonSerializer.Serialize(fetchRequest, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, content);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<double?>();
        }
        else
        {
            logger.LogError(
                "Failed to fetch CO2 concentration from Timeseries with statusCode: {StatusCode}",
                response.StatusCode
            );
            return null;
        }
    }
}
