using System.Text.Json;
using System.Text.Json.Serialization;

namespace api.Controllers.Models;

public record BenchmarkDatasetDto(
    string Id,
    string Label,
    string Container,
    string DefaultSplit,
    IReadOnlyList<string> Devices,
    IReadOnlyList<string> Versions
);

public record TriggerBenchmarkRequest(
    string DatasetId,
    string Version,
    string Split,
    string Device,
    string? RunId
);

public record BenchmarkJobDto(
    string WorkflowName,
    string? WorkflowUid,
    string DatasetId,
    string Version,
    string Split,
    string Device,
    string RunId,
    string Phase,
    string? Message,
    DateTime? CreatedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt
);

public record BenchmarkResultDto(
    string RunId,
    BenchmarkRunMetadataDto? Run,
    BenchmarkMetricsDto? Metrics,
    string? Error
);

public record BenchmarkRunMetadataDto(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("dataset")] BenchmarkRunDatasetDto Dataset,
    [property: JsonPropertyName("artifact")] string Artifact,
    [property: JsonPropertyName("evaluator")] string Evaluator,
    [property: JsonPropertyName("protocol")] string Protocol,
    [property: JsonPropertyName("execution")] BenchmarkExecutionDto Execution
);

public record BenchmarkRunDatasetDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("split")] string Split,
    [property: JsonPropertyName("manifest_sha256")] string ManifestSha256
);

public record BenchmarkExecutionDto(
    [property: JsonPropertyName("hardware")] string Hardware,
    [property: JsonPropertyName("timing_scope")] string TimingScope
);

public record BenchmarkMetricsDto(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("processing")] BenchmarkProcessingDto Processing,
    [property: JsonPropertyName("evaluations")]
        Dictionary<string, BenchmarkEvaluationDto> Evaluations
);

public record BenchmarkProcessingDto(
    [property: JsonPropertyName("unit")] string Unit,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("seconds")] double Seconds,
    [property: JsonPropertyName("per_second")] double PerSecond
);

public record BenchmarkEvaluationDto(
    [property: JsonPropertyName("labels")] List<string> Labels,
    [property: JsonPropertyName("confusion_matrix")] List<List<int?>> ConfusionMatrix,
    [property: JsonPropertyName("matched_count")] int MatchedCount,
    [property: JsonPropertyName("mean_iou")] double? MeanIou
);

public record BenchmarkImageResultDto(
    [property: JsonPropertyName("image_id")] int ImageId,
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("gt_count")] int GtCount,
    [property: JsonPropertyName("pred_count")] int PredCount,
    [property: JsonPropertyName("matched")] int Matched,
    [property: JsonPropertyName("missed")] int Missed,
    [property: JsonPropertyName("false_positives")] int FalsePositives,
    [property: JsonPropertyName("mean_iou")] double? MeanIou
);

public record BenchmarkRunDetailDto(
    string RunId,
    BenchmarkRunMetadataDto? Run,
    BenchmarkMetricsDto? Metrics,
    IReadOnlyList<BenchmarkImageResultDto>? PerImage,
    bool HasPredictions,
    string? Error
);

public record BenchmarkCocoAnnotationDto(
    int Id,
    int ImageId,
    int CategoryId,
    string CategoryName,
    IReadOnlyList<double>? Bbox,
    double? Area,
    JsonElement? Segmentation
);

public record BenchmarkImageComparisonDto(
    int ImageId,
    string FileName,
    int Width,
    int Height,
    BenchmarkImageResultDto? Summary,
    IReadOnlyList<BenchmarkCocoAnnotationDto> GroundTruth,
    IReadOnlyList<BenchmarkCocoAnnotationDto> Predictions
);
