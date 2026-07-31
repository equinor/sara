using System;
using System.Collections.Generic;
using api.Configurations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Test;

/// <summary>
/// Guard rails for the integration-test authentication path.
///
/// The generic OIDC issuer removes Entra ID from the picture entirely, so these
/// tests exist to make sure it stays confined to the IntegrationTest environment
/// and cannot be reached from Development, Staging or Production.
/// </summary>
public class AuthenticationConfigurationsTests
{
    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Api.Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ServiceProvider BuildProvider(string environmentName)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AzureAd:Instance"] = "https://login.microsoftonline.com",
            ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000000",
            ["AzureAd:ClientId"] = "sara-test",
        };

        // Mirrors the appsettings layout: AzureAd:Authority is set only in
        // appsettings.IntegrationTest.json.
        if (environmentName == AuthenticationConfigurations.IntegrationTestEnvironment)
        {
            settings["AzureAd:Authority"] = "http://oauth-mock:8080";
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.ConfigureAuthentication(configuration, new StubHostEnvironment(environmentName));

        return services.BuildServiceProvider();
    }

    private static JwtBearerOptions GetJwtBearerOptions(ServiceProvider provider) =>
        provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void EntraIssuerValidatorIsWiredOutsideIntegrationTest(string environmentName)
    {
        using var provider = BuildProvider(environmentName);

        var options = GetJwtBearerOptions(provider);

        // AddMicrosoftIdentityWebApi installs the Entra-aware issuer validator. If this
        // is ever null outside IntegrationTest, issuer validation has been weakened for
        // a real deployment.
        Assert.NotNull(options.TokenValidationParameters.IssuerValidator);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void AuthorityIsNotRedirectedOutsideIntegrationTest(string environmentName)
    {
        using var provider = BuildProvider(environmentName);

        var options = GetJwtBearerOptions(provider);

        Assert.True(options.RequireHttpsMetadata);
        Assert.DoesNotContain("oauth-mock", options.Authority ?? string.Empty);
    }

    [Fact]
    public void IntegrationTestEnvironmentPointsTokenValidationAtTheMockIssuer()
    {
        using var provider = BuildProvider(AuthenticationConfigurations.IntegrationTestEnvironment);

        var options = GetJwtBearerOptions(provider);

        Assert.Equal("http://oauth-mock:8080", options.Authority);
        Assert.Equal("sara-test", options.Audience);
        Assert.False(options.RequireHttpsMetadata);

        var parameters = options.TokenValidationParameters;
        Assert.True(parameters.ValidateIssuer);
        Assert.True(parameters.ValidateAudience);
        Assert.True(parameters.ValidateLifetime);
        // The Entra-specific validator must be gone, otherwise the mock's issuer would
        // be rejected and instance discovery would hit login.microsoftonline.com.
        Assert.Null(parameters.IssuerValidator);
    }
}
