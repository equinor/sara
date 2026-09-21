using api.Database.Models;

namespace api.Configurations;

public class AnalysisOptions
{
    public const string SectionName = "Analysis";

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
    public List<string> Workflows { get; set; } = [];

    /// <summary>
    /// Declares the result keys this analysis produces, keyed by
    /// <see cref="AnalysisResultValue.Key"/>. Presentation metadata lives here
    /// rather than on every result row, since it is a property of the
    /// (analysis type, key) pair and not of an individual measurement.
    /// </summary>
    public Dictionary<string, ResultKeyConfig> Results { get; set; } = [];
}

public class ResultKeyConfig
{
    public ResultValueKind ValueKind { get; set; } = ResultValueKind.Numeric;

    /// <summary>Canonical unit. See <see cref="ResultUnits"/> for the vocabulary.</summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Seeded onto new <see cref="Analysis"/> rows with
    /// <see cref="ThresholdSource.Config"/>. Without a default, manually
    /// triggered inspections would never alarm.
    /// </summary>
    public ThresholdConfig? DefaultThreshold { get; set; }
}

public class ThresholdConfig
{
    public double? LowerAlert { get; set; }
    public double? LowerWarning { get; set; }
    public double? UpperWarning { get; set; }
    public double? UpperAlert { get; set; }
    public bool? AlertWhenTrue { get; set; }
    public double? MinConfidence { get; set; }
}

/// <summary>
/// Canonical unit vocabulary for <see cref="AnalysisResultValue.Unit"/>.
/// Replaces the previous drift between "percentage", "°C", "" and
/// "bool [isBreach]" (which encoded a type, not a unit).
/// </summary>
public static class ResultUnits
{
    public const string Percent = "percent";
    public const string DegreesCelsius = "degC";
}

public class WorkflowConfig
{
    public required string WorkflowTemplateName { get; set; }

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
