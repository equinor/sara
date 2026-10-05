using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace api.MQTT;

#nullable disable
public class IsarInspectionValueMessage : MqttMessage
{
    [JsonPropertyName("isar_id")]
    public string ISARID { get; set; }

    [JsonPropertyName("robot_name")]
    public string RobotName { get; set; }

    [JsonPropertyName("inspection_id")]
    public string InspectionId { get; set; }

    [JsonPropertyName("blob_storage_data_path")]
    [Required]
    public required InspectionPathMessage InspectionDataPath { get; set; }

    [JsonPropertyName("installation_code")]
    public string InstallationCode { get; set; }

    [JsonPropertyName("tag_id")]
    public string TagID { get; set; }

    [JsonPropertyName("inspection_type")]
    public string InspectionType { get; set; }

    [JsonPropertyName("inspection_description")]
    public string InspectionDescription { get; set; }

    [JsonPropertyName("value")]
    public float Value { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("robot_pose")]
    public Database.Models.Pose RobotPose { get; set; }

    [JsonPropertyName("target_position")]
    public Database.Models.Position TargetPosition { get; set; }
}
