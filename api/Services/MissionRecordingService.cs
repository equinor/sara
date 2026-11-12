using System.Text.Json;
using api.Configurations;
using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Azure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services;

public interface IMissionRecordingService
{
    Task<MissionRecordingResponse> GetRecording(string installationCode, string runId);

    Task<RecordingAccessResponse?> GetAccess(
        string installationCode,
        string runId,
        string cameraKey
    );
}

/// <summary>
/// Serves ISAR mission recordings: the session manifest from raw storage, and links to
/// each camera's visualization output (anonymized in production). Never links the raw video.
/// </summary>
public class MissionRecordingService(
    SaraDbContext context,
    IBlobStorageService blobStorageService,
    IOptions<AnalysisOptions> analysisOptions
) : IMissionRecordingService
{
    private const string RecordingInspectionType = "Video";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new(
        JsonSerializerDefaults.Web
    );

    public async Task<MissionRecordingResponse> GetRecording(string installationCode, string runId)
    {
        var recording = await DownloadManifest(installationCode, runId);
        if (recording is null)
            return new MissionRecordingResponse { Status = RecordingStatus.NotRecorded };

        var records = await LoadRecords(
            installationCode,
            recording.Cameras.Values.Select(camera => InspectionId(runId, camera.Key))
        );
        foreach (
            var camera in recording.Cameras.Values.Where(camera =>
                camera.Status == RecordingStatus.Ready
            )
        )
        {
            var workflow = records.TryGetValue(InspectionId(runId, camera.Key), out var record)
                ? VisualizationWorkflows.FindLatest(record)
                : null;
            (camera.Status, camera.Error) = workflow switch
            {
                { Status: WorkflowStatus.Succeeded, OutputBlobStorageLocation: not null } => (
                    RecordingStatus.Ready,
                    null
                ),
                { Status: WorkflowStatus.Failed } => (
                    RecordingStatus.Failed,
                    "Processing the recording failed"
                ),
                _ => (RecordingStatus.Processing, null),
            };
        }
        return recording;
    }

    public async Task<RecordingAccessResponse?> GetAccess(
        string installationCode,
        string runId,
        string cameraKey
    )
    {
        var inspectionId = InspectionId(runId, cameraKey);
        var records = await LoadRecords(installationCode, [inspectionId]);
        if (
            !records.TryGetValue(inspectionId, out var record)
            || VisualizationWorkflows.FindLatest(record)
                is not { Status: WorkflowStatus.Succeeded, OutputBlobStorageLocation: { } output }
        )
            return null;

        // Computed before signing, so it never claims a later expiry than the links have.
        var expiresAt = DateTimeOffset.UtcNow.Add(BlobStorageService.SasLifetime);
        var fileName = $"{runId}-{cameraKey}{Path.GetExtension(output.BlobName)}";
        var playbackUrl = await blobStorageService.TryCreateReadSasUriAsync(output, "inline");
        var downloadUrl = await blobStorageService.TryCreateReadSasUriAsync(
            output,
            $"attachment; filename=\"{fileName}\""
        );
        if (playbackUrl is null || downloadUrl is null)
            throw new InvalidOperationException($"Could not sign recording links for {output}");

        return new RecordingAccessResponse
        {
            PlaybackUrl = playbackUrl,
            DownloadUrl = downloadUrl,
            ExpiresAt = expiresAt,
        };
    }

    // Matches the inspection id ISAR publishes for each camera recording.
    private static string InspectionId(string runId, string cameraKey) => $"{runId}-{cameraKey}";

    private async Task<MissionRecordingResponse?> DownloadManifest(
        string installationCode,
        string runId
    )
    {
        var location = new BlobStorageLocation
        {
            StorageAccount =
                analysisOptions.Value.StorageAccounts.GetValueOrDefault("RawStorageAccount")
                ?? throw new InvalidOperationException("Storage:RawStorageAccount is not set"),
            // ISAR writes recordings to the installation's inspection container.
            BlobContainer = installationCode.ToLowerInvariant(),
            BlobName = $"{runId}/session.json",
        };
        try
        {
            using var manifest = await blobStorageService.DownloadBlobAsync(location);
            return await JsonSerializer.DeserializeAsync<MissionRecordingResponse>(
                manifest,
                ManifestJsonOptions
            );
        }
        catch (RequestFailedException error) when (error.Status == 404)
        {
            return null;
        }
    }

    private Task<Dictionary<string, InspectionRecord>> LoadRecords(
        string installationCode,
        IEnumerable<string> inspectionIds
    )
    {
        var ids = inspectionIds.ToList();
        var code = installationCode.ToUpperInvariant();
        return context
            .InspectionRecords.AsNoTracking()
            .Where(record =>
                ids.Contains(record.InspectionId)
                && record.InspectionType == RecordingInspectionType
                && record.InstallationCode.ToUpper() == code
            )
            .Include(record => record.Analyses)
                .ThenInclude(analysis => analysis.Runs)
                    .ThenInclude(run => run.Workflows)
            .ToDictionaryAsync(record => record.InspectionId);
    }
}
