using System;
using api.Database.Models;
using api.Services.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Test.Services.Results;

public class ResultEvaluatorTests
{
    private readonly ResultEvaluator _evaluator = new(NullLogger<ResultEvaluator>.Instance);

    private static readonly AnalysisThreshold Band = new()
    {
        Key = "temperature",
        LowerAlert = 20,
        LowerWarning = 35,
        UpperWarning = 60,
        UpperAlert = 80,
    };

    private static ExtractedValue Numeric(double value, double? confidence = 1.0) =>
        ExtractedValue.Numeric("temperature", value, "degC", confidence, null, null);

    [Theory]
    [InlineData(10, ResultSeverity.Alert)]
    [InlineData(20, ResultSeverity.Alert)]
    [InlineData(35, ResultSeverity.Warning)]
    [InlineData(50, ResultSeverity.Ok)]
    [InlineData(60, ResultSeverity.Warning)]
    [InlineData(80, ResultSeverity.Alert)]
    public void NumericBandsAreInclusiveAtTheBound(double value, ResultSeverity expected)
    {
        Assert.Equal(expected, _evaluator.Evaluate(Numeric(value), Band).Severity);
    }

    [Fact]
    public void ValueIsNotEvaluatedWhenNoThresholdIsConfigured()
    {
        Assert.Equal(
            ResultSeverity.NotEvaluated,
            _evaluator.Evaluate(Numeric(10), threshold: null).Severity
        );
    }

    [Theory]
    [InlineData(0.5, ResultSeverity.Inconclusive)]
    [InlineData(null, ResultSeverity.Inconclusive)]
    [InlineData(0.99, ResultSeverity.Ok)]
    public void ConfidenceBelowTheFloorIsInconclusiveRatherThanOk(
        double? confidence,
        ResultSeverity expected
    )
    {
        var threshold = new AnalysisThreshold
        {
            Key = "temperature",
            UpperAlert = 80,
            MinConfidence = 0.99,
        };

        Assert.Equal(expected, _evaluator.Evaluate(Numeric(50, confidence), threshold).Severity);
    }

    [Theory]
    [InlineData(true, ResultSeverity.Alert)]
    [InlineData(false, ResultSeverity.Ok)]
    public void BooleanAlarmsOnTheConfiguredSide(bool actual, ResultSeverity expected)
    {
        var threshold = new AnalysisThreshold { Key = "isBreak", AlertWhenTrue = true };
        var value = ExtractedValue.Boolean("isBreak", actual, 1.0, null, null);

        Assert.Equal(expected, _evaluator.Evaluate(value, threshold).Severity);
    }
}
