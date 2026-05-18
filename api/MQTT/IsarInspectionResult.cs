using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace api.MQTT;

public class InspectionPathMessage
{
    [JsonPropertyName("storage_account")]
    [Required]
    public required string StorageAccount { get; set; }

    [JsonPropertyName("blob_container")]
    [Required]
    public required string BlobContainer { get; set; }

    [JsonPropertyName("blob_name")]
    [Required]
    public required string BlobName { get; set; }
}

public abstract class MqttMessage { }

public class IsarInspectionResultMessage : MqttMessage
{
    [JsonPropertyName("isar_id")]
    [Required]
    public required string ISARID { get; set; }

    [JsonPropertyName("robot_name")]
    [Required]
    public required string RobotName { get; set; }

    [JsonPropertyName("inspection_id")]
    [Required]
    public required string InspectionId { get; set; }

    [JsonPropertyName("blob_storage_data_path")]
    [Required]
    public required InspectionPathMessage InspectionDataPath { get; set; }

    [JsonPropertyName("blob_storage_metadata_path")]
    [Required]
    public required InspectionPathMessage InspectionMetadataPath { get; set; }

    [JsonPropertyName("installation_code")]
    [Required]
    public required string InstallationCode { get; set; }

    [JsonPropertyName("tag_id")]
    [Required]
    public required string TagID { get; set; }

    [JsonPropertyName("inspection_type")]
    [Required]
    public required string InspectionType { get; set; }

    [JsonPropertyName("inspection_description")]
    [Required]
    public required string InspectionDescription { get; set; }

    [JsonPropertyName("timestamp")]
    [Required]
    public required DateTime Timestamp { get; set; }

    [JsonPropertyName("required_analysis")]
    public List<string>? RequiredAnalysis { get; set; }

    [JsonPropertyName("robot_pose")]
    public Database.Models.Pose? RobotPose { get; set; }

    [JsonPropertyName("target_position")]
    public Database.Models.Position? TargetPosition { get; set; }

    [JsonPropertyName("analysis_group")]
    public IsarAnalysisGroupMessage? AnalysisGroup { get; set; }
}

public class IsarAnalysisGroupMessage
{
    [JsonPropertyName("analysis_group_id")]
    [Required]
    public required string AnalysisGroupId { get; set; }

    [JsonPropertyName("analysis_group_size")]
    [Required]
    public required int AnalysisGroupSize { get; set; }

    [JsonPropertyName("analysis_group_analyses")]
    [Required]
    public required List<string> AnalysisGroupAnalyses { get; set; }
}

public class SaraVisualizationAvailableMessage : MqttMessage
{
    [JsonPropertyName("inspection_id")]
    public required string InspectionId { get; set; }

    [JsonPropertyName("workflow_id")]
    public required Guid WorkflowId { get; set; }

    [JsonPropertyName("analysis_run_id")]
    public required Guid AnalysisRunId { get; set; }

    [JsonPropertyName("analysis_id")]
    public required Guid AnalysisId { get; set; }

    [JsonPropertyName("storageAccount")]
    public required string StorageAccount { get; set; }

    [JsonPropertyName("blobContainer")]
    public required string BlobContainer { get; set; }

    [JsonPropertyName("blobName")]
    public required string BlobName { get; set; }
}

public class SaraAnalysisResultMessage : MqttMessage
{
    [JsonPropertyName("inspection_ids")]
    public required List<string> InspectionIds { get; set; }

    [JsonPropertyName("analysis_group_id")]
    public string? AnalysisGroupId { get; set; }

    [JsonPropertyName("workflow_id")]
    public required Guid WorkflowId { get; set; }

    [JsonPropertyName("analysis_run_id")]
    public required Guid AnalysisRunId { get; set; }

    [JsonPropertyName("analysis_id")]
    public required Guid AnalysisId { get; set; }

    [JsonPropertyName("analysisType")]
    public required string AnalysisType { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("confidence")]
    public float? Confidence { get; set; } // As percentage (0-100)

    [JsonPropertyName("warning")]
    public string? Warning { get; set; }

    [JsonPropertyName("storageAccount")]
    public string? StorageAccount { get; set; }

    [JsonPropertyName("blobContainer")]
    public string? BlobContainer { get; set; }

    [JsonPropertyName("blobName")]
    public string? BlobName { get; set; }
}
