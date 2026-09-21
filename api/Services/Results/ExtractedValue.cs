using api.Database.Models;

namespace api.Services.Results;

public sealed class ExtractedValue
{
    public required string Key { get; init; }

    public required ResultValueKind ValueKind { get; init; }

    public double? NumericValue { get; init; }

    public bool? BooleanValue { get; init; }

    public string? TextValue { get; init; }

    public string? Unit { get; init; }

    public double? Confidence { get; init; }

    public string? ModelMessage { get; init; }

    public Guid? SourceWorkflowId { get; init; }

    public static ExtractedValue Numeric(
        string key,
        double value,
        string? unit = null,
        double? confidence = null,
        string? modelMessage = null,
        Guid? sourceWorkflowId = null
    ) =>
        new()
        {
            Key = key,
            ValueKind = ResultValueKind.Numeric,
            NumericValue = value,
            Unit = unit,
            Confidence = confidence,
            ModelMessage = modelMessage,
            SourceWorkflowId = sourceWorkflowId,
        };

    public static ExtractedValue Boolean(
        string key,
        bool value,
        double? confidence = null,
        string? modelMessage = null,
        Guid? sourceWorkflowId = null
    ) =>
        new()
        {
            Key = key,
            ValueKind = ResultValueKind.Boolean,
            BooleanValue = value,
            Confidence = confidence,
            ModelMessage = modelMessage,
            SourceWorkflowId = sourceWorkflowId,
        };
}

public sealed record ResultEvaluation(ResultSeverity Severity, string? ThresholdSnapshotJson);
