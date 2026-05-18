using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using api.Database.Context;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Api.Test;

/// <summary>
/// Per-test-class helpers for spinning up an isolated PostgreSQL Testcontainer,
/// applying EF Core migrations, and constructing a configured
/// <see cref="TestWebApplicationFactory{TProgram}"/>. Mirrors Flotilla's
/// test-setup conventions.
/// </summary>
public static class TestSetupHelpers
{
    private const string PostgresImage = "postgres:17.6";

    /// <summary>
    /// Start a PostgreSQL container, apply all EF Core migrations, and return
    /// the running container plus its connection string. Caller owns the
    /// container's lifetime.
    /// </summary>
    public static async Task<(
        PostgreSqlContainer Container,
        string ConnectionString
    )> ConfigurePostgreSqlDatabase()
    {
        var container = new PostgreSqlBuilder(PostgresImage).Build();
        await container.StartAsync();

        string connectionString = container.GetConnectionString();
        await using var context = ConfigurePostgreSqlContext(connectionString);
        await context.Database.MigrateAsync();

        return (container, connectionString);
    }

    /// <summary>
    /// Build a <see cref="SaraDbContext"/> bound to the given Npgsql
    /// connection string. Each test should construct its own context.
    /// </summary>
    public static SaraDbContext ConfigurePostgreSqlContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SaraDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)
        );
        return new SaraDbContext(optionsBuilder.Options);
    }

    public static TestWebApplicationFactory<Program> ConfigureWebApplicationFactory(
        string postgresConnectionString
    )
    {
        return new TestWebApplicationFactory<Program>(postgresConnectionString);
    }

    public static HttpClient ConfigureHttpClient(TestWebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("http://localhost:8000"),
            }
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        return client;
    }

    /// <summary>
    /// Poll <paramref name="workFunction"/> until it returns true or
    /// <paramref name="timeoutSeconds"/> elapses.
    /// </summary>
    public static async Task<bool> WaitFor(
        Func<Task<bool>> workFunction,
        double timeoutSeconds = 5,
        double pollIntervalSeconds = 0.1
    )
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await workFunction())
            {
                return true;
            }
            await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds));
        }
        return false;
    }
}
