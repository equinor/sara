using System.Reflection;
using api.Database.Context;
using Api.Database.Context;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using MQTTnet.Extensions.ManagedClient;
using Npgsql;

namespace api.Configurations;

public static class CustomServiceConfigurations
{
    private const string AzurePostgresScope = "https://ossrdbms-aad.database.windows.net/.default";

    public static TokenCredential CreateCredential(IConfiguration config)
    {
        string? tenantId = config["AzureAd:TenantId"];
        string? clientId = config["AzureAd:ClientId"];
        string? clientSecret = config["AzureAd:ClientSecret"];

        if (string.IsNullOrWhiteSpace(tenantId))
            tenantId = null;
        if (string.IsNullOrWhiteSpace(clientId))
            clientId = null;
        if (string.IsNullOrWhiteSpace(clientSecret))
            clientSecret = null;

        // Fallback to the standard AZURE_* environment variables (read here via
        // IConfiguration's environment-variable provider) when the corresponding
        // AzureAd:* keys are not set in any config source. This matches the
        // convention used by the Azure SDK credentials and keeps env-only
        // configuration (e.g. cloud pods that only set AZURE_TENANT_ID /
        // AZURE_CLIENT_ID via the deployment manifest, or local shells / CI
        // jobs that only export AZURE_CLIENT_SECRET) working without requiring
        // a parallel AzureAd:* entry in appsettings.
        tenantId ??= config["AZURE_TENANT_ID"];
        clientId ??= config["AZURE_CLIENT_ID"];
        clientSecret ??= config["AZURE_CLIENT_SECRET"];

        string[] allowedAuthMethods =
            config.GetSection("AzureAd:AllowedAuthMethods").Get<string[]>() ?? [];
        if (allowedAuthMethods.Length == 0)
        {
            allowedAuthMethods = ["WorkloadIdentity"];
        }

        var credentials = new List<TokenCredential>();
        var activated = new List<string>();

        foreach (string method in allowedAuthMethods)
        {
            if (string.Equals(method, "WorkloadIdentity", StringComparison.OrdinalIgnoreCase))
            {
                var workloadOptions = new WorkloadIdentityCredentialOptions();
                if (!string.IsNullOrWhiteSpace(clientId))
                    workloadOptions.ClientId = clientId;
                if (!string.IsNullOrWhiteSpace(tenantId))
                    workloadOptions.TenantId = tenantId;

                credentials.Add(new WorkloadIdentityCredential(workloadOptions));
                activated.Add("WorkloadIdentityCredential");
            }
            else if (string.Equals(method, "ClientSecret", StringComparison.OrdinalIgnoreCase))
            {
                if (
                    !string.IsNullOrWhiteSpace(tenantId)
                    && !string.IsNullOrWhiteSpace(clientId)
                    && !string.IsNullOrWhiteSpace(clientSecret)
                    && !clientSecret.StartsWith("Fill in", StringComparison.OrdinalIgnoreCase)
                )
                {
                    credentials.Add(new ClientSecretCredential(tenantId, clientId, clientSecret));
                    activated.Add("ClientSecretCredential");
                    Console.WriteLine(
                        $"ClientSecretCredential configured with tenantId='{tenantId}', clientId='{clientId}'."
                    );
                }
                else
                {
                    var missing = new List<string>();
                    if (string.IsNullOrWhiteSpace(tenantId))
                        missing.Add("tenantId");
                    if (string.IsNullOrWhiteSpace(clientId))
                        missing.Add("clientId");
                    if (
                        string.IsNullOrWhiteSpace(clientSecret)
                        || clientSecret.StartsWith("Fill in", StringComparison.OrdinalIgnoreCase)
                    )
                        missing.Add("clientSecret");

                    Console.WriteLine(
                        $"AzureAd:AllowedAuthMethods includes 'ClientSecret' but "
                            + $"{string.Join(", ", missing)} is missing/placeholder; "
                            + "skipping ClientSecretCredential."
                    );
                }
            }
            else if (string.Equals(method, "AzureCliBootstrap", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    "AzureCliBootstrap: using Azure CLI credential to bootstrap Key Vault access. "
                        + "The app registration's client secret will be loaded from Key Vault for subsequent Azure calls."
                );
                credentials.Add(new AzureCliCredential());
                activated.Add("AzureCliCredential (bootstrap)");
            }
            else
            {
                Console.WriteLine(
                    $"Unknown auth method '{method}' in AzureAd:AllowedAuthMethods; "
                        + "expected 'WorkloadIdentity', 'ClientSecret' or 'AzureCliBootstrap'."
                );
            }
        }

        if (credentials.Count == 0)
        {
            throw new InvalidOperationException(
                "No usable Azure credential could be constructed from "
                    + "AzureAd:AllowedAuthMethods. Configure at least one of "
                    + "'WorkloadIdentity', 'ClientSecret' or 'AzureCliBootstrap' (with the required values present)."
            );
        }

        if (credentials.Count == 1)
        {
            Console.WriteLine($"Using {activated[0]} only");
            return credentials[0];
        }

        Console.WriteLine("Using ChainedTokenCredential: " + string.Join(" -> ", activated));
        return new ChainedTokenCredential([.. credentials]);
    }

