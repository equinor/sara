using api.Database.Context;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Services.Results;

public record RecordedResultValue(InspectionRecord InspectionRecord, AnalysisResultValue Value);

public interface IAnalysisResultRecorder
{
    Task<IReadOnlyList<RecordedResultValue>> Record(
        Analysis analysis,
        AnalysisRun run,
        IReadOnlyList<ExtractedValue> values,
        CancellationToken cancellationToken = default
    );
}

public class AnalysisResultRecorder(
    SaraDbContext context,
    IResultEvaluator evaluator,
    ILogger<AnalysisResultRecorder> logger
) : IAnalysisResultRecorder
{
    public async Task<IReadOnlyList<RecordedResultValue>> Record(
        Analysis analysis,
        AnalysisRun run,
        IReadOnlyList<ExtractedValue> values,
        CancellationToken cancellationToken = default
    )
    {
        if (values.Count == 0)
            return [];

        var alreadyRecorded = await context.AnalysisResultValues.AnyAsync(
            candidate => candidate.AnalysisRunId == run.Id,
            cancellationToken
        );

        if (alreadyRecorded)
        {
            logger.LogDebug(
                "Result values already recorded for run {AnalysisRunId} — skipping",
                run.Id
            );
            return [];
        }

        var inspectionRecords = analysis.InspectionRecords;
        if (inspectionRecords.Count == 0)
        {
            logger.LogWarning(
                "Analysis {AnalysisId} has no inspection records — cannot resolve a correlation "
                    + "key, so {ValueCount} result value(s) for run {AnalysisRunId} were discarded",
                analysis.Id,
                values.Count,
                run.Id
            );
            return [];
        }

        if (inspectionRecords.Count > 1)
        {
            logger.LogWarning(
                "Analysis {AnalysisId} is a group analysis ({Count} records) — a single result "
                    + "cannot be attributed to one record, so {ValueCount} result value(s) for run "
                    + "{AnalysisRunId} were discarded. Group-aware extraction is required before "
                    + "grouped results can be recorded.",
                analysis.Id,
                inspectionRecords.Count,
                values.Count,
                run.Id
            );
            return [];
        }

        var inspectionRecord = inspectionRecords[0];

        var thresholdsByKey = analysis
            .Thresholds.GroupBy(threshold => threshold.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase
            );

        var rows = new List<RecordedResultValue>();

        foreach (var value in values)
        {
            thresholdsByKey.TryGetValue(value.Key, out var threshold);
            var evaluation = evaluator.Evaluate(value, threshold);

            rows.Add(
                new RecordedResultValue(
                    inspectionRecord,
                    new AnalysisResultValue
                    {
                        AnalysisRunId = run.Id,
                        SourceWorkflowId = value.SourceWorkflowId,
                        AnalysisType = analysis.AnalysisType,
                        InstallationCode = inspectionRecord.InstallationCode,
                        Tag = inspectionRecord.Tag,
                        InspectionDescription = inspectionRecord.InspectionDescription,
                        CorrelationKey = AnalysisResultValue.BuildCorrelationKey(
                            inspectionRecord.InstallationCode,
                            inspectionRecord.Tag,
                            inspectionRecord.InspectionDescription,
                            analysis.AnalysisType,
                            value.Key
                        ),
                        Key = value.Key,
                        ValueKind = value.ValueKind,
                        NumericValue = value.NumericValue,
                        BooleanValue = value.BooleanValue,
                        TextValue = value.TextValue,
                        Unit = value.Unit,
                        Confidence = value.Confidence,
                        ModelMessage = value.ModelMessage,
                        Severity = evaluation.Severity,
                        ThresholdSnapshotJson = evaluation.ThresholdSnapshotJson,
                        MeasuredAt =
                            inspectionRecord.Timestamp ?? run.CompletedAt ?? DateTime.UtcNow,
                    }
                )
            );
        }

        context.AnalysisResultValues.AddRange(rows.Select(row => row.Value));
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Recorded {RowCount} result value(s) for analysis '{AnalysisType}' run {AnalysisRunId}",
            rows.Count,
            analysis.AnalysisType,
            run.Id
        );

        return rows;
    }
}
