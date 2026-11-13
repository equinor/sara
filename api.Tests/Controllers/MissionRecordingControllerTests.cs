using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using Api.Test.Database;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Controllers;

public class MissionRecordingControllerTests : IAsyncLifetime
{
    private const string RunId = "3f2b8c1e-9d4a-4e6b-8a7c-1d2e3f4a5b6c";
    private const string RecordingUrl = $"/api/recordings/kaa/missions/{RunId}";

    private const string Manifest = """
        {
          "status": "Expired",
          "gaps": [{ "at": "2026-11-11T10:30:00+00:00", "reason": "Reconnecting" }],
          "cameras": {
            "front": { "cameraId": "front", "key": "front", "status": "Ready", "firstFrameAt": "2026-11-11T10:00:01+00:00" },
            "rear": { "cameraId": "rear", "key": "rear", "status": "Ready" },
            "arm": { "cameraId": "arm", "key": "arm", "status": "Expired", "error": "upload failed" }
          }
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        (_container, string cs) = await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(cs);
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(cs);
        _db = new DatabaseUtilities(_context);
        _client = TestSetupHelpers.ConfigureHttpClient(_factory);
        _factory.BlobStorageService.BlobContent = Manifest;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _context.DisposeAsync();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private async Task NewRecording(string cameraKey, WorkflowStatus? anonymizer)
    {
        var record = await _db.NewInspectionRecord(
            installationCode: "KAA",
            inspectionId: $"{RunId}-{cameraKey}",
            blobContainer: "kaa",
            blobName: $"{RunId}/{cameraKey}.mp4",
            inspectionType: "Video"
        );
        if (anonymizer is null)
            return;

        var run = await _db.NewAnalysisRun(await _db.NewAnalysis("AnonymizeVideo", [record]));
        var workflow = await _db.NewWorkflow(
            run,
            workflowType: "anonymizer-video",
            outputBlobStorageLocation: _db.NewBlobStorageLocation(
                "anonstore",
                "kaa",
                $"anonymized-{cameraKey}.mp4"
            )
        );
        workflow.Status = anonymizer.Value;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetRecording_OnlyProcessedCamerasAreReady()
    {
        await NewRecording("front", WorkflowStatus.Succeeded);
        await NewRecording("rear", WorkflowStatus.InProgress);

        var recording = await _client.GetFromJsonAsync<MissionRecordingResponse>(
            RecordingUrl,
            JsonOptions,
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Expired", recording!.Status);
        Assert.Single(recording.Gaps);
        Assert.Equal("Ready", recording.Cameras["front"].Status);
        Assert.Equal("2026-11-11T10:00:01+00:00", recording.Cameras["front"].FirstFrameAt);
        Assert.Equal("Processing", recording.Cameras["rear"].Status);
        Assert.Equal(
            ("Expired", "upload failed"),
            (recording.Cameras["arm"].Status, recording.Cameras["arm"].Error)
        );
    }

    [Fact]
    public async Task GetRecording_WithoutManifest_IsNotRecorded()
    {
        _factory.BlobStorageService.DownloadNotFound = true;

        var recording = await _client.GetFromJsonAsync<MissionRecordingResponse>(
            RecordingUrl,
            JsonOptions,
            TestContext.Current.CancellationToken
        );

        Assert.Equal("NotRecorded", recording!.Status);
    }

    [Fact]
    public async Task GetAccess_LinksAnonymizedVideo()
    {
        await NewRecording("front", WorkflowStatus.Succeeded);

        var access = await _client.GetFromJsonAsync<RecordingAccessResponse>(
            $"{RecordingUrl}/cameras/front/access",
            JsonOptions,
            TestContext.Current.CancellationToken
        );

        Assert.StartsWith(
            "https://anonstore.blob.core.windows.net/kaa/anonymized-front.mp4?",
            access!.PlaybackUrl.ToString()
        );
        Assert.Contains("rscd=inline", access.PlaybackUrl.Query);
        Assert.Contains(
            Uri.EscapeDataString($"attachment; filename=\"{RunId}-front.mp4\""),
            access.DownloadUrl.Query
        );
        Assert.True(access.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetAccess_BeforeProcessing_IsNotFound()
    {
        await NewRecording("front", anonymizer: null);

        var response = await _client.GetAsync(
            $"{RecordingUrl}/cameras/front/access",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(RecordingUrl, "Role.Observer.KAA", HttpStatusCode.Forbidden)]
    [InlineData("/api/recordings/kaa/missions/run1", "Role.Admin", HttpStatusCode.BadRequest)]
    public async Task Requests_AreRestricted(string url, string roles, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Test-Roles", roles);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }
}