    public static TokenCredential CreateRuntimeCredential(IConfiguration config)
    {
        string? tenantId = config["AzureAd:TenantId"];
        string? clientId = config["AzureAd:ClientId"];
        string? clientSecret = config["AzureAd:ClientSecret"];

        if (string.IsNullOrWhiteSpace(tenantId))
            tenantId = null;
        if (string.IsNullOrWhiteSpace(clientId))
            clientId = null;
        if (string.IsNullOrWhiteSpace(clientSecret))
            clientSecret = null;

        // See CreateCredential above for rationale: same fallback to the
        // standard AZURE_* environment variables when the AzureAd:* keys are
        // not populated in any other config source.
        tenantId ??= config["AZURE_TENANT_ID"];
        clientId ??= config["AZURE_CLIENT_ID"];
        clientSecret ??= config["AZURE_CLIENT_SECRET"];

        string[] allowedAuthMethods =
            config.GetSection("AzureAd:AllowedAuthMethods").Get<string[]>() ?? [];
        if (allowedAuthMethods.Length == 0)
        {
            allowedAuthMethods = ["WorkloadIdentity"];
        }

        var credentials = new List<TokenCredential>();
        var activated = new List<string>();

        foreach (string method in allowedAuthMethods)
        {
            if (string.Equals(method, "AzureCliBootstrap", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            else if (string.Equals(method, "WorkloadIdentity", StringComparison.OrdinalIgnoreCase))
            {
                var workloadOptions = new WorkloadIdentityCredentialOptions();
                if (!string.IsNullOrWhiteSpace(clientId))
                    workloadOptions.ClientId = clientId;
                if (!string.IsNullOrWhiteSpace(tenantId))
                    workloadOptions.TenantId = tenantId;

                credentials.Add(new WorkloadIdentityCredential(workloadOptions));
                activated.Add("WorkloadIdentityCredential");
            }
            else if (string.Equals(method, "ClientSecret", StringComparison.OrdinalIgnoreCase))
            {
                if (
                    !string.IsNullOrWhiteSpace(tenantId)
                    && !string.IsNullOrWhiteSpace(clientId)
                    && !string.IsNullOrWhiteSpace(clientSecret)
                    && !clientSecret.StartsWith("Fill in", StringComparison.OrdinalIgnoreCase)
                )
                {
                    credentials.Add(new ClientSecretCredential(tenantId, clientId, clientSecret));
                    activated.Add("ClientSecretCredential");
                    Console.WriteLine(
                        $"Runtime ClientSecretCredential configured with tenantId='{tenantId}', clientId='{clientId}'."
                    );
                }
                else
                {
                    Console.WriteLine(
                        "Runtime credential: 'ClientSecret' configured but tenantId, "
                            + "clientId or clientSecret is missing/placeholder; skipping."
                    );
                }
            }
        }

        if (credentials.Count == 0)
        {
            throw new InvalidOperationException(
                "No usable runtime Azure credential could be constructed. "
                    + "Ensure Key Vault has loaded the app registration's client secret, "
                    + "or configure 'WorkloadIdentity' in AzureAd:AllowedAuthMethods."
            );
        }

        if (credentials.Count == 1)
        {
            Console.WriteLine($"Runtime credential: using {activated[0]}");
            return credentials[0];
        }

        Console.WriteLine(
            "Runtime credential: using ChainedTokenCredential: " + string.Join(" -> ", activated)
        );
        return new ChainedTokenCredential([.. credentials]);
    }

    public static IServiceCollection ConfigureDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName
    )
    {
        Console.WriteLine("Configuring Database...");

        if (environmentName.Equals("Test", StringComparison.Ordinal))
        {
            Console.WriteLine(
                "The application is running in a test environment and database "
                    + "configuration is part of the test setup."
            );
            return services;
        }

        bool useInMemoryDatabase = configuration
            .GetSection("Database")
            .GetValue<bool>("UseInMemoryDatabase");

        if (useInMemoryDatabase)
        {
            Console.WriteLine("Using InMemory Database");
            DbContextOptionsBuilder dbBuilder = new DbContextOptionsBuilder<SaraDbContext>();
            string sqlConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = "file::memory:",
                Cache = SqliteCacheMode.Shared,
            }.ToString();

            // In-memory sqlite requires an open connection throughout the whole lifetime of the database
            var connectionToInMemorySqlite = new SqliteConnection(sqlConnectionString);
            connectionToInMemorySqlite.Open();
            dbBuilder.UseSqlite(connectionToInMemorySqlite);

            using var context = new SaraDbContext(dbBuilder.Options);
            context.Database.EnsureCreated();
            InitDb.PopulateDb(context, configuration);

            // Setting splitting behavior explicitly to avoid warning
            services.AddDbContext<SaraDbContext>(options =>
                options.UseSqlite(
                    sqlConnectionString,
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)
                )
            );
        }
        else
        {
            string[] allowedDbAuthMethods =
                configuration.GetSection("Database:AllowedAuthMethods").Get<string[]>() ?? [];
            if (allowedDbAuthMethods.Length == 0)
            {
                allowedDbAuthMethods = ["ConnectionString"];
            }

            Console.WriteLine(
                $"Database auth methods to try (in order): {string.Join(", ", allowedDbAuthMethods)}"
            );

            var errors = new List<(string method, Exception ex)>();
            bool configured = false;

            foreach (string method in allowedDbAuthMethods)
            {
                if (configured)
                    break;

                if (string.Equals(method, "AppRegIdentity", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Console.WriteLine(
                            "Trying AppRegIdentity (Entra ID token) for PostgreSQL..."
                        );
                        ConfigureDatabaseWithAppRegIdentity(services, configuration);
                        Console.WriteLine("AppRegIdentity configured successfully.");
                        configured = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"AppRegIdentity failed: {ex.GetType().Name}: {ex.Message}"
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
                        Console.WriteLine("Trying ConnectionString (Key Vault) for PostgreSQL...");
                        ConfigureDatabaseWithConnectionString(services, configuration);
                        Console.WriteLine("ConnectionString configured successfully.");
                        configured = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"ConnectionString failed: {ex.GetType().Name}: {ex.Message}"
                        );
                        errors.Add(("ConnectionString", ex));
                    }
                }
                else
                {
                    Console.WriteLine(
                        $"Unknown database auth method '{method}' in Database:AllowedAuthMethods; "
                            + "expected 'AppRegIdentity' or 'ConnectionString'."
                    );
                }
            }

