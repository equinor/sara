using System;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Services;
using Api.Test.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services;

public class InspectionRecordServiceTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;

    public async ValueTask InitializeAsync()
    {
        (_container, string cs) = await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(cs);
        _ = _factory.Services;
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

    private async Task<InspectionRecord> CreateInScope(IsarInspectionResultMessage message)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInspectionRecordService>();
        return await service.CreateFromMqttMessage(message);
    }

    [Fact]
    public async Task CreateFromMqttMessage_MessageWithoutPose_PersistsNullPoseFields()
    {
        var message = _db.NewIsarInspectionResultMessage();

        var created = await CreateInScope(message);

        await _context.Entry(created).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Null(created.RobotPose);
        Assert.Null(created.TargetPosition);
    }

    [Fact]
    public async Task CreateFromMqttMessage_MessageWithFullPose_PersistsPoseValues()
    {
        var robotPose = new Pose(
            new Position(1.0f, 2.0f, 3.0f),
            new Orientation(0.1f, 0.2f, 0.3f, 0.4f)
        );
        var targetPosition = new Position(7.0f, 8.0f, 9.0f);
        var message = _db.NewIsarInspectionResultMessage(
            robotPose: robotPose,
            targetPosition: targetPosition
        );

        var created = await CreateInScope(message);

        await _context.Entry(created).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1.0f, created.RobotPose!.Position.X);
        Assert.Equal(0.4f, created.RobotPose.Orientation.W);
        Assert.Equal(8.0f, created.TargetPosition!.Y);
    }
}
