using System.Text.Json.Serialization;

namespace api.MQTT;

/// <summary>
/// Represents the message payload for a successful workflow status update.
/// </summary>
public class WorkflowStatusSuccessMessage : MqttMessage
{
    [JsonPropertyName("inspection_id")]
    public required string InspectionId { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("timestamp")]
    public required DateTime Timestamp { get; set; }
}
