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
    ///
    /// Supports the same <c>Database:AllowedAuthMethods</c> configuration as the runtime
    /// <see cref="CustomServiceConfigurations.ConfigureDatabase"/>:
    /// <list type="bullet">
    ///   <item>
    ///     <term>AppRegIdentity</term>
    ///     <description>
    ///       Acquires a single Entra ID token and uses it as the PostgreSQL password.
    ///       Requires <c>Database:Server</c>, <c>Database:PostgresDatabase</c> and
    ///       <c>Database:User</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>ConnectionString</term>
    ///     <description>
    ///       Uses <c>Database:postgresConnectionString</c> from config or falls back to
    ///       the <c>Database--postgresConnectionString</c> secret in Azure Key Vault.
    ///     </description>
    ///   </item>
    /// </list>
    /// Defaults to <c>["ConnectionString"]</c> when the list is absent or empty (backward
    /// compatible).
    /// </summary>
    public class DesignTimeContextFactory : IDesignTimeDbContextFactory<SaraDbContext>
    {
        private const string AzurePostgresScope =
            "https://ossrdbms-aad.database.windows.net/.default";

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

            string[] allowedDbAuthMethods =
                config.GetSection("Database:AllowedAuthMethods").Get<string[]>() ?? [];
            if (allowedDbAuthMethods.Length == 0)
            {
                allowedDbAuthMethods = ["ConnectionString"];
            }

            Console.WriteLine(
                $"Design-time DB auth methods to try (in order): {string.Join(", ", allowedDbAuthMethods)}"
            );

            string? connectionString = null;
            var errors = new List<(string method, Exception ex)>();

            foreach (string method in allowedDbAuthMethods)
            {
                if (!string.IsNullOrEmpty(connectionString))
                    break;

                if (string.Equals(method, "AppRegIdentity", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        connectionString = BuildAppRegIdentityConnectionString(config);
                        Console.WriteLine(
                            "Design-time: AppRegIdentity connection string built successfully."
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"Design-time: AppRegIdentity failed: {ex.GetType().Name}: {ex.Message}. "
                                + "Trying next method..."
                        );
                        errors.Add(("AppRegIdentity", ex));
                    }
                }
                else if (
                    string.Equals(method, "ConnectionString", StringComparison.OrdinalIgnoreCase)
                )
                {
                    try
                    {
                        connectionString = ResolveKeyVaultConnectionString(config);
                        Console.WriteLine("Design-time: ConnectionString resolved successfully.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"Design-time: ConnectionString failed: {ex.GetType().Name}: {ex.Message}. "
                                + "Trying next method..."
                        );
                        errors.Add(("ConnectionString", ex));
                    }
                }
                else
                {
                    Console.WriteLine(
                        $"Design-time: Unknown database auth method '{method}' in "
                            + "Database:AllowedAuthMethods; expected 'AppRegIdentity' or 'ConnectionString'."
                    );
                }
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                var summary = string.Join(
                    "; ",
                    errors.Select(e => $"{e.method}: {e.ex.GetType().Name}: {e.ex.Message}")
                );
                throw new InvalidOperationException(
                    "Design-time: All database authentication methods failed. "
                        + $"Tried: {string.Join(", ", allowedDbAuthMethods)}. Details: {summary}"
                );
            }

            var optionsBuilder = new DbContextOptionsBuilder<SaraDbContext>();

            // Setting splitting behavior explicitly to avoid warning
            optionsBuilder.UseNpgsql(
                connectionString,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)
            );

            return new SaraDbContext(optionsBuilder.Options);
        }

        /// <summary>
        /// Acquire a single Entra ID token and build a connection string with it as the password.
        /// Migrations are short-lived so a single token (valid ~1 hour) is sufficient.
        /// </summary>
        private static string BuildAppRegIdentityConnectionString(IConfiguration config)
        {
            var server =
                config["Database:Server"]
                ?? throw new InvalidOperationException(
                    "Database:Server is required for AppRegIdentity auth."
                );
            var postgresDb =
                config["Database:PostgresDatabase"]
                ?? throw new InvalidOperationException(
                    "Database:PostgresDatabase is required for AppRegIdentity auth."
                );
            var dbUser =
                config["Database:User"]
                ?? throw new InvalidOperationException(
                    "Database:User is required for AppRegIdentity auth."
                );

            if (
                string.IsNullOrWhiteSpace(server)
                || string.IsNullOrWhiteSpace(postgresDb)
                || string.IsNullOrWhiteSpace(dbUser)
            )
            {
                throw new InvalidOperationException(
                    "Database:Server, Database:PostgresDatabase and Database:User must all be "
                        + "non-empty for AppRegIdentity auth."
                );
            }

            var credential = CustomServiceConfigurations.CreateCredential(config);

            Console.WriteLine("Design-time: Requesting Entra ID token...");
            var token = credential.GetToken(
                new TokenRequestContext([AzurePostgresScope]),
                CancellationToken.None
            );

            return new NpgsqlConnectionStringBuilder
            {
                Host = $"{server}.postgres.database.azure.com",
                Database = postgresDb,
                Username = dbUser,
                Password = token.Token,
                SslMode = SslMode.VerifyFull,
            }.ToString();
        }

        /// <summary>
        /// Resolve the connection string from config or fall back to Azure Key Vault.
        /// </summary>
        private static string ResolveKeyVaultConnectionString(IConfiguration config)
        {
            string? connectionString = config["Database:postgresConnectionString"];

            if (string.IsNullOrEmpty(connectionString))
            {
                string? keyVaultUri =
                    config.GetSection("KeyVault")["VaultUri"]
                    ?? throw new KeyNotFoundException(
                        "No Key Vault URI configured and Database:postgresConnectionString is empty."
                    );

                var keyVault = new SecretClient(
                    new Uri(keyVaultUri),
                    CustomServiceConfigurations.CreateCredential(config)
                );

                connectionString = keyVault
                    .GetSecret("Database--postgresConnectionString")
                    .Value.Value;
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Database:postgresConnectionString is empty and could not be resolved from Key Vault."
                );
            }

            return connectionString;
        }
    }
}
