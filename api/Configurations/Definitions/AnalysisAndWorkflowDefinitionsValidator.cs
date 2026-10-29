namespace api.Configurations;

/// <summary>Checks that workflow and analysis definitions are consistent with each other and with configuration.</summary>
public static class AnalysisAndWorkflowDefinitionsValidator
{
    /// <summary>Checks that definitions refer to each other consistently. Problems are produced lazily.</summary>
    public static IEnumerable<string> ValidateReferences(
        AnalysisAndWorkflowDefinitions definitions
    ) =>
        definitions
            .Workflows.Values.SelectMany(ValidateWorkflowArguments)
            .Concat(
                definitions.Analyses.Values.SelectMany(analysis =>
                    ValidateAnalysis(analysis, definitions.Workflows)
                )
            );

    /// <summary>Checks the definitions against per-environment configuration.</summary>
    public static List<string> ValidateAgainstConfiguration(
        AnalysisAndWorkflowDefinitions definitions,
        AnalysisOptions options
    ) =>
        [
            .. definitions
                .Workflows.Keys.Where(name => !options.Workflows.ContainsKey(name))
                .Select(name => $"Workflow '{name}' has no entry in Analysis:Workflows"),
            .. GetDefaultAnalysisNames(options)
                .Where(name => !definitions.Analyses.ContainsKey(name))
                .Distinct()
                .Select(name => $"Default analysis '{name}' has no definition"),
        ];

    private static IEnumerable<string> GetDefaultAnalysisNames(AnalysisOptions options) =>
        options
            .DefaultAnalysisByInspectionType.Values.Concat(
                options.DefaultAnalysisByInspectionTypeAndExtension.Values.SelectMany(byExtension =>
                    byExtension.Values
                )
            )
            .SelectMany(analyses => analyses);

    private static IEnumerable<string> ValidateWorkflowArguments(WorkflowDefinition workflow) =>
        workflow.Arguments.SelectMany(argument =>
            ValidateArgument(
                workflow,
                argument,
                $"Workflow '{workflow.Name}', argument '{argument.Name}'"
            )
        );

    private static IEnumerable<string> ValidateArgument(
        WorkflowDefinition workflow,
        ArgumentValue value,
        string path
    )
    {
        var valueKinds = new object?[]
        {
            value.Input,
            value.Output,
            value.Value,
            value.Object,
        }.Count(kind => kind is not null);
        if (valueKinds != 1)
        {
            yield return $"{path} must set exactly one of input, output, value or object";
        }

        if (value.Input is not null && !workflow.Inputs.ContainsKey(value.Input))
        {
            yield return $"{path} refers to unknown input '{value.Input}'";
        }

        if (value.Output is not null && !workflow.Outputs.ContainsKey(value.Output))
        {
            yield return $"{path} refers to unknown output '{value.Output}'";
        }

        foreach (var (key, member) in value.Object ?? [])
        {
            foreach (var problem in ValidateArgument(workflow, member, $"{path}.{key}"))
            {
                yield return problem;
            }
        }
    }

    private static IEnumerable<string> ValidateAnalysis(
        AnalysisDefinition analysis,
        IReadOnlyDictionary<string, WorkflowDefinition> workflows
    )
    {
        if (analysis.Steps.Count == 0)
        {
            yield return $"Analysis '{analysis.Name}' has no steps";
        }

        Dictionary<string, WorkflowDefinition?> earlierSteps = new();

        foreach (var step in analysis.Steps)
        {
            var scope = $"Analysis '{analysis.Name}', step '{step.Id}'";
            if (earlierSteps.ContainsKey(step.Id))
            {
                yield return $"{scope} is defined more than once";
                continue;
            }

            workflows.TryGetValue(step.Workflow, out var workflow);
            if (workflow is null)
            {
                yield return $"{scope} uses unknown workflow '{step.Workflow}'";
            }
            else
            {
                foreach (var problem in ValidateInputs(step, workflow, earlierSteps, scope))
                {
                    yield return problem;
                }
            }

            earlierSteps.Add(step.Id, workflow);
        }
    }

    private static IEnumerable<string> ValidateInputs(
        StepDefinition step,
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, WorkflowDefinition?> earlierSteps,
        string scope
    )
    {
        foreach (var name in workflow.Inputs.Keys.Except(step.Inputs.Keys))
        {
            yield return $"{scope} does not bind input '{name}'";
        }

        foreach (var (name, binding) in step.Inputs)
        {
            if (!workflow.Inputs.ContainsKey(name))
            {
                yield return $"{scope} binds '{name}', which '{workflow.Name}' does not declare";
            }

            if (ValidateBinding(binding, earlierSteps) is { } problem)
            {
                yield return $"{scope}, input '{name}' {problem}";
            }
        }
    }

    /// <summary>Returns what is wrong with the binding, or null when it is valid.</summary>
    private static string? ValidateBinding(
        InputBinding binding,
        IReadOnlyDictionary<string, WorkflowDefinition?> earlierSteps
    )
    {
        if ((binding.Source is null) == (binding.FromStep is null))
        {
            return "must set exactly one of source or fromStep";
        }

        if (binding.Source is not null && binding.Output is not null)
        {
            return "sets output, which only applies to fromStep";
        }

        if (binding.Source != InputSourceKind.ReferenceMetadata && binding.Field is not null)
        {
            return "sets field, which only applies to source ReferenceMetadata";
        }

        if (binding.FromStep is null)
        {
            var isReferenceField =
                binding.Field == ReferenceMetadataFields.ReferenceImage
                || binding.Field == ReferenceMetadataFields.Polygon;
            return binding.Source == InputSourceKind.ReferenceMetadata && !isReferenceField
                ? $"needs field {ReferenceMetadataFields.ReferenceImage} or {ReferenceMetadataFields.Polygon} for source ReferenceMetadata"
                : null;
        }

        if (!earlierSteps.TryGetValue(binding.FromStep, out var producer))
        {
            return $"refers to '{binding.FromStep}', which is not an earlier step";
        }

        // The producer's unknown workflow is already reported on that step.
        if (producer is null)
        {
            return null;
        }

        return binding.Output is not null && producer.Outputs.ContainsKey(binding.Output)
            ? null
            : $"refers to unknown output '{binding.Output}' of step '{binding.FromStep}'";
    }
}
