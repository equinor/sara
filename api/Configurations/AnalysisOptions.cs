namespace api.Configurations;

public class AnalysisOptions
{
    public const string SectionName = "Analysis";

    public Dictionary<string, AnalysisConfig> Analyses { get; set; } = [];

    public Dictionary<string, WorkflowConfig> Workflows { get; set; } = [];

    public Dictionary<string, List<string>> DefaultAnalysisByFileExtension { get; set; } = [];

    public int AnalysisGroupTimeoutMinutes { get; set; } = 30;

    public int AnalysisGroupTimeoutCheckIntervalSeconds { get; set; } = 60;
}

public class AnalysisConfig
{
    public List<string> Workflows { get; set; } = [];
}

public class WorkflowConfig
{
    public required string TriggerUrl { get; set; }

    public required string OutputStorageAccount { get; set; }

    public required string OutputBlobContainer { get; set; }

    public string? OutputFileExtension { get; set; }

    public bool IsGate { get; set; }

    public SkipRule? SkipChainIf { get; set; }
}

public class SkipRule
{
    public required string ResultJsonKeyToCheckForSkipBoolean { get; set; }

    public required string Value { get; set; }
}
