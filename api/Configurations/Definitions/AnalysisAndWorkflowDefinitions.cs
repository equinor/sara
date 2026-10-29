namespace api.Configurations;

/// <summary>Every loaded workflow definition and analysis definition, keyed by name.</summary>
public record AnalysisAndWorkflowDefinitions(
    IReadOnlyDictionary<string, WorkflowDefinition> Workflows,
    IReadOnlyDictionary<string, AnalysisDefinition> Analyses
);
