namespace api.Controllers.WorkflowNotification;

public class WorkflowStartedNotification
{
    public required string InspectionId { get; set; }
    public required string WorkflowName { get; set; }
}

public class WorkflowResultNotification<T>
{
    public required string InspectionId { get; set; }
    public required T Result { get; set; }
}

public class WorkflowExitedNotification
{
    public required string InspectionId { get; set; }
    public required string WorkflowStatus { get; set; }
    public required string WorkflowFailures { get; set; }
}
