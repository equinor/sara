using System;
using System.Collections.Generic;
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

public class WorkflowServiceTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;

    public async ValueTask InitializeAsync()
    {
        (_container, string connectionString) =
            await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(connectionString);
        _ = _factory.Services;
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(connectionString);
        _db = new DatabaseUtilities(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task RetryWorkflow_CreatesNewFullAnalysisRun()
    {
        var record = await _db.NewInspectionRecord();
        var analysis = await _db.NewAnalysis(type: "multi-step-test", inspectionRecords: [record]);
        var oldRun = await _db.NewAnalysisRun(analysis);
        var failed = await _db.NewWorkflow(oldRun, workflowType: "test-workflow-2");
        failed.Status = WorkflowStatus.Failed;
        oldRun.Status = AnalysisRunStatus.Failed;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope
                .ServiceProvider.GetRequiredService<IWorkflowService>()
                .RetryWorkflow(failed.Id);
        }

        var runs = await _context
            .AnalysisRuns.Include(run => run.Workflows)
            .Where(run => run.AnalysisId == analysis.Id)
            .OrderBy(run => run.RunNumber)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, runs.Count);
        Assert.Equal(2, runs[1].Workflows.Count);
        Assert.Equal(AnalysisRunStatus.InProgress, runs[1].Status);
        Assert.Single(_factory.ArgoWorkflowClient.Requests);
        Assert.Equal(2, _factory.ArgoWorkflowClient.Requests[0].Tasks.Count);
    }

    [Fact]
    public async Task RetryWorkflow_UnknownWorkflow_Throws()
    {
        using var scope = _factory.Services.CreateScope();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            scope
                .ServiceProvider.GetRequiredService<IWorkflowService>()
                .RetryWorkflow(Guid.NewGuid())
        );
    }
}
