namespace api.Configurations;

public class DashboardOptions
{
    public const string SectionName = "Dashboard";

    /// <summary>
    /// A workflow that has been InProgress for longer than this is flagged as stuck.
    /// </summary>
    public int StuckWorkflowThresholdMinutes { get; set; } = 30;
}
