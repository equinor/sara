using System.Text.Json;
using System.Text.Json.Serialization;
using api.Database.Models;
using api.Services;
using api.Services.ResultHandlers.WorkflowResultHandlers;

namespace api.Controllers.Models;

public class AnalysisResultDto
{
    public Guid AnalysisId { get; set; }

    public string AnalysisType { get; set; } = "";

    public string? Value { get; set; }

    public string? Unit { get; set; }

    public float? Confidence { get; set; } // As percentage (0-100)

    public string? Warning { get; set; }
}

public class WorkflowDto
{
    [JsonConstructor]
#nullable disable
    public WorkflowDto() { }

#nullable enable

    public WorkflowDto(Workflow workflow, IBlobStorageService blobService)
    {
        this.Id = workflow.Id;
        this.StepNumber = workflow.StepNumber;
        this.WorkflowType = workflow.WorkflowType;
        this.Status = workflow.Status;
        this.OutputBlobSAS =
            workflow.OutputBlobStorageLocation != null
                ? blobService.CreateReadSasUri(workflow.OutputBlobStorageLocation).Result
                : null;
        this.Result = GetAnalysisResultDtoFromResultJson(
            workflow.ResultJson,
            workflow.AnalysisRun.AnalysisId,
            workflow.WorkflowType
        );
        this.ResultJson = workflow.ResultJson;
        this.StartedAt = workflow.StartedAt;
        this.CompletedAt = workflow.CompletedAt;
        this.ErrorMessage = workflow.ErrorMessage;
    }

    private static AnalysisResultDto? GetAnalysisResultDtoFromResultJson(
        string? resultJson,
        Guid analysisId,
        string workflowType
    )
    {
        if (resultJson is null)
            return null;

        var jsonOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        switch (workflowType)
        {
            case "fencilla":
            {
                FencillaResult? result;
                try
                {
                    result = JsonSerializer.Deserialize<FencillaResult>(resultJson, jsonOptions);
                }
                catch (JsonException)
                {
                    return null;
                }

                var warning = result?.IsBreak == true ? "Breach detected" : result?.Warning;

                return new AnalysisResultDto
                {
                    AnalysisId = analysisId,
                    AnalysisType = workflowType,
                    Value = result?.IsBreak.ToString(),
                    Unit = "bool [isBreach]",
                    Warning = warning,
                    Confidence = result is null ? null : result.Confidence * 100f,
                };
            }
            case "cloe":
            {
                CLOEResult? result;
                try
                {
                    result = JsonSerializer.Deserialize<CLOEResult>(resultJson, jsonOptions);
                }
                catch (JsonException)
                {
                    return null;
                }

                return new AnalysisResultDto
                {
                    AnalysisId = analysisId,
                    AnalysisType = workflowType,
                    Value = result?.OilLevel is { } oil ? (oil * 100).ToString("F2") : null,
                    Unit = "percentage",
                    Confidence = result?.Confidence is { } c ? c * 100f : null,
                    Warning = result?.Warning,
                };
            }
            case "thermal-reading":
            {
                ThermalReadingResult? result;
                try
                {
                    result = JsonSerializer.Deserialize<ThermalReadingResult>(
                        resultJson,
                        jsonOptions
                    );
                }
                catch (JsonException)
                {
                    return null;
                }

                return new AnalysisResultDto
                {
                    AnalysisId = analysisId,
                    AnalysisType = workflowType,
                    Value = result?.Temperature.ToString("F2"),
                    Unit = "celsius [temperature]",
                    Confidence = result?.Confidence is null ? null : result.Confidence * 100f,
                    Warning = result?.Warning,
                };
            }
            default:
                return null;
        }
    }

    public Guid Id { get; set; }
    public int StepNumber { get; set; }
    public string WorkflowType { get; set; }
    public WorkflowStatus Status { get; set; }
    public Uri? OutputBlobSAS { get; set; }
    public AnalysisResultDto? Result { get; set; }
    public string? ResultJson { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
