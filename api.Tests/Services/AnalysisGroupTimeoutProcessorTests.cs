using System;
using System.Linq;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using Api.Test.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services;

public class AnalysisGroupTimeoutProcessorTests : IAsyncLifetime
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

    private async Task ProcessTimedOutGroupsInScope()
    {
        using var scope = _factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IAnalysisGroupTimeoutProcessor>();
        await processor.ProcessTimedOutGroups(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProcessTimedOutGroups_GroupNotYetTimedOut_DoesNotMutate()
    {
        var group = await _db.NewAnalysisGroup(timeoutAt: DateTime.UtcNow.AddMinutes(5));

        await ProcessTimedOutGroupsInScope();

        await _context.Entry(group).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisGroupStatus.Pending, group.Status);
    }

    [Fact]
    public async Task ProcessTimedOutGroups_IncompleteGroupPastTimeout_MarksTimedOutAndLeavesDeferredAnalysesUntriggered()
    {
        var group = await _db.NewAnalysisGroup(
            expectedSize: 2,
            timeoutAt: DateTime.UtcNow.AddMinutes(-1)
        );
        var analysis = await _db.NewAnalysis(name: "deferred-analysis", analysisGroup: group);

        await ProcessTimedOutGroupsInScope();

        await _context.Entry(group).ReloadAsync(TestContext.Current.CancellationToken);
        await _context.Entry(analysis).ReloadAsync(TestContext.Current.CancellationToken);
        await _context
            .Entry(analysis)
            .Collection(a => a.Runs)
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisGroupStatus.TimedOut, group.Status);
        Assert.Empty(analysis.Runs);
        Assert.Empty(_factory.ArgoHttpHandler.Requests);
    }

    [Fact]
    public async Task ProcessTimedOutGroups_AlreadyCompletedGroup_IsNoOp()
    {
        var group = await _db.NewAnalysisGroup(
            status: AnalysisGroupStatus.Complete,
            timeoutAt: DateTime.UtcNow.AddMinutes(-1)
        );

        await ProcessTimedOutGroupsInScope();

        await _context.Entry(group).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisGroupStatus.Complete, group.Status);
        Assert.Empty(_factory.ArgoHttpHandler.Requests);
    }
}
