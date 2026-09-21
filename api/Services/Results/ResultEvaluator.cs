using System.Text.Json;
using api.Database.Models;

namespace api.Services.Results;

public interface IResultEvaluator
{
    ResultEvaluation Evaluate(ExtractedValue value, AnalysisThreshold? threshold);
}

public class ResultEvaluator(ILogger<ResultEvaluator> logger) : IResultEvaluator
{
    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public ResultEvaluation Evaluate(ExtractedValue value, AnalysisThreshold? threshold)
    {
        if (threshold is null)
            return new ResultEvaluation(ResultSeverity.NotEvaluated, null);

        var snapshot = Snapshot(threshold);

        if (threshold.MinConfidence is { } floor && !(value.Confidence >= floor))
            return new ResultEvaluation(ResultSeverity.Inconclusive, snapshot);

        var severity = value.ValueKind switch
        {
            ResultValueKind.Boolean => EvaluateBoolean(value, threshold),
            ResultValueKind.Numeric => EvaluateNumeric(value, threshold),
            _ => ResultSeverity.NotEvaluated,
        };

        return new ResultEvaluation(severity, snapshot);
    }

    private static ResultSeverity EvaluateBoolean(ExtractedValue value, AnalysisThreshold threshold)
    {
        if (
            threshold.AlertWhenTrue is not { } alertWhenTrue
            || value.BooleanValue is not { } actual
        )
            return ResultSeverity.NotEvaluated;

        return actual == alertWhenTrue ? ResultSeverity.Alert : ResultSeverity.Ok;
    }

    private ResultSeverity EvaluateNumeric(ExtractedValue value, AnalysisThreshold threshold)
    {
        if (value.NumericValue is not { } actual)
            return ResultSeverity.NotEvaluated;

        var hasAnyBound =
            threshold.LowerAlert.HasValue
            || threshold.LowerWarning.HasValue
            || threshold.UpperWarning.HasValue
            || threshold.UpperAlert.HasValue;

        if (!hasAnyBound)
            return ResultSeverity.NotEvaluated;

        WarnIfBoundsOutOfOrder(threshold);

        // Alert bounds are checked first so a misordered configuration still
        // escalates rather than under-reporting.
        if (threshold.LowerAlert is { } lowerAlert && actual <= lowerAlert)
            return ResultSeverity.Alert;

        if (threshold.UpperAlert is { } upperAlert && actual >= upperAlert)
            return ResultSeverity.Alert;

        if (threshold.LowerWarning is { } lowerWarning && actual <= lowerWarning)
            return ResultSeverity.Warning;

        if (threshold.UpperWarning is { } upperWarning && actual >= upperWarning)
            return ResultSeverity.Warning;

        return ResultSeverity.Ok;
    }

    private void WarnIfBoundsOutOfOrder(AnalysisThreshold threshold)
    {
        var ordered = new[]
        {
            threshold.LowerAlert,
            threshold.LowerWarning,
            threshold.UpperWarning,
            threshold.UpperAlert,
        }
            .Where(bound => bound.HasValue)
            .Select(bound => bound!.Value)
            .ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i] >= ordered[i - 1])
                continue;

            logger.LogWarning(
                "Threshold {ThresholdId} for key '{Key}' has bounds out of order "
                    + "(expected LowerAlert <= LowerWarning <= UpperWarning <= UpperAlert)",
                threshold.Id,
                threshold.Key
            );
            return;
        }
    }

    private static string Snapshot(AnalysisThreshold threshold) =>
        JsonSerializer.Serialize(
            new
            {
                threshold.LowerAlert,
                threshold.LowerWarning,
                threshold.UpperWarning,
                threshold.UpperAlert,
                threshold.AlertWhenTrue,
                threshold.MinConfidence,
                Source = threshold.Source.ToString(),
            },
            SnapshotOptions
        );
}