            if (!configured)
            {
                var summary = string.Join(
                    "; ",
                    errors.Select(e => $"{e.method}: {e.ex.GetType().Name}: {e.ex.Message}")
                );
                throw new InvalidOperationException(
                    "All database authentication methods failed. "
                        + $"Tried: {string.Join(", ", allowedDbAuthMethods)}. Details: {summary}"
                );
            }
        }

        return services;
    }

    private static void ConfigureDatabaseWithAppRegIdentity(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        var server = configuration["Database:Server"];
        if (string.IsNullOrWhiteSpace(server))
            throw new InvalidOperationException(
                "Database:Server is required for AppRegIdentity auth."
            );
        var postgresDb = configuration["Database:PostgresDatabase"];
        if (string.IsNullOrWhiteSpace(postgresDb))
            throw new InvalidOperationException(
                "Database:PostgresDatabase is required for AppRegIdentity auth."
            );
        var dbUser = configuration["Database:User"];
        if (string.IsNullOrWhiteSpace(dbUser))
            throw new InvalidOperationException(
                "Database:User is required for AppRegIdentity auth."
            );

        var credential = CreateRuntimeCredential(configuration);

        Console.WriteLine("Requesting Entra ID token via credential...");
        var tokenRequestContext = new TokenRequestContext([AzurePostgresScope]);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        AccessToken token;
        try
        {
            token = credential.GetToken(tokenRequestContext, cts.Token);
        }
        catch (OperationCanceledException oce)
        {
            throw new TimeoutException("Timed out acquiring Entra ID token for PostgreSQL.", oce);
        }
        Console.WriteLine("Entra ID token acquired successfully.");

        var baseConnString = new NpgsqlConnectionStringBuilder
        {
            Host = $"{server}.postgres.database.azure.com",
            Database = postgresDb,
            Username = dbUser,
            SslMode = SslMode.VerifyFull,
        }.ToString();

        int databaseTimeout = GetDatabaseTimeout(configuration);

        services.AddDbContext<SaraDbContext>(
            options =>
                options.UseNpgsql(
                    baseConnString,
                    o =>
                    {
                        o.ConfigureDataSource(ds =>
                        {
                            var dsCredential = CreateRuntimeCredential(configuration);
                            ds.UsePeriodicPasswordProvider(
                                async (_, ct) =>
                                {
                                    using var tokenCts = new CancellationTokenSource(
                                        TimeSpan.FromSeconds(5)
                                    );
                                    var accessToken = await dsCredential.GetTokenAsync(
                                        new TokenRequestContext([AzurePostgresScope]),
                                        CancellationTokenSource
                                            .CreateLinkedTokenSource(ct, tokenCts.Token)
                                            .Token
                                    );
                                    return accessToken.Token;
                                },
                                successRefreshInterval: TimeSpan.FromMinutes(55),
                                failureRefreshInterval: TimeSpan.FromSeconds(5)
                            );
                        });
                        o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
                        o.EnableRetryOnFailure();
                        o.CommandTimeout(databaseTimeout);
                    }
                ),
            ServiceLifetime.Transient
        );
    }

    private static void ConfigureDatabaseWithConnectionString(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        string? connection = configuration["Database:postgresConnectionString"];
        if (string.IsNullOrEmpty(connection))
        {
            throw new InvalidOperationException(
                "Database:postgresConnectionString is empty or missing. "
                    + "Ensure the connection string is loaded (e.g. from Azure Key Vault)."
            );
        }

        int databaseTimeout = GetDatabaseTimeout(configuration);

        // Setting splitting behavior explicitly to avoid warning
        services.AddDbContext<SaraDbContext>(
            options =>
                options.UseNpgsql(
                    connection,
                    o =>
                    {
                        o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
                        o.EnableRetryOnFailure();
                        o.CommandTimeout(databaseTimeout);
                    }
                ),
            ServiceLifetime.Transient
        );
    }

    private static int GetDatabaseTimeout(IConfiguration configuration)
    {
        var timeoutValue = configuration["Database:Timeout"];
        if (
            !string.IsNullOrEmpty(timeoutValue) && int.TryParse(timeoutValue, out var parsedTimeout)
        )
        {
            return parsedTimeout;
        }
        return 30;
    }

    public static IServiceCollection ConfigureSwagger(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSwaggerGen(c =>
        {
            // Add Authorization button in UI
            c.AddSecurityDefinition(
                "oauth2",
                new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        AuthorizationCode = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri(
                                $"{configuration["AzureAd:Instance"]}/{configuration["AzureAd:TenantId"]}/oauth2/token"
                            ),
                            AuthorizationUrl = new Uri(
                                $"{configuration["AzureAd:Instance"]}/{configuration["AzureAd:TenantId"]}/oauth2/authorize"
                            ),
                            Scopes = new Dictionary<string, string>
                            {
                                {
                                    $"api://{configuration["AzureAd:ClientId"]}/user_impersonation",
                                    "User Impersonation"
                                },
                            },
                        },
                    },
                }
            );
            // Show which endpoints have authorization in the UI
            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("oauth2", document)] = [],
            });

            // Make swagger use xml comments from functions
            string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
        });

        return services;
    }

    public static IServiceCollection ConfigureMQTT(this IServiceCollection services)
    {
        var factory = new MQTTnet.MqttFactory();
        var mqttClient = factory.CreateManagedMqttClient();

        services.AddSingleton(mqttClient);

        return services;
    }
}
