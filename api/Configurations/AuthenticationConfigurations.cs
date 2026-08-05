using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace api.Configurations
{
    public static class AuthenticationConfigurations
    {
        /// <summary>
        /// Selects which identity provider the API authenticates against.
        ///
        /// <c>EntraId</c> (the default) is Microsoft Entra ID, unchanged.
        /// <c>Oidc</c> is any conformant OpenID Connect issuer -- a Keycloak realm for
        /// local development and for the armada integration tests, and in principle an
        /// operator's own issuer for a deployment outside Azure.
        ///
        /// This is not an authentication bypass. Issuer, audience, signature, lifetime
        /// and role validation all remain enabled; only the issuer differs. What is
        /// gated instead is transport security, by <see cref="AllowsInsecureMetadata"/>.
        /// </summary>
        public const string ProviderKey = "Authentication:Provider";
        public const string EntraIdProvider = "EntraId";
        public const string OidcProvider = "Oidc";

        /// <summary>
        /// The environment used by the armada integration tests. Selects
        /// appsettings.IntegrationTest.json, which turns off Key Vault.
        /// </summary>
        public const string IntegrationTestEnvironment = "IntegrationTest";

        public const string LocalEnvironment = "Local";

        public static bool UsesGenericOidc(this IConfiguration configuration) =>
            string.Equals(
                configuration[ProviderKey],
                OidcProvider,
                StringComparison.OrdinalIgnoreCase
            );

        /// <summary>
        /// Whether a plain-HTTP authority is tolerated. Only where the issuer is
        /// necessarily local: a developer's machine, or the integration test network.
        ///
        /// Everywhere else an <c>http://</c> authority fails at startup, so pointing a
        /// real deployment at an unencrypted issuer is not something that can happen by
        /// leaking an environment variable.
        /// </summary>
        public static bool AllowsInsecureMetadata(this IHostEnvironment environment) =>
            environment.IsEnvironment(LocalEnvironment)
            || environment.IsEnvironment(IntegrationTestEnvironment);

        /// <summary>
        /// Registers JWT bearer authentication.
        /// </summary>
        public static IServiceCollection ConfigureAuthentication(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment
        )
        {
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"))
                .EnableTokenAcquisitionToCallDownstreamApi()
                .AddInMemoryTokenCaches();

            if (configuration.UsesGenericOidc())
            {
                ConfigureGenericOidcOverrides(services, configuration, environment);
            }

            return services;
        }

        /// <summary>
        /// Points token validation at the configured OpenID issuer.
        ///
        /// AddMicrosoftIdentityWebApi installs an AadIssuerValidator, which expects
        /// Entra-shaped issuers and performs instance discovery against
        /// login.microsoftonline.com. That validator is replaced with plain issuer
        /// validation against the issuer's discovery document.
        ///
        /// This is deliberately split across the two options phases:
        ///
        ///   Configure     - the authority and RequireHttpsMetadata, because
        ///                   JwtBearerPostConfigureOptions throws
        ///                   "MetadataAddress or Authority must use HTTPS" for a plain
        ///                   HTTP authority, and it runs before any post-configuration
        ///                   we could register.
        ///   PostConfigure - clearing the issuer validator, because
        ///                   Microsoft.Identity.Web installs it during
        ///                   post-configuration and the last registration wins.
        /// </summary>
        private static void ConfigureGenericOidcOverrides(
            IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment
        )
        {
            string authority =
                configuration["AzureAd:Authority"]
                ?? throw new InvalidOperationException(
                    $"AzureAd:Authority is required when {ProviderKey} is {OidcProvider}"
                );
            string audience =
                configuration["AzureAd:ClientId"]
                ?? throw new InvalidOperationException(
                    $"AzureAd:ClientId is required when {ProviderKey} is {OidcProvider}"
                );

            bool allowInsecureMetadata = environment.AllowsInsecureMetadata();
            if (
                !allowInsecureMetadata
                && !authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new InvalidOperationException(
                    $"AzureAd:Authority must use HTTPS in the {environment.EnvironmentName} "
                        + $"environment, but was '{authority}'. Plain HTTP is only accepted in "
                        + $"the {LocalEnvironment} and {IntegrationTestEnvironment} environments."
                );
            }

            services.Configure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Authority = authority;
                    options.Audience = audience;
                    options.RequireHttpsMetadata = !allowInsecureMetadata;
                }
            );

            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    var parameters = options.TokenValidationParameters;
                    parameters.ValidateIssuer = true;
                    parameters.ValidateAudience = true;
                    parameters.ValidateLifetime = true;
                    parameters.ValidAudience = audience;
                    parameters.ValidAudiences = [audience];
                    // Drop the Entra-specific issuer validator; the issuer is taken from
                    // the provider's discovery document instead.
                    parameters.IssuerValidator = null;
                    parameters.ValidIssuers = null;
                }
            );
        }
    }
}
