using System;
using System.IO;
using System.Linq;
using api.Configurations;
using Xunit;

namespace Api.Test.Configurations;

public class AnalysisAndWorkflowDefinitionsLoaderTests
{
    // Fixtures live in api.Tests/Definitions and are copied to the output folder, the same
    // folder the test host loads its definitions from.
    private static string ReadDefinition(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Definitions", relativePath));

    private static readonly string TestWorkflow1 = ReadDefinition("workflows/test-workflow-1.yaml");
    private static readonly string TestWorkflow2 = ReadDefinition("workflows/test-workflow-2.yaml");
    private static readonly string TestGate = ReadDefinition("workflows/test-gate.yaml");
    private static readonly string MultiStepAnalysis = ReadDefinition(
        "analyses/multi-step-test.yaml"
    );
    private static readonly string GatedAnalysis = ReadDefinition(
        "analyses/multi-step-gated-test.yaml"
    );

    private static AnalysisAndWorkflowDefinitions Build(string[] workflows, string[] analyses) =>
        AnalysisAndWorkflowDefinitionsLoader.Build(
            workflows.Select((yaml, i) => ($"workflow{i}.yaml", yaml)),
            analyses.Select((yaml, i) => ($"analysis{i}.yaml", yaml))
        );

    private static string ErrorsOf(string[] workflows, string[] analyses) =>
        Assert.Throws<InvalidOperationException>(() => Build(workflows, analyses)).Message;

    [Fact]
    public void Build_ValidDefinitions_ParsesChainAndGate()
    {
        var definitions = Build([TestWorkflow1, TestGate, TestWorkflow2], [GatedAnalysis]);

        var steps = definitions.Analyses["multi-step-gated-test"].Steps;
        Assert.Equal(["first", "gate", "third"], steps.Select(s => s.Id));
        Assert.Equal(InputSourceKind.InspectionImages, steps[0].Inputs["images"].Source);
        Assert.Equal("first", steps[2].Inputs["images"].FromStep);
        Assert.True(steps[2].Inputs["images"].Required);

        var gate = definitions.Workflows["test-gate"];
        Assert.Equal("test-gate", gate.TemplateName);
        Assert.Equal("skip", gate.SkipChainIf!.ResultJsonKeyToCheckForSkipBoolean);
        Assert.Equal("true", gate.SkipChainIf.Value);
        Assert.Equal(
            ["inputBlobStorageLocations", "outputBlobStorageLocation", "extras"],
            gate.Arguments.Select(a => a.Name)
        );
        Assert.Empty(gate.Arguments[2].Object!);
    }

    [Fact]
    public void Build_UnknownProperty_IsRejected()
    {
        var message = ErrorsOf([TestWorkflow1.Replace("outputs:", "outputz:")], []);

        Assert.Contains("workflow0.yaml", message);
    }

    [Fact]
    public void Build_ReferenceToLaterStep_IsReported()
    {
        var message = ErrorsOf(
            [TestWorkflow1, TestWorkflow2],
            [MultiStepAnalysis.Replace("fromStep: first", "fromStep: second")]
        );

        Assert.Contains("'second', which is not an earlier step", message);
    }

    [Theory]
    [InlineData("workflow: test-workflow-2", "workflow: nope", "unknown workflow 'nope'")]
    [InlineData(
        "images: { fromStep: first, output: result }",
        "other: { source: InspectionImages }",
        "does not bind input 'images'"
    )]
    [InlineData(
        "images: { fromStep: first, output: result }",
        "images: { source: InspectionImages, fromStep: first, output: result }",
        "exactly one of source or fromStep"
    )]
    [InlineData(
        "images: { source: InspectionImages }",
        "images: { source: ReferenceMetadata, field: Nope }",
        "needs field"
    )]
    public void Build_InvalidStep_IsReported(string original, string replacement, string expected)
    {
        var message = ErrorsOf(
            [TestWorkflow1, TestWorkflow2],
            [MultiStepAnalysis.Replace(original, replacement)]
        );

        Assert.Contains(expected, message);
    }

    [Fact]
    public void Build_DuplicateName_IsReported()
    {
        var message = ErrorsOf([TestWorkflow1, TestWorkflow1], []);

        Assert.Contains(
            "workflow1.yaml: 'test-workflow-1' is already defined in workflow0.yaml",
            message
        );
    }

    [Fact]
    public void Build_ArgumentReferencingUnknownInput_IsReported()
    {
        var message = ErrorsOf([TestWorkflow1.Replace("input: images", "input: pictures")], []);

        Assert.Contains("unknown input 'pictures'", message);
    }
}
