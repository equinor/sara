using System.Text;
using System.Text.Json;
using api.MQTT;

namespace api.Services;

public class TriggerTimeseriesUploadRequest(
    string name,
    string facility,
    string externalId,
    string description,
    string unit,
    string assetId,
    float value,
    DateTime timestamp,
    bool step = true,
    Dictionary<string, string>? metadata = null
)
{
    public string Name { get; } = name;
    public string Facility { get; } = facility;
    public string ExternalId { get; } = externalId;
    public string Description { get; } = description;
    public string Unit { get; } = unit;
    public string AssetId { get; } = assetId;
    public float Value { get; } = value;
    public DateTime Timestamp { get; } = timestamp;
    public bool Step { get; } = step;
    public Dictionary<string, string> Metadata { get; } = metadata ?? [];
}

public interface ITimeseriesService
{
    public Task TriggerTimeseriesUpload(IsarInspectionValueMessage isarInspectionValueMessage);
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

    public async Task TriggerTimeseriesUpload(IsarInspectionValueMessage isarInspectionValueMessage)
    {
        var name = CreateTimeseriesName(isarInspectionValueMessage);
        var postRequestData = new TriggerTimeseriesUploadRequest(
            name,
            isarInspectionValueMessage.InstallationCode,
            "",
            isarInspectionValueMessage.InspectionType,
            isarInspectionValueMessage.Unit,
            isarInspectionValueMessage.InstallationCode, // TODO: check what assetId is
            isarInspectionValueMessage.Value,
            isarInspectionValueMessage.Timestamp,
            metadata: new Dictionary<string, string>
            {
                { "tag_id", isarInspectionValueMessage.TagID },
                { "inspection_description", isarInspectionValueMessage.InspectionDescription },
                { "robot_name", isarInspectionValueMessage.RobotName },
            }
        );

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
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

    private static string CreateTimeseriesName(
        IsarInspectionValueMessage isarInspectionValueMessage
    )
    {
        string description =
            isarInspectionValueMessage.InspectionDescription?.Replace(" ", "-") ?? string.Empty;
        var name =
            $"{isarInspectionValueMessage.InstallationCode}_"
            + $"{(int)Math.Floor(isarInspectionValueMessage.X)}E_"
            + $"{(int)Math.Floor(isarInspectionValueMessage.Y)}N_"
            + $"{(int)Math.Floor(isarInspectionValueMessage.Z)}U_"
            + $"{isarInspectionValueMessage.TagID}_"
            + $"{isarInspectionValueMessage.RobotName}_"
            + $"{description}";
        return name;
    }
}
