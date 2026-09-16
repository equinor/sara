using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using api.Configurations;
using api.Database.Context;
using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Test.Database;

public class DatabaseAuthenticationTests
{
    private const string DirectConnection =
        "Host=localhost;Database=synthetic;Username=synthetic;Password=synthetic-password";

    private sealed class StubCredential(Exception? failure = null) : TokenCredential
    {
        public int SyncCalls { get; private set; }
        public int AsyncCalls { get; private set; }

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken
        )
        {
            SyncCalls++;
            Assert.Equal(
                ["https://ossrdbms-aad.database.windows.net/.default"],
                requestContext.Scopes
            );
            Assert.True(cancellationToken.CanBeCanceled);
            if (failure is not null)
                throw failure;
            return new AccessToken("synthetic-token", DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken
        )
        {
            AsyncCalls++;
            return failure is null
                ? ValueTask.FromResult(
                    new AccessToken("synthetic-token", DateTimeOffset.UtcNow.AddHours(1))
                )
                : ValueTask.FromException<AccessToken>(failure);
        }
    }

    private static Dictionary<string, string?> Settings(string? methods)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Database:Server"] = "synthetic-server",
            ["Database:PostgresDatabase"] = "synthetic-db",
            ["Database:User"] = "synthetic-identity",
            ["Database:postgresConnectionString"] = DirectConnection,
            ["Database:Timeout"] = "42",
        };
        if (methods is not null)
        {
            foreach (
                var (method, index) in methods.Split(',').Select((method, index) => (method, index))
            )
                settings[$"Database:AllowedAuthMethods:{index}"] = method;
        }
        return settings;
    }

    private static IConfiguration Config(string? methods) =>
        new ConfigurationBuilder().AddInMemoryCollection(Settings(methods)).Build();

    private static RelationalOptionsExtension Options(
        IConfiguration config,
        string environment,
        StubCredential credential
    )
    {
        var services = new ServiceCollection();
        services.ConfigureDatabase(config, environment, credential);
        using var provider = services.BuildServiceProvider();
        return provider
            .GetRequiredService<DbContextOptions<SaraDbContext>>()
            .Extensions.OfType<RelationalOptionsExtension>()
            .Single();
    }

    private static void AssertIdentity(
        RelationalOptionsExtension options,
        StubCredential credential
    )
    {
        var connection = new NpgsqlConnectionStringBuilder(options.ConnectionString);
        Assert.Equal("synthetic-server.postgres.database.azure.com", connection.Host);
        Assert.Equal("synthetic-db", connection.Database);
        Assert.Equal("synthetic-identity", connection.Username);
        Assert.True(string.IsNullOrEmpty(connection.Password));
        Assert.DoesNotContain("synthetic-token", options.ConnectionString!);
        Assert.Equal(SslMode.VerifyFull, connection.SslMode);
        Assert.Equal(42, options.CommandTimeout);
        Assert.Equal(1, credential.SyncCalls);
        Assert.Equal(0, credential.AsyncCalls);
    }

    private static InvalidOperationException AssertRestrictedFailure(
        IConfiguration config,
        string environment,
        StubCredential credential
    )
    {
        var services = new ServiceCollection();
        var error = Assert.Throws<InvalidOperationException>(() =>
            services.ConfigureDatabase(config, environment, credential)
        );
        Assert.Contains(environment, error.Message);
        Assert.Contains("Effective methods:", error.Message);
        Assert.Contains("AppRegIdentity is required", error.Message);
        Assert.Contains("no password fallback", error.Message);
        Assert.DoesNotContain("synthetic-password", error.Message);
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(SaraDbContext)
        );
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(DbContextOptions<SaraDbContext>)
        );
        return error;
    }

    [Theory]
    [InlineData("sTaGiNg", "AppRegIdentity,ConnectionString")]
    [InlineData("pRoDuCtIoN", "connectionstring,appregidentity,ConnectionString")]
    public void RestrictedRuntimeAlwaysSelectsIdentity(string environment, string methods)
    {
        var credential = new StubCredential();
        AssertIdentity(Options(Config(methods), environment, credential), credential);
    }

    [Theory]
    [InlineData("Staging", "AppRegIdentity,ConnectionString")]
    [InlineData("Production", "ConnectionString,AppRegIdentity")]
    public void IdentityFailureCannotFallBackToPassword(string environment, string methods)
    {
        var cause = new InvalidOperationException("synthetic-sensitive-provider-details");
        var credential = new StubCredential(cause);
        var error = AssertRestrictedFailure(Config(methods), environment, credential);
        Assert.Same(
            cause,
            Assert.IsType<AggregateException>(error.InnerException).InnerExceptions[0]
        );
        Assert.DoesNotContain(cause.Message, error.Message);
        Assert.Equal(1, credential.SyncCalls);
    }

    [Theory]
    [InlineData("Staging", "Database:Server")]
    [InlineData("Production", "Database:PostgresDatabase")]
    [InlineData("Staging", "Database:User")]
    public void MissingIdentitySettingCannotFallBack(string environment, string setting)
    {
        var settings = Settings("ConnectionString,AppRegIdentity");
        settings.Remove(setting);
        var credential = new StubCredential();
        var error = AssertRestrictedFailure(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            environment,
            credential
        );
        var cause = Assert.IsType<AggregateException>(error.InnerException).InnerExceptions[0];
        Assert.Contains(setting, cause.Message);
        Assert.Equal(0, credential.SyncCalls);
    }

    [Fact]
    public void TokenCancellationRetainsTimeoutAndOriginalCause()
    {
        var cancellation = new OperationCanceledException("synthetic cancellation");
        var error = AssertRestrictedFailure(
            Config("AppRegIdentity,ConnectionString"),
            "Production",
            new StubCredential(cancellation)
        );
        var timeout = Assert.IsType<TimeoutException>(
            Assert.IsType<AggregateException>(error.InnerException).InnerExceptions[0]
        );
        Assert.Same(cancellation, timeout.InnerException);
    }

    [Theory]
    [InlineData("Staging", "ConnectionString")]
    [InlineData("Production", null)]
    [InlineData("Staging", "Unknown,ConnectionString")]
    public void NoSupportedMethodFailsExplicitly(string environment, string? methods)
    {
        var credential = new StubCredential();
        var error = AssertRestrictedFailure(Config(methods), environment, credential);
        Assert.Null(error.InnerException);
        Assert.Equal(0, credential.SyncCalls);
        Assert.Contains(
            methods?.Contains("Unknown") == true
                ? "Effective methods: [Unknown]"
                : "Effective methods: []",
            error.Message
        );
    }

    [Fact]
    public void EmptyMethodArrayDoesNotRestorePasswordDefault()
    {
        using var json = new MemoryStream(
            Encoding.UTF8.GetBytes("""{"Database":{"AllowedAuthMethods":[]}}""")
        );
        var config = new ConfigurationBuilder()
            .AddJsonStream(json)
            .AddInMemoryCollection(Settings(null))
            .Build();
        AssertRestrictedFailure(config, "Production", new StubCredential());
    }

    private static IConfigurationBuilder RealSettings(string environment) =>
        new ConfigurationBuilder()
            .SetBasePath(Path.Combine(AppContext.BaseDirectory, "DatabaseSettings"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environment}.json")
            .AddInMemoryCollection(Settings(null));

    [Fact]
    public void AppendedConnectionStringCannotReplaceIdentity()
    {
        var config = RealSettings("Production")
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:AllowedAuthMethods:2"] = "ConnectionString",
                }
            )
            .Build();
        var credential = new StubCredential();
        AssertIdentity(Options(config, "Production", credential), credential);
    }

    [Fact]
    public void ShorterLaterArrayRetainsIndicesButCannotRestorePassword()
    {
        using var laterJson = new MemoryStream(
            Encoding.UTF8.GetBytes("""{"Database":{"AllowedAuthMethods":["ConnectionString"]}}""")
        );
        var config = RealSettings("Staging")
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:AllowedAuthMethods:2"] = "ConnectionString",
                }
            )
            .AddJsonStream(laterJson)
            .Build();
        Assert.Equal(
            ["ConnectionString", "ConnectionString", "ConnectionString"],
            Assert.IsType<string[]>(
                config.GetSection("Database:AllowedAuthMethods").Get<string[]>()
            )
        );
        AssertRestrictedFailure(config, "Staging", new StubCredential());
    }

    [Theory]
    [InlineData("Local")]
    [InlineData("IntegrationTest")]
    public void OtherEnvironmentsPreserveDirectConnectionAndLegacyDefault(string environment)
    {
        foreach (var methods in new[] { "ConnectionString,AppRegIdentity", null })
        {
            var credential = new StubCredential();
            Assert.Equal(
                DirectConnection,
                Options(Config(methods), environment, credential).ConnectionString
            );
            Assert.Equal(0, credential.SyncCalls);
        }
    }

    [Fact]
    public void DevelopmentPreservesIdentityPriorityAndPasswordFallback()
    {
        var config = Config("AppRegIdentity,ConnectionString");
        var credential = new StubCredential();
        AssertIdentity(Options(config, "Development", credential), credential);
        var failingCredential = new StubCredential(
            new InvalidOperationException("synthetic failure")
        );
        Assert.Equal(
            DirectConnection,
            Options(config, "Development", failingCredential).ConnectionString
        );
        Assert.Equal(1, failingCredential.SyncCalls);
    }

    [Fact]
    public void TestReturnsBeforeAnyDatabaseConfiguration()
    {
        var services = new ServiceCollection();
        var credential = new StubCredential();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:UseInMemoryDatabase"] = "not-a-boolean",
                }
            )
            .Build();
        Assert.Same(services, services.ConfigureDatabase(config, "Test", credential));
        Assert.Empty(services);
        Assert.Equal(0, credential.SyncCalls);
    }

    [Fact]
    public void InMemoryDatabaseBypassesRestrictedPostgresAuthentication()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:UseInMemoryDatabase"] = "true",
                    ["Database:AllowedAuthMethods:0"] = "ConnectionString",
                }
            )
            .Build();
        var credential = new StubCredential();
        var options = Options(config, "Production", credential);
        Assert.Contains("Sqlite", options.GetType().Name);
        Assert.Equal(0, credential.SyncCalls);
        Assert.Equal(0, credential.AsyncCalls);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void DesignTimeDefaultsToAzureCliDespiteLegacyConnectionStringOverride(
        string environment
    )
    {
        var config = RealSettings(environment)
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:AllowedAuthMethods:0"] = "ConnectionString",
                }
            )
            .Build();
        var error = Assert.Throws<InvalidOperationException>(() =>
            DesignTimeContextFactory.CreateDbContext(config)
        );
        Assert.Contains("Migrations:Postgres:Username", error.Message);
    }

    [Theory]
    [InlineData("Local")]
    [InlineData("Development")]
    [InlineData("IntegrationTest")]
    [InlineData("Test")]
    public void DesignTimeSupportsExplicitLocalOrTemporaryConnectionString(string environment)
    {
        var config = RealSettings("Development")
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Migrations:AuthenticationMode"] = "LocalConnectionString",
                    ["ASPNETCORE_ENVIRONMENT"] = environment,
                }
            )
            .Build();
        using var context = DesignTimeContextFactory.CreateDbContext(config);
        Assert.Equal(DirectConnection, context.Database.GetConnectionString());
    }
}
