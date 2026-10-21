using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using api.Controllers.Models;
using api.Database.Context;
using Api.Test.Database;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Controllers;

public class FeedbackControllerTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        (_container, string connectionString) =
            await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(connectionString);
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(connectionString);
        _db = new DatabaseUtilities(_context);
        _client = TestSetupHelpers.ConfigureHttpClient(_factory);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _context.DisposeAsync();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Theory]
    [InlineData("Role.Observer.TST", false, HttpStatusCode.OK)]
    [InlineData("Role.Observer.NON", false, HttpStatusCode.Forbidden)]
    [InlineData("Role.Observer.TST", true, HttpStatusCode.Forbidden)]
    [InlineData("Role.Observer.TST,Role.Observer.NON", true, HttpStatusCode.OK)]
    [InlineData("Role.Admin", true, HttpStatusCode.OK)]
    public async Task SubmissionRequiresAccessToEveryInstallation(
        string roles,
        bool mixedInstallations,
        HttpStatusCode expected
    )
    {
        var record = await _db.NewInspectionRecord(installationCode: "TST");
        var other = await _db.NewInspectionRecord(installationCode: "NON", inspectionId: "other");
        var analysis = await _db.NewAnalysis(
            inspectionRecords: mixedInstallations ? [record, other] : [record]
        );
        var run = await _db.NewAnalysisRun(analysis);
        _client.DefaultRequestHeaders.Add("X-Test-Roles", roles);

        using var response = await _client.PutAsJsonAsync(
            $"/api/feedback/analysis-run/{run.Id}",
            new UpsertFeedbackRequest { IsCorrect = true },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(
            expected == HttpStatusCode.OK,
            await _context.AnalysisRunFeedbacks.AnyAsync(
                f => f.AnalysisRunId == run.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Theory]
    [InlineData("Role.Observer.TST", HttpStatusCode.Forbidden)]
    [InlineData("Role.Admin", HttpStatusCode.NoContent)]
    public async Task DeletionRequiresAdmin(string roles, HttpStatusCode expected)
    {
        var record = await _db.NewInspectionRecord();
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var path = $"/api/feedback/analysis-run/{run.Id}";
        using var created = await _client.PutAsJsonAsync(
            path,
            new UpsertFeedbackRequest { IsCorrect = true },
            TestContext.Current.CancellationToken
        );
        created.EnsureSuccessStatusCode();
        _client.DefaultRequestHeaders.Add("X-Test-Roles", roles);

        using var response = await _client.DeleteAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(
            expected == HttpStatusCode.Forbidden,
            await _context.AnalysisRunFeedbacks.AnyAsync(
                f => f.AnalysisRunId == run.Id,
                TestContext.Current.CancellationToken
            )
        );
    }
}
