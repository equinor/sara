using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using api.Services;
using api.Services.HostedServices;
using k8s.Autorest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Test.Services;

public class ArgoWorkflowWatcherServiceTests
{
    [Theory]
    [InlineData("eof", LogLevel.Information, true)]
    [InlineData("http", LogLevel.Error, true)]
    [InlineData("gone", LogLevel.Information, false)]
    [InlineData("gone-event", LogLevel.Information, false)]
    [InlineData("complete", null, false)]
    public async Task WatchTermination_RelistsAndProcessesFreshSnapshot(
        string failure,
        LogLevel? expectedLevel,
        bool delayed
    )
    {
        using var harness = new WatcherHarness();
        var firstSnapshot = new ArgoWorkflowResource();
        var secondSnapshot = new ArgoWorkflowResource();
        var liveEvent = new ArgoWorkflowResource();
        var handled = new List<ArgoWorkflowResource>();
        var versions = new List<string>();
        var reconnected = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        long terminatedAt = 0;
        long relistedAt = 0;
        var exception = failure == "complete" ? null : Failure(failure);

        harness
            .Client.Setup(c => c.ListWorkflowsAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (versions.Count == 0)
                    return Task.FromResult(new ArgoWorkflowSnapshot([firstSnapshot], "10"));
                relistedAt = Stopwatch.GetTimestamp();
                return Task.FromResult(new ArgoWorkflowSnapshot([secondSnapshot], "20"));
            });
        harness
            .Client.Setup(c =>
                c.WatchWorkflowsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                (string version, CancellationToken token) =>
                {
                    versions.Add(version);
                    if (versions.Count == 1)
                    {
                        return EmitThenTerminate(
                            [liveEvent],
                            exception,
                            () => terminatedAt = Stopwatch.GetTimestamp()
                        );
                    }
                    reconnected.SetResult();
                    return WaitForCancellation(token);
                }
            );
        harness
            .Processor.Setup(p =>
                p.HandleWorkflowEventAsync(
                    It.IsAny<ArgoWorkflowResource>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<ArgoWorkflowResource, CancellationToken>(
                (workflow, _) => handled.Add(workflow)
            )
            .Returns(Task.CompletedTask);

        var run = harness.RunAsync();
        await reconnected.Task.WaitAsync(harness.Token);
        harness.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(["10", "20"], versions);
        Assert.Equal([firstSnapshot, liveEvent, secondSnapshot], handled);
        var elapsed = Stopwatch.GetElapsedTime(terminatedAt, relistedAt);
        if (delayed)
            Assert.True(elapsed >= TimeSpan.FromSeconds(4.9), $"Retry took only {elapsed}");
        else
            Assert.True(elapsed < TimeSpan.FromSeconds(4), $"Immediate relist took {elapsed}");

        var reconnectLogs = harness.Logger.Entries.Where(e => e.Message.EndsWith("; relisting"));
        if (expectedLevel is null)
        {
            Assert.Empty(reconnectLogs);
        }
        else
        {
            var entry = Assert.Single(reconnectLogs);
            Assert.Equal(expectedLevel, entry.Level);
            if (failure == "eof")
            {
                Assert.Equal("Argo Workflow watch stream closed; relisting", entry.Message);
                Assert.Same(exception, entry.Exception!.InnerException);
            }
            if (expectedLevel == LogLevel.Error)
                Assert.Same(exception, entry.Exception);
        }
        if (expectedLevel != LogLevel.Error)
            Assert.DoesNotContain(harness.Logger.Entries, e => e.Level >= LogLevel.Error);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("io")]
    [InlineData("bare-eof")]
    [InlineData("wrapped-io")]
    [InlineData("nested-eof")]
    [InlineData("unauthorized")]
    [InlineData("forbidden")]
    [InlineData("server-error")]
    [InlineData("http-forbidden")]
    [InlineData("watch-error")]
    [InlineData("processor-error")]
    [InlineData("uncancelled")]
    public async Task OtherWatchFailures_RemainErrorsAndBackoffCanBeCancelled(string failure)
    {
        using var harness = new WatcherHarness();
        var exception = Failure(failure);
        harness
            .Client.Setup(c =>
                c.WatchWorkflowsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .Returns(() => EmitThenTerminate([], exception));

        await AssertFailureAndCancelBackoff(harness, exception, LogLevel.Error);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("snapshot")]
    [InlineData("event")]
    public async Task EofOutsideWatchRead_RemainsAnError(string source)
    {
        using var harness = new WatcherHarness();
        var exception = Failure("eof");
        var workflow = new ArgoWorkflowResource();
        if (source == "list")
        {
            harness
                .Client.Setup(c => c.ListWorkflowsAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
        }
        else
        {
            harness
                .Client.Setup(c => c.ListWorkflowsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new ArgoWorkflowSnapshot(source == "snapshot" ? [workflow] : [], "1")
                );
            harness
                .Client.Setup(c =>
                    c.WatchWorkflowsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
                )
                .Returns(() => EmitThenTerminate([workflow], null));
            harness
                .Processor.Setup(p =>
                    p.HandleWorkflowEventAsync(workflow, It.IsAny<CancellationToken>())
                )
                .ThrowsAsync(exception);
        }

        await AssertFailureAndCancelBackoff(harness, exception, LogLevel.Error);
    }

    [Fact]
    public async Task EofBackoff_CanBeCancelled()
    {
        using var harness = new WatcherHarness();
        var exception = Failure("eof");
        harness
            .Client.Setup(c =>
                c.WatchWorkflowsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .Returns(() => EmitThenTerminate([], exception));

        await AssertFailureAndCancelBackoff(harness, exception, LogLevel.Information);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("snapshot")]
    [InlineData("event")]
    [InlineData("watch")]
    [InlineData("eof-on-cancellation")]
    public async Task Shutdown_ExitsWithoutError(string source)
    {
        using var harness = new WatcherHarness();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Wait(CancellationToken token)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }

        if (source == "list")
        {
            harness
                .Client.Setup(c => c.ListWorkflowsAsync(It.IsAny<CancellationToken>()))
                .Returns(
                    async (CancellationToken token) =>
                    {
                        await Wait(token);
                        return new ArgoWorkflowSnapshot([], "1");
                    }
                );
        }
        else if (source is "snapshot" or "event")
        {
            var workflow = new ArgoWorkflowResource();
            harness
                .Client.Setup(c => c.ListWorkflowsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new ArgoWorkflowSnapshot(source == "snapshot" ? [workflow] : [], "1")
                );
            harness
                .Client.Setup(c =>
                    c.WatchWorkflowsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
                )
                .Returns(() => EmitThenTerminate([workflow], null));
            harness
                .Processor.Setup(p =>
                    p.HandleWorkflowEventAsync(
                        It.IsAny<ArgoWorkflowResource>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns((ArgoWorkflowResource _, CancellationToken token) => Wait(token));
        }
        else
        {
            harness
                .Client.Setup(c =>
                    c.WatchWorkflowsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
                )
                .Returns(
                    (string _, CancellationToken token) =>
                    {
                        entered.TrySetResult();
                        return source == "watch"
                            ? WaitForCancellation(token)
                            : EmitThenTerminate([], Failure("eof"), harness.Cancel);
                    }
                );
        }

        var run = harness.RunAsync();
        await entered.Task.WaitAsync(harness.Token);
        if (source != "eof-on-cancellation")
            harness.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(harness.Logger.Entries, e => e.Message.EndsWith("; relisting"));
    }

    private static async Task AssertFailureAndCancelBackoff(
        WatcherHarness harness,
        Exception exception,
        LogLevel level
    )
    {
        var run = harness.RunAsync();
        await harness.Logger.RetryLogged.Task.WaitAsync(harness.Token);
        // The retry must not hot-loop, and cancellation must interrupt the five-second delay.
        await Task.Delay(TimeSpan.FromMilliseconds(50), harness.Token);
        harness.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        var entry = Assert.Single(harness.Logger.Entries, e => e.Message.EndsWith("; relisting"));
        Assert.Equal(level, entry.Level);
        Assert.Same(
            exception,
            level == LogLevel.Error ? entry.Exception : entry.Exception!.InnerException
        );
        harness.Client.Verify(c => c.ListWorkflowsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Exception Failure(string kind) =>
        kind switch
        {
            "eof" => new HttpRequestException(
                "Error while copying content to a stream.",
                new EndOfStreamException()
            ),
            "http" => new HttpRequestException("Connection refused"),
            "io" => new IOException("Read failed"),
            "bare-eof" => new EndOfStreamException(),
            "wrapped-io" => new HttpRequestException("Read failed", new IOException()),
            "nested-eof" => new HttpRequestException(
                "Unexpected wrapper",
                new IOException("Read failed", new EndOfStreamException())
            ),
            "unauthorized" => new HttpRequestException(
                "Unauthorized",
                new EndOfStreamException(),
                HttpStatusCode.Unauthorized
            ),
            "forbidden" => new HttpRequestException(
                "Forbidden",
                new EndOfStreamException(),
                HttpStatusCode.Forbidden
            ),
            "server-error" => new HttpRequestException(
                "Unavailable",
                new EndOfStreamException(),
                HttpStatusCode.ServiceUnavailable
            ),
            "http-forbidden" => HttpFailure(HttpStatusCode.Forbidden),
            "gone" => HttpFailure(HttpStatusCode.Gone),
            "gone-event" => new ArgoWorkflowWatchException(410, "Expired"),
            "watch-error" => new ArgoWorkflowWatchException(403, "Forbidden"),
            "processor-error" => new InvalidOperationException("Unexpected failure"),
            "uncancelled" => new OperationCanceledException(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static HttpOperationException HttpFailure(HttpStatusCode status) =>
        new("HTTP failure")
        {
            Response = new HttpResponseMessageWrapper(
                new HttpResponseMessage(status),
                string.Empty
            ),
        };

    private static async IAsyncEnumerable<ArgoWorkflowResource> EmitThenTerminate(
        IEnumerable<ArgoWorkflowResource> items,
        Exception? exception,
        Action? beforeTermination = null
    )
    {
        await Task.Yield();
        foreach (var item in items)
            yield return item;
        beforeTermination?.Invoke();
        if (exception is not null)
            throw exception;
    }

    private static async IAsyncEnumerable<ArgoWorkflowResource> WaitForCancellation(
        [EnumeratorCancellation] CancellationToken token
    )
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        yield break;
    }

    private sealed class WatcherHarness : IDisposable
    {
        private readonly CancellationTokenSource _cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        private readonly ServiceProvider _services;
        private readonly TestWatcher _watcher;
        public Mock<IArgoWorkflowClient> Client { get; } = new();
        public Mock<IArgoWorkflowEventProcessor> Processor { get; } = new();
        public RecordingLogger Logger { get; } = new();
        public CancellationToken Token => _cancellation.Token;

        public WatcherHarness()
        {
            _cancellation.CancelAfter(TimeSpan.FromSeconds(20));
            Client
                .Setup(c => c.ListWorkflowsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ArgoWorkflowSnapshot([], "1"));
            _services = new ServiceCollection()
                .AddScoped<IArgoWorkflowEventProcessor>(_ => Processor.Object)
                .BuildServiceProvider();
            _watcher = new TestWatcher(
                Client.Object,
                _services.GetRequiredService<IServiceScopeFactory>(),
                Logger
            );
        }

        public Task RunAsync() => _watcher.RunAsync(Token);

        public void Cancel() => _cancellation.Cancel();

        public void Dispose()
        {
            _cancellation.Cancel();
            _watcher.Dispose();
            _services.Dispose();
            _cancellation.Dispose();
        }
    }

    private sealed class TestWatcher(
        IArgoWorkflowClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<ArgoWorkflowWatcherService> logger
    ) : ArgoWorkflowWatcherService(client, scopeFactory, logger)
    {
        public Task RunAsync(CancellationToken token) => ExecuteAsync(token);
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class RecordingLogger : ILogger<ArgoWorkflowWatcherService>
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public TaskCompletionSource RetryLogged { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var message = formatter(state, exception);
            Entries.Enqueue(new LogEntry(logLevel, message, exception));
            if (message.EndsWith("; relisting"))
                RetryLogged.TrySetResult();
        }
    }
}
