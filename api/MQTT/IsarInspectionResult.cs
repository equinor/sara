using System.Text.Json.Serialization;

namespace api.MQTT;

#nullable disable
public class InspectionPathMessage
{
    [JsonPropertyName("source")]
    public string Source { get; set; }

    [JsonPropertyName("blob_storage_account_url")]
    public string BlobStorageAccountURL { get; set; }

    [JsonPropertyName("blob_container")]
    public string BlobContainer { get; set; }

    [JsonPropertyName("blob_name")]
    public string BlobName { get; set; }
}


public abstract class MqttMessage { }
#nullable disable
public class IsarInspectionResultMessage : MqttMessage
{
    [JsonPropertyName("isar_id")]
    public string ISARID { get; set; }

    [JsonPropertyName("robot_name")]
    public string RobotName { get; set; }

    [JsonPropertyName("inspection_id")]
    public string InspectionId { get; set; }

    [JsonPropertyName("inspection_path")]
    public InspectionPathMessage InspectionPath { get; set; }

    [JsonPropertyName("installation_code")]
    public string InstallationCode { get; set; }

    [JsonPropertyName("analysis_to_be_run")]
    public string[] AnalysisToBeRun { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}
