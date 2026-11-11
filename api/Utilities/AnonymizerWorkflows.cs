namespace api.Utilities;

/// <summary>Workflow types whose output is the anonymized version of the inspection.</summary>
public static class AnonymizerWorkflows
{
    public static readonly IReadOnlyList<string> Types =
    [
        "anonymizer",
        "anonymizer-video",
        "anonymizer-acoustic",
    ];

    public static bool IsAnonymizer(string workflowType) =>
        Types.Contains(workflowType, StringComparer.OrdinalIgnoreCase);
}
