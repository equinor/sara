using System.Text.Json;
using api.Database.Context;
using api.Database.Models;

namespace api.Services.ResultHandlers.WorkflowResultHandlers;

public interface IWorkflowResultHandler
{
    public string WorkflowType { get; }

    public Task OnWorkflowCompleted(Workflow workflow);
}

internal static class WorkflowResultHandlerHelpers
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static T? DeserializeResult<T>(Workflow workflow, ILogger logger)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(workflow.ResultJson))
        {
            logger.LogWarning(
                "Workflow {WorkflowType} (Id: {WorkflowId}) has no ResultJson",
                workflow.WorkflowType,
                workflow.Id
            );
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(workflow.ResultJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex,
                "Failed to deserialize ResultJson for workflow {WorkflowType} (Id: {WorkflowId}) as {ResultType}: {Json}",
                workflow.WorkflowType,
                workflow.Id,
                typeof(T).Name,
                workflow.ResultJson
            );
            return null;
        }
    }

    public static async Task UploadTimeseriesAndAssignId(
        SaraDbContext context,
        ITimeseriesService timeseriesService,
        InspectionRecord inspectionRecord,
        TriggerTimeseriesUploadRequest uploadRequest,
        Workflow workflow,
        ILogger logger
    )
    {
        try
        {
            var timeseriesId = await timeseriesService.TriggerTimeseriesUpload(uploadRequest);
            if (timeseriesId is null)
                return;

            inspectionRecord.TimeseriesId = timeseriesId;
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to upload {Description} datapoint to Timeseries for workflow {WorkflowId}",
                uploadRequest.Description,
                workflow.Id
            );
        }
    }
}
