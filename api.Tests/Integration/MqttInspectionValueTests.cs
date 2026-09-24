using System;
using System.Collections.Generic;
using System.IO;
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
    public async Task ValueMessage_PersistsJsonBackedRecord()
    {
        var message = _db.NewIsarInspectionValueMessage();

        await _factory
            .Services.GetRequiredService<MqttEventHandler>()
            .ProcessIsarInspectionValue(message);

        var record = await _context.InspectionRecords.SingleAsync(
            TestContext.Current.CancellationToken
        );
        var upload = Assert.Single(_factory.BlobStorageService.Uploads);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(message), upload.Content);
        Assert.Equal(
            (
                "teststorage",
                "tst",
                upload.Location.BlobName,
                "application/json",
                message.InspectionId,
                message.InspectionType,
                message.Timestamp
            ),
            (
                record.BlobStorageLocation.StorageAccount,
                record.BlobStorageLocation.BlobContainer,
                record.BlobStorageLocation.BlobName,
                upload.ContentType,
                record.InspectionId,
                record.InspectionType,
                record.Timestamp
            )
        );
    }

    [Fact]
    public async Task ValueMessage_ForwardsUnchangedTimeseriesRequestAfterPersistence()
    {
        var message = _db.NewIsarInspectionValueMessage();
        bool recordExistedAtUpload = false;
        _factory.TimeseriesService.BeforeUpload = async _ =>
            recordExistedAtUpload = await _context.InspectionRecords.AnyAsync(
                r => r.InspectionId == message.InspectionId,
                TestContext.Current.CancellationToken
            );

        await _factory
            .Services.GetRequiredService<MqttEventHandler>()
            .ProcessIsarInspectionValue(message);

        var request = Assert.Single(_factory.TimeseriesService.Uploads);
        Assert.True(recordExistedAtUpload);
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
    [InlineData(true)]
    [InlineData(false)]
    public async Task PersistenceFailure_DoesNotForwardOrSubmitAnalysis(bool failBlobUpload)
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                if (failBlobUpload)
                {
                    var blobService = new Mock<IBlobStorageService>();
                    blobService
                        .Setup(s =>
                            s.UploadBlobAsync(
                                It.IsAny<BlobStorageLocation>(),
                                It.IsAny<Stream>(),
                                It.IsAny<string>()
                            )
                        )
                        .ThrowsAsync(new IOException("Upload failed"));
                    services.AddSingleton(blobService.Object);
                }
                else
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
            .ProcessIsarInspectionValue(_db.NewIsarInspectionValueMessage());

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
