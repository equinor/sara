using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Services;
using Api.Test.Database;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Integration;

public class MqttInspectionValueTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;

    public async ValueTask InitializeAsync()
    {
        (_container, string cs) = await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(cs);
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(cs);
        _db = new DatabaseUtilities(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ValueMessage_UsesIsarBlobWithoutUploading()
    {
        var message = _db.NewIsarInspectionValueMessage();
        var blobService = new Mock<IBlobStorageService>(MockBehavior.Strict);
        blobService.Setup(s => s.ExistsAsync(It.IsAny<BlobStorageLocation>())).ReturnsAsync(true);
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(blobService.Object))
        );

        await factory
            .Services.GetRequiredService<MqttEventHandler>()
            .ProcessIsarInspectionValue(message);

        var record = await _context.InspectionRecords.SingleAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equal(
            $"{message.InspectionDataPath.StorageAccount}/{message.InspectionDataPath.BlobContainer}/{message.InspectionDataPath.BlobName}",
            record.BlobStorageLocation.ToString()
        );
    }

    [Fact]
    public async Task ValueMessage_PersistsRobotPoseAndTargetPosition()
    {
        var message = JsonSerializer.Deserialize<IsarInspectionValueMessage>(
            """
            {
              "inspection_id": "position-test", "installation_code": "TST", "inspection_type": "CO2Measurement",
              "blob_storage_data_path": {"storage_account": "acct", "blob_container": "cont", "blob_name": "value.json"},
              "robot_pose": {
                "position": {"x": 1, "y": 2, "z": 3},
                "orientation": {"x": 0, "y": 0, "z": 0.6, "w": 0.8}
              },
              "target_position": {"x": 4, "y": 5, "z": 6}
            }
            """
        )!;

        await _factory
            .Services.GetRequiredService<MqttEventHandler>()
            .ProcessIsarInspectionValue(message);

        var record = await _context.InspectionRecords.SingleAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equivalent(
            new Pose(new Position(1, 2, 3), new Orientation(0, 0, 0.6f, 0.8f)),
            record.RobotPose
        );
        Assert.Equivalent(new Position(4, 5, 6), record.TargetPosition);
    }

    [Fact]
    public async Task ValueMessage_ForwardsUnchangedTimeseriesRequest()
    {
        var message = _db.NewIsarInspectionValueMessage();

        await _factory
            .Services.GetRequiredService<MqttEventHandler>()
            .ProcessIsarInspectionValue(message);

        var request = Assert.Single(_factory.TimeseriesService.Uploads);
        Assert.Equal(
            JsonSerializer.Serialize(
                new TriggerTimeseriesUploadRequest
                {
                    Name = "TST_2E_2N_3U_test-tag_test-robot_CO2-reading",
                    Facility = "TST",
                    ExternalId = "",
                    Description = "CO2Measurement",
                    Unit = "ppm",
                    AssetId = "TST",
                    Value = message.Value,
                    Timestamp = message.Timestamp,
                    Metadata = new Dictionary<string, string>
                    {
                        ["tag_id"] = "test-tag",
                        ["inspection_description"] = "CO2 reading",
                        ["robot_name"] = "test-robot",
                    },
                }
            ),
            JsonSerializer.Serialize(request)
        );
    }

    [Theory]
    [InlineData("null-path")]
    [InlineData("missing-blob")]
    [InlineData("database")]
    public async Task PersistenceFailure_DoesNotForwardOrSubmitAnalysis(string failure)
    {
        var message = _db.NewIsarInspectionValueMessage();
        if (failure == "null-path")
            message.InspectionDataPath = null!;
        _factory.BlobStorageService.BlobExists = failure != "missing-blob";

        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                if (failure == "database")
                {
                    var recordService = new Mock<IInspectionRecordService>();
                    recordService
                        .Setup(s =>
                            s.CreateFromMqttMessage(
                                It.IsAny<IsarInspectionValueMessage>(),
                                It.IsAny<BlobStorageLocation>()
                            )
                        )
                        .ThrowsAsync(new DbUpdateException("Save failed"));
                    services.AddSingleton(recordService.Object);
                }
            })
        );

        await factory
            .Services.GetRequiredService<MqttEventHandler>()
            .ProcessIsarInspectionValue(message);

        Assert.Empty(_factory.TimeseriesService.Uploads);
        Assert.Empty(_factory.ArgoWorkflowClient.Requests);
        Assert.False(
            await _context.InspectionRecords.AnyAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task TimeseriesFailure_StillSubmitsJsonToConfiguredWorkflow()
    {
        _factory.TimeseriesService.BeforeUpload = _ =>
            throw new HttpRequestException("Timeseries unavailable");

        await _factory
            .Services.GetRequiredService<MqttEventHandler>()
            .ProcessIsarInspectionValue(_db.NewIsarInspectionValueMessage());

        var record = await _context.InspectionRecords.SingleAsync(
            TestContext.Current.CancellationToken
        );
        var request = Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal("test-1", request.WorkflowTemplateName);
        Assert.Equal(
            JsonSerializer.Serialize(
                new[] { record.BlobStorageLocation },
                JsonSerializerOptions.Web
            ),
            request.Arguments["inputBlobStorageLocations"]
        );
    }
}
