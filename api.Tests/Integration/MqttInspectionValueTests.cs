using System;
using System.Linq;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using Api.Test.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task ValueMessage_CreatesInspectionRecord()
    {
        var message = _db.NewIsarInspectionValueMessage();
        message.RobotPose = new Pose(new Position(1, 2, 3), new Orientation(0, 0, 0.6f, 0.8f));
        message.TargetPosition = new Position(4, 5, 6);

        await _factory
            .Services.GetRequiredService<MqttEventHandler>()
            .ProcessIsarInspectionValue(message);

        var record = await _context.InspectionRecords.SingleAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equivalent(
            new
            {
                message.InspectionId,
                message.InstallationCode,
                BlobStorageLocation = message.InspectionDataPath,
                message.RobotPose,
                message.TargetPosition,
            },
            record
        );
    }

    [Fact]
    public async Task ValueMessage_ForwardsTimeseriesRequest()
    {
        var message = _db.NewIsarInspectionValueMessage();

        await _factory
            .Services.GetRequiredService<MqttEventHandler>()
            .ProcessIsarInspectionValue(message);

        var request = Assert.Single(_factory.TimeseriesService.Uploads);
        Assert.Equivalent(
            new
            {
                message.Value,
                message.Unit,
                message.Timestamp,
                Facility = message.InstallationCode,
            },
            request
        );
    }

    private static readonly string[] GasMeasurementTypes =
    [
        "IsobutyleneMeasurement",
        "CO2Measurement",
        "TolueneMeasurement",
        "O2Measurement",
        "COMeasurement",
        "H2SMeasurement",
    ];

    private IsarInspectionValueMessage[] NewXam8000GasMeasurementMessages(
        string inspectionId = "xam-task"
    ) =>
        GasMeasurementTypes
            .Select(
                (type, index) =>
                    _db.NewIsarInspectionValueMessage(
                        inspectionId: inspectionId,
                        inspectionType: type,
                        inspectionDescription: "Xam-8000 gas recording",
                        value: index + 1,
                        unit: type == "O2Measurement" ? "%" : "ppm"
                    )
            )
            .ToArray();

    [Fact]
    public async Task GasMeasurementsSharingInspectionId_AreStoredAsSeparateRecords()
    {
        var messages = NewXam8000GasMeasurementMessages();
        var handler = _factory.Services.GetRequiredService<MqttEventHandler>();

        foreach (var message in messages)
            await handler.ProcessIsarInspectionValue(message);

        var records = await _context
            .InspectionRecords.OrderBy(r => r.CreatedAt)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(GasMeasurementTypes.Length, records.Count);
        Assert.All(records, r => Assert.Equal("xam-task", r.InspectionId));
        Assert.Equal(GasMeasurementTypes, records.Select(r => r.InspectionType));
        Assert.Equal(
            messages.Select(m => m.InspectionDataPath.BlobName),
            records.Select(r => r.BlobStorageLocation.BlobName)
        );
        Assert.Equal(GasMeasurementTypes.Length, _factory.TimeseriesService.Uploads.Count);
        Assert.Equal(
            GasMeasurementTypes.Length,
            _factory.MqttPublisher.InspectionRecordAvailableMessages.Count
        );
        Assert.All(
            _factory.MqttPublisher.InspectionRecordAvailableMessages,
            m => Assert.Equal("xam-task", m.InspectionId)
        );
    }

    [Fact]
    public async Task ReplayedGasMeasurement_IsIgnoredWithoutAffectingOtherTypes()
    {
        var messages = NewXam8000GasMeasurementMessages();
        var handler = _factory.Services.GetRequiredService<MqttEventHandler>();
        foreach (var message in messages)
            await handler.ProcessIsarInspectionValue(message);

        await handler.ProcessIsarInspectionValue(messages[2]);

        Assert.Equal(
            GasMeasurementTypes.Length,
            await _context.InspectionRecords.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            GasMeasurementTypes.Length,
            _factory.MqttPublisher.InspectionRecordAvailableMessages.Count
        );
        Assert.Equal(
            1,
            await _context.InspectionRecords.CountAsync(
                r => r.InspectionType == messages[2].InspectionType,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task ValueMessage_SuccessfulTimeseriesUpload_SetsTimeseriesId()
    {
        var message = _db.NewIsarInspectionValueMessage();
        _factory.TimeseriesService.UploadResult = _ => "ts-id-789";

        await _factory
            .Services.GetRequiredService<MqttEventHandler>()
            .ProcessIsarInspectionValue(message);

        var record = await _context.InspectionRecords.SingleAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equal("ts-id-789", record.TimeseriesId);
    }

    [Fact]
    public async Task ValueMessage_FailedTimeseriesUpload_LeavesTimeseriesIdNullAndStillCreatesRecord()
    {
        var message = _db.NewIsarInspectionValueMessage();
        _factory.TimeseriesService.UploadResult = _ =>
            throw new InvalidOperationException("Simulated upload failure");

        await _factory
            .Services.GetRequiredService<MqttEventHandler>()
            .ProcessIsarInspectionValue(message);

        var record = await _context.InspectionRecords.SingleAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Null(record.TimeseriesId);
    }
}
