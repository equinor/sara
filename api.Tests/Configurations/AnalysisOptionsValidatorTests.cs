using System.Collections.Generic;
using api.Configurations;
using Xunit;

namespace Api.Test.Configurations;

public class AnalysisOptionsValidatorTests
{
    private static OutputConfig Output(string storageAccount = "visualized") =>
        new() { StorageAccount = storageAccount };

    private static AnalysisOptions ValidOptions() =>
        new()
        {
            StorageAccounts = new() { ["visualized"] = "visstore" },
            Workflows = new()
            {
                ["first"] = new()
                {
                    Template = "first-template",
                    Outputs = new() { ["result"] = Output(), ["extra"] = Output() },
                    Extras = new() { ["location"] = new() { Output = "extra" } },
                },
                ["second"] = new()
                {
                    Template = "second-template",
                    Outputs = new() { ["result"] = Output() },
                    Parameters = new()
                    {
                        ["metadata"] = new() { Source = ValueSource.InspectionMetadata },
                    },
                },
            },
            Analyses = new()
            {
                ["chain"] = new()
                {
                    Steps =
                    [
                        new() { Workflow = "first" },
                        new() { Workflow = "second", Input = "first.extra" },
                    ],
                },
            },
            DefaultAnalysisByInspectionType = new() { ["Image"] = ["chain"] },
        };

    private static string Errors(AnalysisOptions options)
    {
        var result = new AnalysisOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
        return result.FailureMessage;
    }

    [Fact]
    public void Validate_ValidOptions_Succeeds() =>
        Assert.True(new AnalysisOptionsValidator().Validate(null, ValidOptions()).Succeeded);

    [Fact]
    public void Validate_UnknownWorkflowInStep_Fails()
    {
        var options = ValidOptions();
        options.Analyses["chain"].Steps.Add(new() { Workflow = "missing" });

        Assert.Contains("unknown workflow 'missing'", Errors(options));
    }

    [Theory]
    [InlineData("second.result", "not an earlier step")]
    [InlineData("first.missing", "step 'first' has no output 'missing'")]
    [InlineData("first", "must have the form 'step.output'")]
    public void Validate_InvalidInput_Fails(string input, string expected)
    {
        var options = ValidOptions();
        options.Analyses["chain"].Steps[1].Input = input;

        Assert.Contains(expected, Errors(options));
    }

    [Fact]
    public void Validate_DuplicateStepId_Fails()
    {
        var options = ValidOptions();
        options.Analyses["chain"].Steps.Add(new() { Workflow = "first" });

        Assert.Contains("duplicate step id", Errors(options));
    }

    [Fact]
    public void Validate_StorageAccountNotSet_Fails()
    {
        var options = ValidOptions();
        options.StorageAccounts["visualized"] = "";

        Assert.Contains("storage account 'visualized' is not set", Errors(options));
    }

    [Fact]
    public void Validate_MissingResultOutput_Fails()
    {
        var options = ValidOptions();
        options.Workflows["second"].Outputs = new() { ["other"] = Output() };

        Assert.Contains("an output named 'result' is required", Errors(options));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("extra", null, "constant")]
    public void Validate_ValueWithoutExactlyOneSource_Fails(
        string? output,
        ValueSource? source,
        string? value
    )
    {
        var options = ValidOptions();
        options.Workflows["first"].Extras["location"] = new()
        {
            Output = output,
            Source = source,
            Value = value,
        };

        Assert.Contains("set exactly one of Output, Source or Value", Errors(options));
    }

    [Fact]
    public void Validate_ExtraReferencingUnknownOutput_Fails()
    {
        var options = ValidOptions();
        options.Workflows["first"].Extras["location"] = new() { Output = "missing" };

        Assert.Contains("unknown output 'missing'", Errors(options));
    }

    [Fact]
    public void Validate_IncompleteSkipRule_Fails()
    {
        var options = ValidOptions();
        options.Workflows["first"].SkipChainIf = new()
        {
            ResultJsonKeyToCheckForSkipBoolean = "rain",
            Value = "",
        };

        Assert.Contains("SkipChainIf", Errors(options));
    }

    [Fact]
    public void Validate_UnknownDefaultAnalysis_Fails()
    {
        var options = ValidOptions();
        options.DefaultAnalysisByInspectionTypeAndExtension = new()
        {
            ["Video"] = new Dictionary<string, List<string>> { [".mp4"] = ["missing"] },
        };

        Assert.Contains("Default analysis 'missing' is not defined", Errors(options));
    }
}
