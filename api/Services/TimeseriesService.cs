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
        var postRequestData = new TriggerTimeseriesUploadRequest
        {
            Name = name,
            Facility = isarInspectionValueMessage.InstallationCode,
            ExternalId = "",
            Description = isarInspectionValueMessage.InspectionType,
            Unit = isarInspectionValueMessage.Unit,
            AssetId = isarInspectionValueMessage.InstallationCode, // TODO: check what assetId is
            Value = isarInspectionValueMessage.Value,
            Timestamp = isarInspectionValueMessage.Timestamp,
            Metadata = new Dictionary<string, string>
            {
                { "tag_id", isarInspectionValueMessage.TagID },
                { "inspection_description", isarInspectionValueMessage.InspectionDescription },
                { "robot_name", isarInspectionValueMessage.RobotName },
            },
        };

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
            + $"{FloorWithTolerance(isarInspectionValueMessage.X)}E_"
            + $"{FloorWithTolerance(isarInspectionValueMessage.Y)}N_"
            + $"{FloorWithTolerance(isarInspectionValueMessage.Z)}U_"
            + $"{isarInspectionValueMessage.TagID}_"
            + $"{isarInspectionValueMessage.RobotName}_"
            + $"{description}";
        return name;
    }

    // Tolerance set to 0.06 by default to mimic expected fault tolerance in a robot positioning system
    public static int FloorWithTolerance(double value, double tolerance = 0.06)
    {
        var floored = (int)Math.Floor(value);
        if (value - floored >= 1 - tolerance)
            return floored + 1;
        return floored;
    }
}
