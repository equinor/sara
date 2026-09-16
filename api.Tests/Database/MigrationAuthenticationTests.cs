using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using api.Database.Context;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Moq;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Xunit;

namespace Api.Test.Database;

public class MigrationAuthenticationTests
{
    private const string Tenant = "11111111-2222-3333-4444-555555555555";
    private const string SensitiveDetails = "synthetic-sensitive-provider-details";

    private sealed class StubCredential(Func<CancellationToken, AccessToken>? acquire = null)
        : TokenCredential
    {
        public int SyncCalls { get; private set; }
        public int AsyncCalls { get; private set; }

        private AccessToken Acquire(
            TokenRequestContext context,
            CancellationToken cancellationToken
        )
        {
            Assert.Equal(["https://ossrdbms-aad.database.windows.net/.default"], context.Scopes);
            Assert.True(cancellationToken.CanBeCanceled);
            return acquire is null
                ? new AccessToken(
                    $"synthetic-token-{SyncCalls + AsyncCalls}",
                    DateTimeOffset.UtcNow.AddHours(1)
                )
                : acquire(cancellationToken);
        }

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken
        )
        {
            SyncCalls++;
            return Acquire(requestContext, cancellationToken);
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken
        )
        {
            AsyncCalls++;
            return ValueTask.FromResult(Acquire(requestContext, cancellationToken));
        }
    }

    private static Dictionary<string, string?> Settings() =>
        new()
        {
            ["Migrations:AuthenticationMode"] = "AzureCli",
            ["Migrations:Postgres:Host"] = "migration.example.invalid",
            ["Migrations:Postgres:Database"] = "migration-database",
            ["Migrations:Postgres:Username"] = "migration-role",
            ["AZURE_TENANT_ID"] = Tenant,
        };

    private static IConfiguration Config(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    // Inspect EF's actual data-source wiring without opening a database connection.
#pragma warning disable EF1001
    private static NpgsqlDataSource DataSource(SaraDbContext context) =>
        Assert.IsAssignableFrom<NpgsqlDataSource>(
            context
                .GetService<IDbContextOptions>()
                .Extensions.OfType<NpgsqlOptionsExtension>()
                .Single()
                .DataSource
        );
#pragma warning restore EF1001

    [Theory]
    [InlineData(null)]
    [InlineData("Legacy")]
    [InlineData("legacy")]
    public void LegacyPreservesDefaultDirectPasswordAndOrderedFallback(string? mode)
    {
        const string direct =
            "Host=localhost;Database=synthetic;Username=synthetic;Password=synthetic-password";
        foreach (var methods in new[] { null, "ConnectionString", "AppRegIdentity" })
        {
            var settings = new Dictionary<string, string?>
            {
                ["Database:postgresConnectionString"] = direct,
                ["Database:Server"] = "",
            };
            if (mode is not null)
                settings["Migrations:AuthenticationMode"] = mode;
            if (methods is not null)
            {
                settings["Database:AllowedAuthMethods:0"] = methods;
                settings["Database:AllowedAuthMethods:1"] = "ConnectionString";
            }
            using var context = DesignTimeContextFactory.CreateDbContext(
                Config(settings),
                _ =>
                    throw new InvalidOperationException(
                        "Legacy must not create a migration credential."
                    )
            );
            Assert.Equal(direct, context.Database.GetConnectionString());
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("AzureCli ")]
    [InlineData("azure_cli")]
    [InlineData("Unknown")]
    public void InvalidModeFailsBeforeReadingLegacyConfiguration(string mode)
    {
        var config = new Mock<IConfiguration>(MockBehavior.Strict);
        config.Setup(c => c["Migrations:AuthenticationMode"]).Returns(mode);
        var error = Assert.Throws<InvalidOperationException>(() =>
            DesignTimeContextFactory.CreateDbContext(config.Object)
        );
        Assert.Contains("Migrations:AuthenticationMode", error.Message);
    }

    [Theory]
    [InlineData("AzureCli")]
    [InlineData("azurecli")]
    public void AzureCliReadsOnlyDedicatedSettingsAndUsesExplicitTenant(string mode)
    {
        var settings = Settings();
        settings["Migrations:AuthenticationMode"] = mode;
        var config = new Mock<IConfiguration>(MockBehavior.Strict);
        foreach (var (key, value) in settings)
            config.Setup(c => c[key]).Returns(value);
        var credential = new StubCredential();
        var credentialCalls = 0;
        using var context = DesignTimeContextFactory.CreateDbContext(
            config.Object,
            options =>
            {
                credentialCalls++;
                Assert.Equal(Tenant, options.TenantId);
                Assert.Equal(TimeSpan.FromSeconds(30), options.ProcessTimeout);
                Assert.Empty(options.AdditionallyAllowedTenants);
                return credential;
            }
        );
        var source = DataSource(context);
        var connection = new NpgsqlConnectionStringBuilder(source.ConnectionString);
        Assert.Equal("migration.example.invalid", connection.Host);
        Assert.Equal("migration-database", connection.Database);
        Assert.Equal("migration-role", connection.Username);
        Assert.Equal(SslMode.VerifyFull, connection.SslMode);
        Assert.True(string.IsNullOrEmpty(connection.Password));
        Assert.Equal(1, credentialCalls);
        Assert.Equal(0, credential.SyncCalls + credential.AsyncCalls);
        Assert.DoesNotContain("synthetic-token", context.Database.GetConnectionString()!);
        Assert.False(context.GetService<ILoggingOptions>().IsSensitiveDataLoggingEnabled);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void AzureCliIgnoresInheritedAndOverriddenRuntimeArraysAndPasswords(string environment)
    {
        var settings = Settings();
        settings["Database:AllowedAuthMethods:0"] = "ConnectionString";
        settings["Database:AllowedAuthMethods:2"] = "ConnectionString";
        settings["Database:postgresConnectionString"] = "not even a connection string";
        settings["Database:User"] = "runtime-role";
        settings["AzureAd:TenantId"] = "not-the-migration-tenant";
        settings["KeyVault:VaultUri"] = "not-a-uri";
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(AppContext.BaseDirectory, "DatabaseSettings"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environment}.json")
            .AddInMemoryCollection(settings)
            .Build();
        Assert.True(config.GetSection("Database:AllowedAuthMethods").Get<string[]>()!.Length >= 3);
        using var context = DesignTimeContextFactory.CreateDbContext(
            config,
            _ => new StubCredential()
        );
        Assert.Equal(
            "migration-role",
            new NpgsqlConnectionStringBuilder(DataSource(context).ConnectionString).Username
        );
    }

    [Theory]
    [InlineData("Migrations:Postgres:Host", null)]
    [InlineData("Migrations:Postgres:Database", null)]
    [InlineData("Migrations:Postgres:Username", null)]
    [InlineData("AZURE_TENANT_ID", null)]
    [InlineData("Migrations:Postgres:Host", "")]
    [InlineData("Migrations:Postgres:Database", " ")]
    [InlineData("Migrations:Postgres:Username", " role")]
    [InlineData("Migrations:Postgres:Username", "role\n")]
    [InlineData("Migrations:Postgres:Host", "host:5432")]
    [InlineData("Migrations:Postgres:Host", "host,other")]
    [InlineData("Migrations:Postgres:Host", "https://host")]
    [InlineData("AZURE_TENANT_ID", "not-a-tenant")]
    [InlineData("AZURE_TENANT_ID", "00000000-0000-0000-0000-000000000000")]
    public void InvalidDedicatedSettingsFailBeforeCredentialCreation(string key, string? value)
    {
        var settings = Settings();
        settings[key] = value;
        settings["Database:postgresConnectionString"] = "stored-password-must-not-be-read";
        var calls = 0;
        var error = Assert.Throws<InvalidOperationException>(() =>
            DesignTimeContextFactory.CreateDbContext(
                Config(settings),
                _ =>
                {
                    calls++;
                    return new StubCredential();
                }
            )
        );
        Assert.Contains(key, error.Message);
        Assert.Equal(0, calls);
        Assert.DoesNotContain("stored-password", error.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NpgsqlCloneCallbacksRequestFreshTokensWithoutChangingMigrationSource(
        bool async
    )
    {
        var credential = new StubCredential();
        using var context = DesignTimeContextFactory.CreateDbContext(
            Config(Settings()),
            _ => credential
        );
        var connection = Assert.IsType<NpgsqlConnection>(context.Database.GetDbConnection());
        for (var i = 1; i <= 2; i++)
        {
            // CloneWith invokes the real password provider without a network connection.
            using var clone = async
                ? await connection.CloneWithAsync(
                    connection.ConnectionString,
                    TestContext.Current.CancellationToken
                )
                : connection.CloneWith(connection.ConnectionString);
            Assert.Equal(
                $"synthetic-token-{i}",
                new NpgsqlConnectionStringBuilder(clone.ConnectionString).Password
            );
            Assert.Equal(
                SslMode.VerifyFull,
                new NpgsqlConnectionStringBuilder(clone.ConnectionString).SslMode
            );
        }
        Assert.Equal(async ? 0 : 2, credential.SyncCalls);
        Assert.Equal(async ? 2 : 0, credential.AsyncCalls);
        Assert.True(
            string.IsNullOrEmpty(
                new NpgsqlConnectionStringBuilder(DataSource(context).ConnectionString).Password
            )
        );
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task NpgsqlCallbackFailuresHaveNoSensitiveExceptionChainOrPasswordFallback(
        bool async,
        bool empty
    )
    {
        using var context = DesignTimeContextFactory.CreateDbContext(
            Config(Settings()),
            _ => new StubCredential(_ =>
                empty ? default : throw new AuthenticationFailedException(SensitiveDetails)
            )
        );
        var connection = Assert.IsType<NpgsqlConnection>(context.Database.GetDbConnection());
        var error = async
            ? await Assert.ThrowsAsync<NpgsqlException>(async () =>
                await connection.CloneWithAsync(
                    connection.ConnectionString,
                    TestContext.Current.CancellationToken
                )
            )
            : Assert.Throws<NpgsqlException>(() =>
                connection.CloneWith(connection.ConnectionString)
            );
        Assert.Contains("no password fallback", error.ToString());
        Assert.DoesNotContain(SensitiveDetails, error.ToString());
        Assert.Null(Assert.IsType<InvalidOperationException>(error.InnerException).InnerException);
    }

    [Theory]
    [InlineData(false, "failure")]
    [InlineData(true, "failure")]
    [InlineData(false, "unavailable")]
    [InlineData(true, "unavailable")]
    [InlineData(false, "null")]
    [InlineData(true, "null")]
    [InlineData(false, "empty")]
    [InlineData(true, "empty")]
    [InlineData(false, "whitespace")]
    [InlineData(true, "whitespace")]
    public async Task TokenFailureOrEmptyTokenFailsClosedWithoutSensitiveDiagnostics(
        bool async,
        string result
    )
    {
        var auth = new AzureCliMigrationAuthentication(
            new StubCredential(_ =>
                result switch
                {
                    "failure" => throw new AuthenticationFailedException(SensitiveDetails),
                    "unavailable" => throw new CredentialUnavailableException(SensitiveDetails),
                    "null" => default,
                    "empty" => new AccessToken("", DateTimeOffset.UtcNow.AddHours(1)),
                    _ => new AccessToken(" ", DateTimeOffset.UtcNow.AddHours(1)),
                }
            )
        );
        var error = async
            ? await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await auth.GetPasswordAsync(CancellationToken.None)
            )
            : Assert.Throws<InvalidOperationException>(() => auth.GetPassword());
        Assert.Contains("no password fallback", error.Message);
        Assert.DoesNotContain(SensitiveDetails, error.ToString());
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnexpectedProgrammingErrorsAreNotReclassified(bool async)
    {
        var cause = new InvalidOperationException("synthetic programming error");
        var auth = new AzureCliMigrationAuthentication(new StubCredential(_ => throw cause));
        var error = async
            ? await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await auth.GetPasswordAsync(CancellationToken.None)
            )
            : Assert.Throws<InvalidOperationException>(() => auth.GetPassword());
        Assert.Same(cause, error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcquisitionCancellationIsReportedAsTimeoutWithoutSensitiveDetails(bool async)
    {
        var auth = new AzureCliMigrationAuthentication(
            new StubCredential(_ => throw new OperationCanceledException(SensitiveDetails))
        );
        var error = async
            ? await Assert.ThrowsAsync<TimeoutException>(async () =>
                await auth.GetPasswordAsync(CancellationToken.None)
            )
            : Assert.Throws<TimeoutException>(() => auth.GetPassword());
        Assert.Contains("timed out", error.Message);
        Assert.DoesNotContain(SensitiveDetails, error.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncCallerCancellationIsPropagated(bool alreadyCanceled)
    {
        using var cancellation = new CancellationTokenSource();
        var credential = new StubCredential(token =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return default;
        });
        if (alreadyCanceled)
            cancellation.Cancel();
        var auth = new AzureCliMigrationAuthentication(credential);
        var error = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await auth.GetPasswordAsync(cancellation.Token)
        );
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(alreadyCanceled ? 0 : 1, credential.AsyncCalls);
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContextDisposesItsOwnMigrationSource(bool async)
    {
        var context = DesignTimeContextFactory.CreateDbContext(
            Config(Settings()),
            _ => new StubCredential()
        );
        var source = DataSource(context);
        if (async)
            await context.DisposeAsync();
        else
            context.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await source.OpenConnectionAsync(new CancellationToken(true))
        );
        // Both disposal entry points remain safe when EF tooling disposes more than once.
        context.Dispose();
        await context.DisposeAsync();
    }

    [Fact]
    public async Task RuntimeConstructorDoesNotOwnAnExternalSource()
    {
        await using var source = NpgsqlDataSource.Create("Host=localhost;Username=synthetic");
        var context = new SaraDbContext(
            new DbContextOptionsBuilder<SaraDbContext>().UseNpgsql(source).Options
        );
        context.Dispose();
        await context.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await source.OpenConnectionAsync(new CancellationToken(true))
        );
    }

    [Fact]
    public void FactoryCallsDoNotShareCredentialSources()
    {
        using var first = DesignTimeContextFactory.CreateDbContext(
            Config(Settings()),
            _ => new StubCredential()
        );
        var settings = Settings();
        settings["AZURE_TENANT_ID"] = "22222222-2222-3333-4444-555555555555";
        using var second = DesignTimeContextFactory.CreateDbContext(
            Config(settings),
            _ => new StubCredential()
        );
        Assert.NotSame(DataSource(first), DataSource(second));
    }

    [Fact]
    public void ReviewedMarkerIsExactlyTheSupportedContractWithOneLf()
    {
        Assert.Equal(
            Encoding.UTF8.GetBytes("azure-cli-postgresql-v1\n"),
            File.ReadAllBytes(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "DatabaseSettings",
                    ".migration-auth-contract"
                )
            )
        );
    }
}
