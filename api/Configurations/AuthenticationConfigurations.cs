using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace api.Configurations
{
    public static class AuthenticationConfigurations
    {
        /// <summary>
        /// The environment in which the API validates tokens against a generic OpenID
        /// Connect issuer instead of Microsoft Entra ID.
        ///
        /// This exists solely so the armada integration tests can run against a local
        /// mock issuer, with no Entra app registrations and no client secrets, while
        /// still exercising authentication for real.
        ///
        /// Gating on the environment name rather than on a configuration flag is
        /// deliberate: it keeps the generic-issuer path unreachable from Development,
        /// Staging and Production regardless of which environment variables are set.
        /// </summary>
        public const string IntegrationTestEnvironment = "IntegrationTest";

        public static bool UsesGenericOidc(this IHostEnvironment environment) =>
            environment.IsEnvironment(IntegrationTestEnvironment);

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

            if (environment.UsesGenericOidc())
            {
                ConfigureGenericOidcOverrides(services, configuration);
            }

            return services;
        }

        /// <summary>
        /// Points token validation at the mock issuer.
        ///
        /// AddMicrosoftIdentityWebApi installs an AadIssuerValidator, which expects
        /// Entra-shaped issuers and performs instance discovery against
        /// login.microsoftonline.com. That validator is replaced with plain issuer
        /// validation against the mock's discovery document.
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
            IConfiguration configuration
        )
        {
            string authority =
                configuration["AzureAd:Authority"]
                ?? throw new InvalidOperationException(
                    $"AzureAd:Authority is required in the {IntegrationTestEnvironment} environment"
                );
            string audience =
                configuration["AzureAd:ClientId"]
                ?? throw new InvalidOperationException(
                    $"AzureAd:ClientId is required in the {IntegrationTestEnvironment} environment"
                );

            services.Configure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Authority = authority;
                    options.Audience = audience;
                    // The mock issuer is plain HTTP on the test network.
                    options.RequireHttpsMetadata = false;
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
                    // the mock's discovery document instead.
                    parameters.IssuerValidator = null;
                    parameters.ValidIssuers = null;
                }
            );
        }
    }
}
