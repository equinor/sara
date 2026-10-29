using Microsoft.Extensions.Options;

namespace api.Configurations;

/// <summary>Checks that every reference between analyses, workflows, outputs and storage accounts resolves.</summary>
public class AnalysisOptionsValidator : IValidateOptions<AnalysisOptions>
{
    public ValidateOptionsResult Validate(string? name, AnalysisOptions options)
    {
        List<string> errors = [];

        foreach (var (workflowName, workflow) in options.Workflows)
        {
            ValidateWorkflow($"Workflows:{workflowName}", workflow, options, errors);
        }

        foreach (var (analysisName, analysis) in options.Analyses)
        {
            ValidateAnalysis($"Analyses:{analysisName}", analysis, options, errors);
        }

        var defaultAnalyses = options
            .DefaultAnalysisByInspectionType.Values.Concat(
                options.DefaultAnalysisByInspectionTypeAndExtension.Values.SelectMany(byExtension =>
                    byExtension.Values
                )
            )
            .SelectMany(analyses => analyses);
        foreach (var analysis in defaultAnalyses.Distinct())
        {
            if (!options.Analyses.ContainsKey(analysis))
            {
                errors.Add($"Default analysis '{analysis}' is not defined under Analyses");
            }
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors.Select(error => $"Analysis:{error}"));
    }

    private static void ValidateWorkflow(
        string path,
        WorkflowConfig workflow,
        AnalysisOptions options,
        List<string> errors
    )
    {
        if (string.IsNullOrWhiteSpace(workflow.Template))
        {
            errors.Add($"{path}: Template is required");
        }

        if (!workflow.Outputs.ContainsKey(WorkflowConfig.ResultOutput))
        {
            errors.Add($"{path}: an output named '{WorkflowConfig.ResultOutput}' is required");
        }

        if (
            workflow.SkipChainIf is { } skipRule
            && (
                string.IsNullOrWhiteSpace(skipRule.ResultJsonKeyToCheckForSkipBoolean)
                || string.IsNullOrWhiteSpace(skipRule.Value)
            )
        )
        {
            errors.Add(
                $"{path}:SkipChainIf: ResultJsonKeyToCheckForSkipBoolean and Value are required"
            );
        }

        foreach (var (outputName, output) in workflow.Outputs)
        {
            if (
                string.IsNullOrWhiteSpace(
                    options.StorageAccounts.GetValueOrDefault(output.StorageAccount)
                )
            )
            {
                errors.Add(
                    $"{path}:Outputs:{outputName}: storage account '{output.StorageAccount}' is not set under Storage"
                );
            }
        }

        foreach (
            var (valueName, value) in workflow
                .Extras.Select(e => ($"Extras:{e.Key}", e.Value))
                .Concat(workflow.Parameters.Select(p => ($"Parameters:{p.Key}", p.Value)))
        )
        {
            object?[] sources = [value.Output, value.Source, value.Value];
            if (sources.Count(source => source is not null) != 1)
            {
                errors.Add($"{path}:{valueName}: set exactly one of Output, Source or Value");
            }
            else if (value.Output is not null && !workflow.Outputs.ContainsKey(value.Output))
            {
                errors.Add($"{path}:{valueName}: unknown output '{value.Output}'");
            }
        }
    }

    private static void ValidateAnalysis(
        string path,
        AnalysisConfig analysis,
        AnalysisOptions options,
        List<string> errors
    )
    {
        if (analysis.Steps.Count == 0)
        {
            errors.Add($"{path}: at least one step is required");
        }

        Dictionary<string, WorkflowConfig> earlierSteps = [];
        foreach (var step in analysis.Steps)
        {
            var stepPath = $"{path}:Steps:{step.StepId}";
            if (!options.Workflows.TryGetValue(step.Workflow, out var workflow))
            {
                errors.Add($"{stepPath}: unknown workflow '{step.Workflow}'");
                continue;
            }

            if (step.Input is not null)
            {
                if (step.InputReference is not { } input)
                {
                    errors.Add($"{stepPath}: Input must have the form 'step.output'");
                }
                else if (!earlierSteps.TryGetValue(input.StepId, out var producer))
                {
                    errors.Add(
                        $"{stepPath}: Input refers to '{input.StepId}', which is not an earlier step"
                    );
                }
                else if (!producer.Outputs.ContainsKey(input.Output))
                {
                    errors.Add($"{stepPath}: step '{input.StepId}' has no output '{input.Output}'");
                }
            }

            if (!earlierSteps.TryAdd(step.StepId, workflow))
            {
                errors.Add($"{stepPath}: duplicate step id, set Id on one of them");
            }
        }
    }
}
