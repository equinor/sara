using System.Text.Json.Serialization;
namespace api.MQTT
{
    public abstract class MqttMessage { }
#nullable disable
    public class IsarInspectionResultMessage : MqttMessage
    {
        [JsonPropertyName("robot_name")]
        public string RobotName { get; set; }

        [JsonPropertyName("inspection_id")]
        public string InspectionId { get; set; }

        [JsonPropertyName("inspection_path")]
        public string InspectionPath { get; set; }

        [JsonPropertyName("analysis_type")]
        public string AnalysisType { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
