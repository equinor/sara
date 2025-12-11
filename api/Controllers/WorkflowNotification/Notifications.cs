namespace api.Controllers.WorkflowNotification;

public class WorkflowStartedNotification
{
    public required string InspectionId { get; set; }
    public required string WorkflowName { get; set; }
}

public class WorkflowResultNotification
{
    public required string InspectionId { get; set; }
}

public class AnonymizerWorkflowResultNotification : WorkflowResultNotification
{
    public required bool IsPersonInImage { get; set; }
}

public class CLOEWorkflowResultNotification : WorkflowResultNotification
{
    public required float OilLevel { get; set; }
}

public class FencillaWorkflowResultNotification : WorkflowResultNotification
{
    public required bool IsBreak { get; set; }
    public required float Confidence { get; set; }
}

public class ThermalReadingWorkflowResultNotification : WorkflowResultNotification
{
    public required float Temperature { get; set; }
}

public enum ExitHandlerWorkflowStatus
{
    Succeeded,
    Failed, // Logic errors within the workflow
    Error, // Error outside the control of the workflow
}

public class WorkflowExitedNotification
{
    public required string InspectionId { get; set; }
    public required ExitHandlerWorkflowStatus ExitHandlerWorkflowStatus { get; set; }
    public required string WorkflowFailures { get; set; }
}
