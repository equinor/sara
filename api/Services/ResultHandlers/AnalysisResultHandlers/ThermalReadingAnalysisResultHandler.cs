using api.Configurations;
using api.Database.Models;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using api.Services.Results;

namespace api.Services.ResultHandlers.AnalysisResultHandlers;

public class ThermalReadingAnalysisResultHandler(
    IAnalysisResultRecorder recorder,
    ITimeseriesService timeseriesService,
    ILogger<ThermalReadingAnalysisResultHandler> logger
) : AnalysisResultHandlerBase(recorder, logger)
{
    public override string AnalysisName => "thermal-reading";

    protected override string ResultWorkflowType => "thermal-reading";

    protected override IReadOnlyList<ExtractedValue> ExtractValues(Workflow workflow)
    {
        var result = WorkflowResultHandlerHelpers.DeserializeResult<ThermalReadingResult>(
            workflow,
            logger
        );

        if (result is null)
            return [];

        return
        [
            ExtractedValue.Numeric(
                ResultKeys.Temperature,
                result.Temperature,
                ResultUnits.DegreesCelsius,
                result.Confidence,
                result.Warning,
                workflow.Id
            ),
        ];
    }

    protected override async Task OnValuesRecorded(
        Analysis analysis,
        AnalysisRun analysisRun,
        IReadOnlyList<RecordedResultValue> recorded
    )
    {
        foreach (var row in recorded)
        {
            if (row.Value.Key != ResultKeys.Temperature)
                continue;

            if (row.Value.Severity == ResultSeverity.Inconclusive)
            {
                logger.LogWarning(
                    "Skipping thermal-reading timeseries upload for run {AnalysisRunId}: "
                        + "temperature {Temperature}°C, confidence {Confidence} did not meet the "
                        + "configured minimum",
                    analysisRun.Id,
                    row.Value.NumericValue,
                    row.Value.Confidence
                );
                continue;
            }

            if (row.Value.NumericValue is not { } temperature)
                continue;

            await TryUploadTimeseries(analysisRun, row.InspectionRecord, temperature);
        }
    }

    private async Task TryUploadTimeseries(
        AnalysisRun analysisRun,
        InspectionRecord inspectionRecord,
        double temperature
    )
    {
        var uploadRequest = new TriggerTimeseriesUploadRequest
        {
            Name =
                $"{inspectionRecord.InstallationCode}_{inspectionRecord.Tag}_{inspectionRecord.InspectionDescription?.Replace(" ", "-")}",
            Facility = inspectionRecord.InstallationCode,
            ExternalId = "",
            Description = "ThermalReading",
            Unit = "°C",
            AssetId = inspectionRecord.InstallationCode,
            Value = (float)temperature,
            Timestamp = inspectionRecord.Timestamp ?? DateTime.UtcNow,
            Step = true,
            Metadata = new Dictionary<string, string>
            {
                { "tag_id", inspectionRecord.Tag ?? "" },
                { "inspection_description", inspectionRecord.InspectionDescription ?? "" },
                { "robot_name", inspectionRecord.RobotName ?? "" },
            },
        };

        try
        {
            await timeseriesService.TriggerTimeseriesUpload(uploadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to upload thermal-reading datapoint to Timeseries for run {AnalysisRunId}",
                analysisRun.Id
            );
        }
    }
}
