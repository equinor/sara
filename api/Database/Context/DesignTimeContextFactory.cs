using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace api.Database.Context
{
    /// <summary>
    /// This class is not called by anything explicitly, but is used by EF core when adding migrations and updating database.
    /// </summary>
    public class DesignTimeContextFactory : IDesignTimeDbContextFactory<SaraDbContext>
    {
        // We cannot use dependency injection directly in this class, hence the "manual" extraction of the config variables
        // Followed this tutorial: https://blog.tonysneed.com/2018/12/20/idesigntimedbcontextfactory-and-dependency-injection-a-love-story/
        public SaraDbContext CreateDbContext(string[] args)
        {
            // Get environment
            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!;

            string projectPath = Path.Combine(
                Directory.GetParent(Directory.GetCurrentDirectory())!.FullName,
                "api"
            );

            // Build config
            var config = new ConfigurationBuilder()
                .SetBasePath(projectPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return CreateDbContext(config);
        }

        internal static SaraDbContext CreateDbContext(IConfiguration config)
        {
            var migrationMode = config["Migrations:AuthenticationMode"] ?? "AzureCli";
            if (string.Equals(migrationMode, "AzureCli", StringComparison.OrdinalIgnoreCase))
                return CreateAzureCliContext(config);
            if (
                !string.Equals(
                    migrationMode,
                    "LocalConnectionString",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new InvalidOperationException(
                    "Migrations:AuthenticationMode must be 'AzureCli' or 'LocalConnectionString'."
                );
            }

            var environment = config["ASPNETCORE_ENVIRONMENT"];
            if (
                !string.Equals(environment, "Local", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    environment,
                    "IntegrationTest",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                throw new InvalidOperationException(
                    "LocalConnectionString migrations require Local, Development or IntegrationTest."
                );

            var connectionString = config["Database:postgresConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "Database:postgresConnectionString is required for LocalConnectionString migrations."
                );
            var optionsBuilder = new DbContextOptionsBuilder<SaraDbContext>();

            // Setting splitting behavior explicitly to avoid warning
            optionsBuilder.UseNpgsql(
                connectionString,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)
            );

            return new SaraDbContext(optionsBuilder.Options);
        }

        private static SaraDbContext CreateAzureCliContext(IConfiguration config)
        {
            var host = RequiredMigrationSetting(config, "Migrations:Postgres:Host");
            var database = RequiredMigrationSetting(config, "Migrations:Postgres:Database");
            var username = RequiredMigrationSetting(config, "Migrations:Postgres:Username");
            var tenant = RequiredMigrationSetting(config, "AZURE_TENANT_ID");
            if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
            {
                throw new InvalidOperationException(
                    "Migrations:Postgres:Host must be a single DNS hostname or IP address without a port."
                );
            }
            if (!Guid.TryParseExact(tenant, "D", out var tenantId) || tenantId == Guid.Empty)
                throw new InvalidOperationException("AZURE_TENANT_ID must be a tenant GUID.");

            var credentialOptions = new AzureCliCredentialOptions
            {
                TenantId = tenant,
                ProcessTimeout = AzureCliMigrationAuthentication.TokenTimeout,
            };
            var authentication = new AzureCliMigrationAuthentication(
                new AzureCliCredential(credentialOptions)
            );
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(
                new NpgsqlConnectionStringBuilder
                {
                    Host = host,
                    Database = database,
                    Username = username,
                    SslMode = SslMode.VerifyFull,
                }.ToString()
            );
            dataSourceBuilder.UsePasswordProvider(
                _ => authentication.GetPassword(),
                (_, cancellationToken) => authentication.GetPasswordAsync(cancellationToken)
            );
            var dataSource = dataSourceBuilder.Build();
            try
            {
                var options = new DbContextOptionsBuilder<SaraDbContext>().UseNpgsql(
                    dataSource,
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)
                );
                return new SaraDbContext(options.Options, dataSource);
            }
            catch
            {
                dataSource.Dispose();
                throw;
            }
        }

        private static string RequiredMigrationSetting(IConfiguration config, string key)
        {
            var value = config[key];
            if (
                string.IsNullOrWhiteSpace(value)
                || value != value.Trim()
                || value.Any(char.IsControl)
            )
                throw new InvalidOperationException(
                    $"{key} is required and must not contain surrounding whitespace or control characters."
                );
            return value;
        }
    }
}
