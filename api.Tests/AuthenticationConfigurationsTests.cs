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
/// Guard rails for the generic OpenID Connect authentication path.
///
/// The path is selected by Authentication:Provider, so unlike an environment name it
/// is reachable anywhere -- deliberately, because local development and deployments
/// outside Azure both need it. These tests therefore assert two separate things: that
/// Entra ID remains the default and its issuer validator is left intact, and that an
/// unencrypted issuer is refused outside the two environments where the issuer is
/// necessarily local.
/// </summary>
public class AuthenticationConfigurationsTests
{
    private const string KeycloakAuthority = "http://keycloak:8080/realms/robotics";

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Api.Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ServiceProvider BuildProvider(
        string environmentName,
        string? provider = null,
        string? authority = null
    )
    {
        var settings = new Dictionary<string, string?>
        {
            ["AzureAd:Instance"] = "https://login.microsoftonline.com",
            ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000000",
            ["AzureAd:ClientId"] = "sara-test",
        };

        if (provider is not null)
        {
            settings[AuthenticationConfigurations.ProviderKey] = provider;
        }

        if (authority is not null)
        {
            settings["AzureAd:Authority"] = authority;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.ConfigureAuthentication(configuration, new StubHostEnvironment(environmentName));

        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildOidcProvider(
        string environmentName,
        string authority = KeycloakAuthority
    ) => BuildProvider(environmentName, AuthenticationConfigurations.OidcProvider, authority);

    private static JwtBearerOptions GetJwtBearerOptions(ServiceProvider provider) =>
        provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("Local")]
    [InlineData("Test")]
    [InlineData(AuthenticationConfigurations.IntegrationTestEnvironment)]
    public void EntraIssuerValidatorSurvivesUnderTheDefaultProvider(string environmentName)
    {
        using var provider = BuildProvider(environmentName);

        var options = GetJwtBearerOptions(provider);

        // AddMicrosoftIdentityWebApi installs the Entra-aware issuer validator. If this
        // is ever null while the provider is EntraId, issuer validation has been
        // weakened for a real deployment.
        Assert.NotNull(options.TokenValidationParameters.IssuerValidator);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("Local")]
    [InlineData(AuthenticationConfigurations.IntegrationTestEnvironment)]
    public void AuthorityIsNotRedirectedUnderTheDefaultProvider(string environmentName)
    {
        using var provider = BuildProvider(environmentName);

        var options = GetJwtBearerOptions(provider);

        Assert.DoesNotContain("keycloak", options.Authority ?? string.Empty);
    }

    [Theory]
    [InlineData("Local")]
    [InlineData(AuthenticationConfigurations.IntegrationTestEnvironment)]
    public void OidcProviderRedirectsTokenValidation(string environmentName)
    {
        using var provider = BuildOidcProvider(environmentName);

        var options = GetJwtBearerOptions(provider);

        Assert.Equal(KeycloakAuthority, options.Authority);
        Assert.Equal("sara-test", options.Audience);
        Assert.False(options.RequireHttpsMetadata);

        var parameters = options.TokenValidationParameters;
        Assert.True(parameters.ValidateIssuer);
        Assert.True(parameters.ValidateAudience);
        Assert.True(parameters.ValidateLifetime);
        // The Entra-specific validator must be gone, otherwise the issuer would be
        // rejected and instance discovery would hit login.microsoftonline.com.
        Assert.Null(parameters.IssuerValidator);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("Test")]
    public void UnencryptedIssuerIsRefusedInDeployedEnvironments(string environmentName)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BuildOidcProvider(environmentName)
        );

        Assert.Contains("must use HTTPS", exception.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void EncryptedIssuerIsAcceptedInDeployedEnvironments(string environmentName)
    {
        using var provider = BuildOidcProvider(
            environmentName,
            "https://keycloak.example.com/realms/robotics"
        );

        var options = GetJwtBearerOptions(provider);

        // Metadata over HTTPS is required, which is the whole point of the rail.
        Assert.True(options.RequireHttpsMetadata);
        Assert.Null(options.TokenValidationParameters.IssuerValidator);
    }

    [Fact]
    public void OidcProviderWithoutAnAuthorityFailsLoudly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BuildProvider("Local", AuthenticationConfigurations.OidcProvider, authority: null)
        );

        Assert.Contains("AzureAd:Authority is required", exception.Message);
    }
}
