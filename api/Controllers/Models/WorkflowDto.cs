using System.Globalization;
using System.Text.Json.Serialization;
using api.Database.Models;
using api.Services;

namespace api.Controllers.Models;

public class AnalysisResultDto
{
    public Guid AnalysisId { get; set; }

    public string AnalysisType { get; set; } = "";

    public string? Key { get; set; }

    public string? Value { get; set; }

    public string? Unit { get; set; }

    public float? Confidence { get; set; } // As percentage (0-100)

    public string? Warning { get; set; }

    public ResultSeverity Severity { get; set; }

    public DateTime? MeasuredAt { get; set; }

    public static AnalysisResultDto FromResultValue(Guid analysisId, AnalysisResultValue value) =>
        new()
        {
            AnalysisId = analysisId,
            AnalysisType = value.AnalysisType,
            Key = value.Key,
            Value = FormatValue(value),
            Unit = value.Unit,
            Confidence = value.Confidence is { } confidence ? (float)(confidence * 100d) : null,
            Warning = value.ModelMessage,
            Severity = value.Severity,
            MeasuredAt = value.MeasuredAt,
        };

    private static string? FormatValue(AnalysisResultValue value) =>
        value.ValueKind switch
        {
            ResultValueKind.Numeric => value.NumericValue?.ToString(
                "F5",
                CultureInfo.InvariantCulture
            ),
            ResultValueKind.Boolean => value.BooleanValue?.ToString(),
            _ => value.TextValue,
        };
}

public class WorkflowDto
{
    [JsonConstructor]
#nullable disable
    public WorkflowDto() { }

#nullable enable

    // A null blobService skips OutputBlobSAS, which only the workflow endpoints expose.
    public WorkflowDto(Workflow workflow, IBlobStorageService? blobService)
    {
        this.Id = workflow.Id;
        this.AnalysisRunId = workflow.AnalysisRunId;
        this.StepNumber = workflow.StepNumber;
        this.WorkflowType = workflow.WorkflowType;
        this.Status = workflow.Status;
        this.ArgoWorkflowName = workflow.ArgoWorkflowName;
        this.ArgoWorkflowUid = workflow.ArgoWorkflowUid;
        this.ArgoNodeId = workflow.ArgoNodeId;
        this.OutputBlobSAS =
            blobService != null && workflow.OutputBlobStorageLocation != null
                ? blobService.TryCreateReadSasUriAsync(workflow.OutputBlobStorageLocation).Result
                : null;
        this.Result = ResolveResult(workflow);
        this.ResultJson = workflow.ResultJson;
        this.StartedAt = workflow.StartedAt;
        this.CompletedAt = workflow.CompletedAt;
        this.ErrorMessage = workflow.ErrorMessage;
    }

    private static AnalysisResultDto? ResolveResult(Workflow workflow)
    {
        var run = workflow.AnalysisRun;
        if (run is null)
            return null;

        var value = run.ResultValues.FirstOrDefault(candidate =>
            candidate.SourceWorkflowId == workflow.Id
        );

        return value is null ? null : AnalysisResultDto.FromResultValue(run.AnalysisId, value);
    }

    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public int StepNumber { get; set; }
    public string WorkflowType { get; set; }
    public WorkflowStatus Status { get; set; }
    public string? ArgoWorkflowName { get; set; }
    public string? ArgoWorkflowUid { get; set; }
    public string? ArgoNodeId { get; set; }
    public Uri? OutputBlobSAS { get; set; }
    public AnalysisResultDto? Result { get; set; }
    public string? ResultJson { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
