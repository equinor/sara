using System.Reflection;
using api.Database.Context;
using Api.Database.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using MQTTnet.Extensions.ManagedClient;

namespace api.Configurations;

public static class CustomServiceConfigurations
{
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
            InitDb.PopulateDb(context);

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
