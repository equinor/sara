namespace api.Configurations;

public class AnalysisOptions
{
    public const string SectionName = "Analysis";

    public Dictionary<string, WorkflowConfig> Workflows { get; set; } = [];

    public Dictionary<string, List<string>> DefaultAnalysisByInspectionType { get; set; } = [];

    public Dictionary<
        string,
        Dictionary<string, List<string>>
    > DefaultAnalysisByInspectionTypeAndExtension { get; set; } = [];

    public int AnalysisGroupTimeoutMinutes { get; set; } = 30;

    public int AnalysisGroupTimeoutCheckIntervalSeconds { get; set; } = 60;
}

public class WorkflowConfig
{
    public required string OutputStorageAccount { get; set; }
}
