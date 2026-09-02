using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using api.Services;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Test.Services;

public class UserDelegationKeyProviderTests
{
    private static readonly TimeSpan SasLifetime = TimeSpan.FromHours(1);

    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static Mock<BlobServiceClient> MockClient(int callCounter = 0)
    {
        var mock = new Mock<BlobServiceClient>();
        mock.Setup(c =>
                c.GetUserDelegationKeyAsync(
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (DateTimeOffset? start, DateTimeOffset end, CancellationToken _) =>
                    Response.FromValue(
                        BlobsModelFactory.UserDelegationKey(
                            "oid",
                            "tid",
                            start ?? DateTimeOffset.UtcNow,
                            end,
                            "b",
                            "2021-08-06",
                            "key"
                        ),
                        Mock.Of<Response>()
                    )
            );
        return mock;
    }

    [Fact]
    public async Task SecondCallReusesTheCachedKeyInsteadOfCallingStorage()
    {
        var client = MockClient();
        var provider = new UserDelegationKeyProvider(
            NullLogger<UserDelegationKeyProvider>.Instance,
            new FakeTimeProvider(DateTimeOffset.UtcNow)
        );

        await provider.GetAsync(
            client.Object,
            "acct",
            SasLifetime,
            TestContext.Current.CancellationToken
        );
        await provider.GetAsync(
            client.Object,
            "acct",
            SasLifetime,
            TestContext.Current.CancellationToken
        );
        await provider.GetAsync(
            client.Object,
            "acct",
            SasLifetime,
            TestContext.Current.CancellationToken
        );

        client.Verify(
            c =>
                c.GetUserDelegationKeyAsync(
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task DifferentStorageAccountsAreCachedSeparately()
    {
        var client = MockClient();
        var provider = new UserDelegationKeyProvider(
            NullLogger<UserDelegationKeyProvider>.Instance,
            new FakeTimeProvider(DateTimeOffset.UtcNow)
        );

        await provider.GetAsync(
            client.Object,
            "acct-a",
            SasLifetime,
            TestContext.Current.CancellationToken
        );
        await provider.GetAsync(
            client.Object,
            "acct-b",
            SasLifetime,
            TestContext.Current.CancellationToken
        );

        client.Verify(
            c =>
                c.GetUserDelegationKeyAsync(
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task KeyIsRefetchedOnceTheReuseWindowHasPassed()
    {
        var client = MockClient();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var provider = new UserDelegationKeyProvider(
            NullLogger<UserDelegationKeyProvider>.Instance,
            clock
        );

        await provider.GetAsync(
            client.Object,
            "acct",
            SasLifetime,
            TestContext.Current.CancellationToken
        );
        // Key lifetime is 2h and the SAS lifetime is 1h, so the key stops being
        // reusable a little under an hour in.
        clock.Advance(TimeSpan.FromMinutes(70));
        await provider.GetAsync(
            client.Object,
            "acct",
            SasLifetime,
            TestContext.Current.CancellationToken
        );

        client.Verify(
            c =>
                c.GetUserDelegationKeyAsync(
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task ASasSignedAtTheEndOfTheReuseWindowStillExpiresBeforeTheKey()
    {
        DateTimeOffset? capturedExpiry = null;
        var client = new Mock<BlobServiceClient>();
        client
            .Setup(c =>
                c.GetUserDelegationKeyAsync(
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (DateTimeOffset? start, DateTimeOffset end, CancellationToken _) =>
                {
                    capturedExpiry = end;
                    return Response.FromValue(
                        BlobsModelFactory.UserDelegationKey(
                            "oid",
                            "tid",
                            start ?? DateTimeOffset.UtcNow,
                            end,
                            "b",
                            "2021-08-06",
                            "key"
                        ),
                        Mock.Of<Response>()
                    );
                }
            );

        var start = DateTimeOffset.UtcNow;
        var clock = new FakeTimeProvider(start);
        var provider = new UserDelegationKeyProvider(
            NullLogger<UserDelegationKeyProvider>.Instance,
            clock
        );

        await provider.GetAsync(
            client.Object,
            "acct",
            SasLifetime,
            TestContext.Current.CancellationToken
        );

        // Walk forward minute by minute for as long as the cached key is reused,
        // and assert the SAS that would be signed at that instant expires inside
        // the key's validity window.
        for (var minutes = 0; minutes < 120; minutes++)
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            var callsBefore = client.Invocations.Count;
            await provider.GetAsync(
                client.Object,
                "acct",
                SasLifetime,
                TestContext.Current.CancellationToken
            );
            if (client.Invocations.Count != callsBefore)
                break; // key was refreshed; the old window is over

            var sasExpiry = clock.GetUtcNow() + SasLifetime;
            Assert.True(
                sasExpiry < capturedExpiry,
                $"SAS signed at +{minutes}min would expire {sasExpiry:O}, after the key expires {capturedExpiry:O}"
            );
        }
    }

    [Fact]
    public async Task ConcurrentCallersOnlyTriggerOneFetch()
    {
        var gate = new TaskCompletionSource();
        var calls = 0;
        var client = new Mock<BlobServiceClient>();
        client
            .Setup(c =>
                c.GetUserDelegationKeyAsync(
                    It.IsAny<DateTimeOffset?>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (DateTimeOffset? start, DateTimeOffset end, CancellationToken _) =>
                {
                    Interlocked.Increment(ref calls);
                    await gate.Task;
                    return Response.FromValue(
                        BlobsModelFactory.UserDelegationKey(
                            "oid",
                            "tid",
                            start ?? DateTimeOffset.UtcNow,
                            end,
                            "b",
                            "2021-08-06",
                            "key"
                        ),
                        Mock.Of<Response>()
                    );
                }
            );

        var provider = new UserDelegationKeyProvider(
            NullLogger<UserDelegationKeyProvider>.Instance,
            new FakeTimeProvider(DateTimeOffset.UtcNow)
        );

        var inFlight = Enumerable
            .Range(0, 20)
            .Select(_ =>
                provider.GetAsync(
                    client.Object,
                    "acct",
                    SasLifetime,
                    TestContext.Current.CancellationToken
                )
            )
            .ToArray();

        gate.SetResult();
        await Task.WhenAll(inFlight);

        Assert.Equal(1, calls);
    }

    /// Captures log entries so the test can assert on refresh frequency.
    private sealed class RecordingLogger : ILogger<UserDelegationKeyProvider>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Messages.Add(formatter(state, exception));
    }

    [Fact]
    public async Task RefreshesAreLogged_ButCacheHitsAreNot()
    {
        // The value of the log line is that it appears roughly once an hour per
        // account. If cache hits were logged too it would fire once per blob,
        // which is the noise the cache exists to remove.
        var client = MockClient();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var logger = new RecordingLogger();
        var provider = new UserDelegationKeyProvider(logger, clock);

        for (var i = 0; i < 25; i++)
            await provider.GetAsync(
                client.Object,
                "acct",
                SasLifetime,
                TestContext.Current.CancellationToken
            );

        Assert.Single(logger.Messages);
        Assert.Contains("acct", logger.Messages[0]);
        Assert.Contains("first fetch since startup", logger.Messages[0]);

        // Past the reuse window the key is refetched, and that is logged.
        clock.Advance(TimeSpan.FromMinutes(70));
        await provider.GetAsync(
            client.Object,
            "acct",
            SasLifetime,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, logger.Messages.Count);
        Assert.Contains("min ago", logger.Messages[1]);
    }
}
