using api.Database.Models;

namespace api.Utilities;

/// <summary>Workflows whose output is the viewable base layer of an inspection (anonymized or passthrough copy).</summary>
public static class VisualizationWorkflows
{
    public static readonly HashSet<string> Types = new(
        AnonymizerWorkflows.Types.Append("copy-raw-to-visualized"),
        StringComparer.OrdinalIgnoreCase
    );

    public static Workflow? FindLatest(InspectionRecord record) =>
        record
            .Analyses.SelectMany(a => a.Runs)
            .SelectMany(r => r.Workflows)
            .Where(w => Types.Contains(w.WorkflowType))
            .OrderByDescending(w => w.CompletedAt ?? w.StartedAt ?? DateTime.MinValue)
            .FirstOrDefault();
}
