using api.Database.Models;
using api.MQTT;
using api.Utilities;

namespace api.Services;

public record MqttInspectionRecordResult(
    InspectionRecord Record,
    bool IsDuplicate,
    IReadOnlyList<string> IncompleteAnalyses
)
{
    internal static void EnsureEquivalent(
        InspectionRecord record,
        IsarInspectionResultMessage message
    )
    {
        List<string> conflicts = [];
        void Compare<T>(string field, T stored, T incoming)
        {
            if (!EqualityComparer<T>.Default.Equals(stored, incoming))
                conflicts.Add(field);
        }

        Compare(
            nameof(record.FlotillaMissionId),
            record.FlotillaMissionId,
            message.MissionId is null ? null : Sanitize.SanitizeUserInput(message.MissionId)
        );
        Compare(
            nameof(record.InstallationCode),
            record.InstallationCode,
            Sanitize.SanitizeUserInput(message.InstallationCode)
        );
        Compare(
            "StorageAccount",
            record.BlobStorageLocation.StorageAccount,
            message.InspectionDataPath.StorageAccount
        );
        Compare(
            "BlobContainer",
            record.BlobStorageLocation.BlobContainer,
            message.InspectionDataPath.BlobContainer
        );
        Compare(
            "BlobName",
            record.BlobStorageLocation.BlobName,
            message.InspectionDataPath.BlobName
        );
        Compare(nameof(record.InspectionType), record.InspectionType, message.InspectionType);
        Compare(nameof(record.Tag), record.Tag, Sanitize.SanitizeUserInput(message.TagId));
        Compare(
            nameof(record.InspectionDescription),
            record.InspectionDescription,
            Sanitize.SanitizeUserInput(message.InspectionDescription)
        );
        Compare(
            nameof(record.MissionName),
            record.MissionName,
            Sanitize.SanitizeUserInput(message.MissionName)
        );
        Compare(nameof(record.RobotName), record.RobotName, message.RobotName);
        // PostgreSQL timestamps have microsecond precision, whereas DateTime has 100 ns ticks.
        Compare(
            nameof(record.Timestamp),
            record.Timestamp?.ToUniversalTime().Ticks / 10,
            message.Timestamp.ToUniversalTime().Ticks / 10
        );
        Compare(
            "RobotPose.Position",
            Coordinates(record.RobotPose?.Position),
            Coordinates(message.RobotPose?.Position)
        );
        Compare(
            "RobotPose.Orientation",
            Rotation(record.RobotPose?.Orientation),
            Rotation(message.RobotPose?.Orientation)
        );
        Compare(
            nameof(record.TargetPosition),
            Coordinates(record.TargetPosition),
            Coordinates(message.TargetPosition)
        );
        Compare(
            "AnalysisGroupId",
            record.AnalysisGroup?.GroupId,
            message.AnalysisGroup?.AnalysisGroupId
        );
        Compare(
            "AnalysisGroupSize",
            record.AnalysisGroup?.ExpectedSize,
            message.AnalysisGroup?.AnalysisGroupSize
        );

        // Analyses can be added manually or backfilled when a group completes.
        // The original requested set and other unstored MQTT metadata cannot be reconstructed.
        if (
            (message.RequiredAnalysis ?? [])
                .Except(record.Analyses.Select(a => a.AnalysisType))
                .Any()
        )
            conflicts.Add(nameof(message.RequiredAnalysis));

        if (conflicts.Count > 0)
            throw new InvalidOperationException(
                $"Conflicting MQTT inspection result for inspection id {record.InspectionId}; differing fields: {string.Join(", ", conflicts)}"
            );
    }

    private static (float, float, float)? Coordinates(Position? position) =>
        position is null ? null : (position.X, position.Y, position.Z);

    private static (float, float, float, float)? Rotation(Orientation? orientation) =>
        orientation is null ? null : (orientation.X, orientation.Y, orientation.Z, orientation.W);

    internal static IReadOnlyList<string> FindIncompleteAnalyses(
        InspectionRecord record,
        IsarInspectionResultMessage message,
        bool awaitingGroup
    )
    {
        List<string> incomplete = [];
        foreach (
            var analysis in record.Analyses.Where(a =>
                (message.RequiredAnalysis ?? []).Contains(a.AnalysisType)
            )
        )
        {
            // Later user-requested reruns do not change whether ingestion was handled.
            var initialRun = analysis.Runs.OrderBy(r => r.RunNumber).FirstOrDefault();
            if (
                initialRun is null
                && awaitingGroup
                && analysis.AnalysisGroupId == record.AnalysisGroupId
            )
                continue;
            if (initialRun is null)
                incomplete.Add($"{analysis.AnalysisType} ({analysis.Id}): no initial run");
            else if (initialRun.Status is AnalysisRunStatus.Failed or AnalysisRunStatus.Pending)
                incomplete.Add(
                    $"{analysis.AnalysisType} ({analysis.Id}): initial run {initialRun.Id} is {initialRun.Status}"
                );
            else if (
                initialRun.Status == AnalysisRunStatus.InProgress
                && (
                    initialRun.Workflows.Count == 0
                    || initialRun.Workflows.Any(w => string.IsNullOrEmpty(w.ArgoWorkflowUid))
                )
            )
                incomplete.Add(
                    $"{analysis.AnalysisType} ({analysis.Id}): initial run {initialRun.Id} has unconfirmed workflow submission"
                );
        }
        return incomplete;
    }
}
