using api.Configurations;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
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

            string? server = config["Database:Server"];
            string? database = config["Database:Name"];
            string? user = config["Database:User"];

            bool useEntraAuth =
                !string.IsNullOrWhiteSpace(server)
                && !string.IsNullOrWhiteSpace(database)
                && !string.IsNullOrWhiteSpace(user);

            var optionsBuilder = new DbContextOptionsBuilder<SaraDbContext>();

            if (useEntraAuth)
            {
                TokenCredential credential = CustomServiceConfigurations.CreateRuntimeCredential(
                    config
                );

                string baseConnectionString =
                    $"Host={server};Database={database};Username={user};SSL Mode=Require;Trust Server Certificate=true;";

                var dataSourceBuilder = new NpgsqlDataSourceBuilder(baseConnectionString);
                dataSourceBuilder.UsePeriodicPasswordProvider(
                    async (_, ct) =>
                    {
                        var token = await credential
                            .GetTokenAsync(
                                new TokenRequestContext(
                                    ["https://ossrdbms-aad.database.windows.net/.default"]
                                ),
                                ct
                            )
                            .ConfigureAwait(false);
                        return token.Token;
                    },
                    successRefreshInterval: TimeSpan.FromMinutes(50),
                    failureRefreshInterval: TimeSpan.FromSeconds(10)
                );

                var dataSource = dataSourceBuilder.Build();

                // Setting splitting behavior explicitly to avoid warning
                optionsBuilder.UseNpgsql(
                    dataSource,
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)
                );

                return new SaraDbContext(optionsBuilder.Options);
            }

            string? connectionString = config["Database:postgresConnectionString"];

            if (string.IsNullOrEmpty(connectionString))
            {
                string? keyVaultUri =
                    config.GetSection("KeyVault")["VaultUri"]
                    ?? throw new KeyNotFoundException("No key vault in config");

                var keyVault = new SecretClient(
                    new Uri(keyVaultUri),
                    CustomServiceConfigurations.CreateCredential(config)
                );

                connectionString = keyVault
                    .GetSecret("Database--postgresConnectionString")
                    .Value.Value;
            }

            // Setting splitting behavior explicitly to avoid warning
            optionsBuilder.UseNpgsql(
                connectionString,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)
            );

            return new SaraDbContext(optionsBuilder.Options);
        }
    }
}
