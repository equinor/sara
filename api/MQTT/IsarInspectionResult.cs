﻿using System.Text.Json.Serialization;

namespace api.MQTT;

#nullable disable
public class InspectionPathMessage
{
    [JsonPropertyName("source")]
    public string Source { get; set; }

    [JsonPropertyName("storage_account")]
    public required string StorageAccount { get; set; }

    [JsonPropertyName("blob_container")]
    public required string BlobContainer { get; set; }

    [JsonPropertyName("blob_name")]
    public required string BlobName { get; set; }
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

    [JsonPropertyName("tag_id")]
    public string TagID { get; set; }

    [JsonPropertyName("inspection_type")]
    public string InspectionType { get; set; }

    [JsonPropertyName("inspection_description")]
    public string InspectionDescription { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}

public class SaraVisualizationAvailableMessage : MqttMessage
{
    [JsonPropertyName("inspection_id")]
    public string InspectionId { get; set; }

    [JsonPropertyName("storageAccount")]
    public required string StorageAccount { get; set; }

    [JsonPropertyName("blobContainer")]
    public required string BlobContainer { get; set; }

    [JsonPropertyName("blobName")]
    public required string BlobName { get; set; }
}
