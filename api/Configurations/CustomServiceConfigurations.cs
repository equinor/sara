using System.Reflection;
using api.Database.Context;
using Api.Database.Context;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using MQTTnet.Extensions.ManagedClient;

namespace api.Configurations;

public static class CustomServiceConfigurations
{
    /// <summary>
    /// Build a <see cref="TokenCredential"/> for authenticating against Azure resources.
    ///
    /// The set of credential types to try is configured via
    /// <c>AzureAd:AllowedAuthMethods</c>, an ordered list whose entries may be
    /// <c>"WorkloadIdentity"</c> and/or <c>"ClientSecret"</c> (case-insensitive). The
    /// order of the list determines the order in which credentials are tried when more
    /// than one method is enabled (i.e. it controls the order inside the resulting
    /// <see cref="ChainedTokenCredential"/>).
    ///
    /// In cloud (AKS with Azure Workload Identity), set
    /// <c>"AllowedAuthMethods": [ "WorkloadIdentity" ]</c> and rely on the standard
    /// <c>AZURE_CLIENT_ID</c>, <c>AZURE_TENANT_ID</c>, <c>AZURE_FEDERATED_TOKEN_FILE</c>
    /// and <c>AZURE_AUTHORITY_HOST</c> environment variables injected by the
    /// azure-workload-identity mutating webhook.
    ///
    /// For local development (and CI) set e.g.
    /// <c>"AllowedAuthMethods": [ "WorkloadIdentity", "ClientSecret" ]</c> together with
    /// <c>AzureAd:ClientSecret</c> (or <c>AZURE_CLIENT_SECRET</c>). When environment
    /// variables are used, the .NET configuration array binding pattern is e.g.
    /// <c>AzureAd__AllowedAuthMethods__0=ClientSecret</c>.
    /// </summary>
    public static TokenCredential CreateCredential(IConfiguration config)
    {
        string? tenantId = config["AzureAd:TenantId"];
        string? clientId = config["AzureAd:ClientId"];
        string? clientSecret = config["AzureAd:ClientSecret"];

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
                }
                else
                {
                    Console.WriteLine(
                        "AzureAd:AllowedAuthMethods includes 'ClientSecret' but tenantId, "
                            + "clientId or clientSecret is missing/placeholder; skipping ClientSecretCredential."
                    );
                }
            }
            else
            {
                Console.WriteLine(
                    $"Unknown auth method '{method}' in AzureAd:AllowedAuthMethods; "
                        + "expected 'WorkloadIdentity' or 'ClientSecret'."
                );
            }
        }

        if (credentials.Count == 0)
        {
            throw new InvalidOperationException(
                "No usable Azure credential could be constructed from "
                    + "AzureAd:AllowedAuthMethods. Configure at least one of "
                    + "'WorkloadIdentity' or 'ClientSecret' (with the required values present)."
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

    public static IServiceCollection ConfigureDatabase(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        bool useInMemoryDatabase = configuration
            .GetSection("Database")
            .GetValue<bool>("UseInMemoryDatabase");

        if (useInMemoryDatabase)
        {
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
            string? connection = configuration["Database:postgresConnectionString"];
            // Setting splitting behavior explicitly to avoid warning
            services.AddDbContext<SaraDbContext>(
                options =>
                    options.UseNpgsql(
                        connection,
                        o =>
                        {
                            o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
                            o.EnableRetryOnFailure();
                        }
                    ),
                ServiceLifetime.Transient
            );
        }
        return services;
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
