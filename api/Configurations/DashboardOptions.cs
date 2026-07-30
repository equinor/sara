namespace api.Configurations;

public class DashboardOptions
{
    public const string SectionName = "Dashboard";

    /// <summary>
    /// A workflow that has been InProgress for longer than this is flagged as stuck.
    /// </summary>
    public int StuckWorkflowThresholdMinutes { get; set; } = 30;

    /// <summary>
    /// Window sizes (in hours) the summary endpoint accepts. Requests outside
    /// this allow-list are rejected to keep trend bucketing bounded.
    /// </summary>
    public int[] AllowedWindowHours { get; set; } = [24, 168, 720];
}
