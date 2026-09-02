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
    public required string IsarId { get; set; }

    [JsonPropertyName("robot_name")]
    [Required]
    public required string RobotName { get; set; }

    [JsonPropertyName("inspection_id")]
    [Required]
    public required string InspectionId { get; set; }

    [JsonPropertyName("mission_id")]
    public string? MissionId { get; set; }

    [JsonPropertyName("mission_name")]
    [Required]
    public required string MissionName { get; set; }

    [JsonPropertyName("blob_storage_data_path")]
    [Required]
    public required InspectionPathMessage InspectionDataPath { get; set; }

    [JsonPropertyName("installation_code")]
    [Required]
    public required string InstallationCode { get; set; }

    [JsonPropertyName("tag_id")]
    [Required]
    public required string TagId { get; set; }

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

    [JsonPropertyName("file_type")]
    [Required]
    public required string FileType { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("acoustic_metadata")]
    public AcousticMetadataMessage? AcousticMetadata { get; set; }

    [JsonPropertyName("analysis_group")]
    public IsarAnalysisGroupMessage? AnalysisGroup { get; set; }
}

public class AcousticMetadataMessage
{
    [JsonPropertyName("snr_value")]
    [Required]
    public required double SnrValue { get; set; }

    [JsonPropertyName("leak_rate")]
    [Required]
    public required double LeakRate { get; set; }

    [JsonPropertyName("leak_rate_unit")]
    [Required]
    public required string LeakRateUnit { get; set; }

    [JsonPropertyName("sound_pressure_level_at_sensor_db")]
    [Required]
    public required double SoundPressureLevelAtSensorDb { get; set; }

    [JsonPropertyName("sound_pressure_level_at_source_db")]
    [Required]
    public required double SoundPressureLevelAtSourceDb { get; set; }

    [JsonPropertyName("distance_to_source")]
    [Required]
    public required double DistanceToSource { get; set; }

    [JsonPropertyName("result")]
    [Required]
    public required string Result { get; set; }

    [JsonPropertyName("frequency_from")]
    [Required]
    public required double FrequencyFrom { get; set; }

    [JsonPropertyName("frequency_to")]
    [Required]
    public required double FrequencyTo { get; set; }
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
}

public class SaraAnalysisResultMessage : MqttMessage
{
    [JsonPropertyName("inspection_ids")]
    public required List<string> InspectionIds { get; set; }

    [JsonPropertyName("analysis_id")]
    public required Guid AnalysisId { get; set; }

    [JsonPropertyName("analysis_group_id")]
    public string? AnalysisGroupId { get; set; }

    [JsonPropertyName("workflow_id")]
    public required Guid WorkflowId { get; set; }

    [JsonPropertyName("analysis_run_id")]
    public required Guid AnalysisRunId { get; set; }

    [JsonPropertyName("analysisType")]
    public required string AnalysisType { get; set; }
}
