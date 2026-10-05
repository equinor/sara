using System;
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
}
