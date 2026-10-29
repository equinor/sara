namespace api.Configurations;

public class AnalysisOptions
{
    public const string SectionName = "Analysis";

    // Filled from the Storage section, see Program.cs.
    public Dictionary<string, string> StorageAccounts { get; set; } = [];

    public Dictionary<string, AnalysisConfig> Analyses { get; set; } = [];

    public Dictionary<string, WorkflowConfig> Workflows { get; set; } = [];

    public Dictionary<string, List<string>> DefaultAnalysisByInspectionType { get; set; } = [];

    public Dictionary<
        string,
        Dictionary<string, List<string>>
    > DefaultAnalysisByInspectionTypeAndExtension { get; set; } = [];

    public int AnalysisGroupTimeoutMinutes { get; set; } = 30;

    public int AnalysisGroupTimeoutCheckIntervalSeconds { get; set; } = 60;
}

public class AnalysisConfig
{
    public List<StepConfig> Steps { get; set; } = [];
}

public class StepConfig
{
    public string Workflow { get; set; } = "";

    public string? Id { get; set; }

    public string? Input { get; set; }

    public string StepId => Id ?? Workflow;

    public (string StepId, string Output)? InputReference =>
        Input?.Split('.', 2) is [var stepId, var output] ? (stepId, output) : null;
}

public class WorkflowConfig
{
    public const string ResultOutput = "result";

    public string Template { get; set; } = "";

    public Dictionary<string, OutputConfig> Outputs { get; set; } = [];

    // Members of the `extras` Argo parameter (the analyzer's --extras).
    public Dictionary<string, ValueConfig> Extras { get; set; } = [];

    // Additional top-level Argo parameters. Only fencilla's unused inspectionMetadata; remove with it.
    public Dictionary<string, ValueConfig> Parameters { get; set; } = [];

    public SkipRule? SkipChainIf { get; set; }

    public bool IsGate => SkipChainIf is not null;
}

public class OutputConfig
{
    public string StorageAccount { get; set; } = "";

    public string? Extension { get; set; }

    public bool KeepInputName { get; set; }
}

public enum ValueSource
{
    ReferenceImage,
    ReferencePolygon,
    InspectionMetadata,
}

public class ValueConfig
{
    public string? Output { get; set; }

    public ValueSource? Source { get; set; }

    public string? Value { get; set; }

    public bool Optional { get; set; }
}

public class SkipRule
{
    public required string ResultJsonKeyToCheckForSkipBoolean { get; set; }

    public required string Value { get; set; }
}
