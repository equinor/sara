using System.Collections.Generic;
using System.IO;
using System.Linq;
using api.Database.Context;
using api.MQTT;
using api.Services;
using Api.Test.Mocks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Test;

/// <summary>
/// Test-time <see cref="WebApplicationFactory{TEntryPoint}"/> for the SARA API.
/// Wires the SaraDbContext to a caller-supplied PostgreSQL connection string,
/// replaces <see cref="IMqttPublisherService"/>, <see cref="IEmailService"/>,
/// <see cref="ITimeseriesService"/> and <see cref="IArgoWorkflowSubmitter"/>
/// with recording fakes, and removes background hosted services so the test
/// host does not connect to a real broker, cluster, or SMTP/Omnia endpoint.
/// </summary>
public class TestWebApplicationFactory<TProgram>(string postgresConnectionString)
    : WebApplicationFactory<Program>
    where TProgram : class
{
    private readonly string _postgresConnectionString = postgresConnectionString;

    public RecordingMqttPublisher MqttPublisher { get; } = new();
    public RecordingArgoWorkflowSubmitter ArgoSubmitter { get; } = new();
    public RecordingEmailService EmailService { get; } = new();
    public RecordingTimeseriesService TimeseriesService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        string projectDir = Directory.GetCurrentDirectory();
        string testConfigPath = Path.Combine(projectDir, "appsettings.Test.json");

        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.AddJsonFile(testConfigPath, optional: false);
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Database:UseInMemoryDatabase"] = "false",
                        ["Database:postgresConnectionString"] = _postgresConnectionString,
                    }
                );
            }
        );

        builder.ConfigureTestServices(services =>
        {
            ReplaceDbContext(services);
            ReplaceMqttPublisher(services);
            ReplaceArgoSubmitter(services);
            ReplaceEmailService(services);
            ReplaceTimeseriesService(services);
            ReplaceAuthentication(services);
            RegisterMqttEventHandler(services);
            RemoveHostedServices(services);
        });
    }

    private void ReplaceDbContext(IServiceCollection services)
    {
        var dbContextDescriptors = services
            .Where(d =>
                d.ServiceType == typeof(DbContextOptions<SaraDbContext>)
                || d.ServiceType == typeof(SaraDbContext)
            )
            .ToList();
        foreach (var descriptor in dbContextDescriptors)
        {
            services.Remove(descriptor);
        }

        services.AddDbContext<SaraDbContext>(
            options =>
                options.UseNpgsql(
                    _postgresConnectionString,
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)
                ),
            ServiceLifetime.Transient
        );
    }

    private void ReplaceMqttPublisher(IServiceCollection services)
    {
        var existing = services.Where(d => d.ServiceType == typeof(IMqttPublisherService)).ToList();
        foreach (var descriptor in existing)
        {
            services.Remove(descriptor);
        }
        services.AddSingleton<IMqttPublisherService>(MqttPublisher);
    }

    private void ReplaceArgoSubmitter(IServiceCollection services)
    {
        var existing = services
            .Where(d => d.ServiceType == typeof(IArgoWorkflowSubmitter))
            .ToList();
        foreach (var descriptor in existing)
        {
            services.Remove(descriptor);
        }
        services.AddSingleton<IArgoWorkflowSubmitter>(ArgoSubmitter);
    }

    private void ReplaceEmailService(IServiceCollection services)
    {
        var existing = services.Where(d => d.ServiceType == typeof(IEmailService)).ToList();
        foreach (var descriptor in existing)
        {
            services.Remove(descriptor);
        }
        services.AddSingleton<IEmailService>(EmailService);
    }

    private void ReplaceTimeseriesService(IServiceCollection services)
    {
        var existing = services.Where(d => d.ServiceType == typeof(ITimeseriesService)).ToList();
        foreach (var descriptor in existing)
        {
            services.Remove(descriptor);
        }
        services.AddSingleton<ITimeseriesService>(TimeseriesService);
    }

    private static void RemoveHostedServices(IServiceCollection services)
    {
        var hostedDescriptors = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();
        foreach (var descriptor in hostedDescriptors)
        {
            services.Remove(descriptor);
        }
    }

    private static void ReplaceAuthentication(IServiceCollection services)
    {
        services
            .AddAuthentication(defaultScheme: TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName,
                _ => { }
            );

        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
            options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            options.DefaultScheme = TestAuthHandler.SchemeName;
        });
    }

    /// <summary>
    /// Registers <see cref="MqttEventHandler"/> as a singleton so integration
    /// tests can resolve it and drive the inspection-result pipeline directly.
    /// The handler's constructor subscribes to a static MQTT event; this is a
    /// no-op in tests because <c>MqttService</c> is removed from hosted
    /// services and never raises the event.
    /// </summary>
    private static void RegisterMqttEventHandler(IServiceCollection services)
    {
        services.AddSingleton<MqttEventHandler>();
    }
}
