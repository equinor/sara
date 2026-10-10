using System.Diagnostics;
using api.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MigrationRunner;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string serviceName = "sara-migrations";
using var activitySource = new ActivitySource(serviceName);
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddSimpleConsole();

var endpoint = builder.Configuration["OpenTelemetry:OtelExporterOtlpEndpoint"];
if (!string.IsNullOrWhiteSpace(endpoint))
{
    builder
        .Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(serviceName))
        .WithLogging(
            _ => { },
            options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
            }
        )
        .WithTracing(tracing => tracing.AddSource(serviceName))
        .UseOtlpExporter(OtlpExportProtocol.HttpProtobuf, new Uri(endpoint));
}

using var host = builder.Build();
await host.StartAsync();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(serviceName);
var run = Stopwatch.StartNew();
string? migrationId = null;
var appliedChanges = new List<string>();
var appliedCount = 0;
var exitCode = 0;

using (var activity = activitySource.StartActivity("Apply database migrations"))
{
    try
    {
        // The design-time factory preserves the Job's identity-only authentication requirement.
        await using var db = new DesignTimeContextFactory().CreateDbContext(args);
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation(
                "Database already up to date; applied 0 migrations in {DurationMs} ms",
                run.ElapsedMilliseconds
            );
        }
        else
        {
            var assembly = db.GetService<IMigrationsAssembly>();
            foreach (var pendingId in pending)
            {
                migrationId = pendingId;
                var migrationType = assembly.Migrations[pendingId];
                var migration = assembly.CreateMigration(migrationType, db.Database.ProviderName!);
                var changes = MigrationChanges.Describe(migration.UpOperations);
                var elapsed = Stopwatch.StartNew();
                logger.LogInformation(
                    "Applying migration {MigrationId}; changes: {Changes}",
                    pendingId,
                    changes
                );
                await db.Database.MigrateAsync(pendingId);
                logger.LogInformation(
                    "Applied migration {MigrationId} in {DurationMs} ms; changes: {Changes}",
                    pendingId,
                    elapsed.ElapsedMilliseconds,
                    changes
                );
                appliedCount++;
                appliedChanges.Add(changes);
            }
            logger.LogInformation(
                "Migration run succeeded: applied {AppliedCount} migration(s) in {DurationMs} ms; changes: {Changes}",
                appliedCount,
                run.ElapsedMilliseconds,
                string.Join("; ", appliedChanges)
            );
        }
    }
    catch (Exception exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        // EF migration IDs correspond to the checked-in .cs migration files.
        logger.LogError(
            exception,
            "Migration run failed at {MigrationFile} after applying {AppliedCount} migration(s) in {DurationMs} ms",
            migrationId is null ? "before migration selection" : $"{migrationId}.cs",
            appliedCount,
            run.ElapsedMilliseconds
        );
        exitCode = 1;
    }
}

// The Job exits immediately; flush the log provider before stopping the host.
if (
    !string.IsNullOrWhiteSpace(endpoint)
    && !host.Services.GetRequiredService<LoggerProvider>().ForceFlush(5000)
)
{
    Console.Error.WriteLine("Failed to flush migration logs to the OpenTelemetry collector");
}
await host.StopAsync();
return exitCode;
