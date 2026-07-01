using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Indigosoft.TestTask.Aggregator.Channels;
using Indigosoft.TestTask.Aggregator.Deduplication;
using Indigosoft.TestTask.Aggregator.Parsing;
using Indigosoft.TestTask.Aggregator.WebSockets;
using Indigosoft.TestTask.Aggregator.Workers;
using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Core.Models;
using Indigosoft.TestTask.Core.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Workers;

public sealed class ExchangeConnectionWorkerTests
{
    private const string ValidExchangeAJson = """
        {
          "ticker": "BTCUSDT",
          "price": 65000.12,
          "volume": 0.42,
          "timestamp": "2026-06-30T12:00:00.000Z",
          "sequence": 123
        }
        """;

    [Fact]
    public async Task RunAsync_WithValidMessage_ParsesDeduplicatesAndEnqueuesTick()
    {
        var stream = new FakeExchangeMessageStream();
        stream.EnqueueMessages(waitForCancellationAfterMessages: true, ValidExchangeAJson);
        var worker = CreateWorker(stream, out var tickChannel);
        using var cancellation = new CancellationTokenSource();

        var workerTask = worker.RunAsync(CreateConnection(), cancellation.Token);
        var tick = await tickChannel.ReadAsync(cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(ExchangeSource.ExchangeA, tick.Source);
        Assert.Equal("BTCUSDT", tick.Ticker);
        Assert.Equal(65000.12m, tick.Price);

        await StopWorkerAsync(workerTask, cancellation);
    }

    [Fact]
    public async Task RunAsync_WithInvalidJson_SkipsMessage()
    {
        var stream = new FakeExchangeMessageStream();
        stream.EnqueueMessages(waitForCancellationAfterMessages: true, "{ invalid json");
        var worker = CreateWorker(stream, out var tickChannel);
        using var cancellation = new CancellationTokenSource();

        var workerTask = worker.RunAsync(CreateConnection(), cancellation.Token);
        await stream.WaitForCompletedAttemptsAsync(1).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0, tickChannel.Count);

        await StopWorkerAsync(workerTask, cancellation);
    }

    [Fact]
    public async Task RunAsync_WithDuplicateMessages_EnqueuesOnlyOneTick()
    {
        var stream = new FakeExchangeMessageStream();
        stream.EnqueueMessages(waitForCancellationAfterMessages: true, ValidExchangeAJson, ValidExchangeAJson);
        var worker = CreateWorker(stream, out var tickChannel);
        using var cancellation = new CancellationTokenSource();

        var workerTask = worker.RunAsync(CreateConnection(), cancellation.Token);
        await stream.WaitForCompletedAttemptsAsync(1).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, tickChannel.Count);
        var tick = await tickChannel.ReadAsync(cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("BTCUSDT", tick.Ticker);
        Assert.Equal(0, tickChannel.Count);

        await StopWorkerAsync(workerTask, cancellation);
    }

    [Fact]
    public async Task RunAsync_WhenStreamThrowsClosedException_ReconnectsAndContinues()
    {
        var stream = new FakeExchangeMessageStream();
        stream.EnqueueException(new ExchangeWebSocketClosedException(ExchangeSource.ExchangeA, "Closed by server."));
        stream.EnqueueMessages(waitForCancellationAfterMessages: true, ValidExchangeAJson);
        var reconnectDelay = new RecordingReconnectDelay();
        var worker = CreateWorker(stream, reconnectDelay, out var tickChannel);
        using var cancellation = new CancellationTokenSource();

        var workerTask = worker.RunAsync(CreateConnection(), cancellation.Token);

        await reconnectDelay.WaitForDelayCountAsync(1).WaitAsync(TimeSpan.FromSeconds(2));
        var tick = await tickChannel.ReadAsync(cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("BTCUSDT", tick.Ticker);
        Assert.Single(reconnectDelay.Delays);

        await StopWorkerAsync(workerTask, cancellation);
    }

    [Fact]
    public async Task RunAsync_WhenStreamThrowsIdleTimeoutException_Reconnects()
    {
        var stream = new FakeExchangeMessageStream();
        stream.EnqueueException(new ExchangeWebSocketIdleTimeoutException(ExchangeSource.ExchangeA, TimeSpan.FromSeconds(10)));
        stream.EnqueueMessages(waitForCancellationAfterMessages: true, ValidExchangeAJson);
        var reconnectDelay = new RecordingReconnectDelay();
        var worker = CreateWorker(stream, reconnectDelay, out var tickChannel);
        using var cancellation = new CancellationTokenSource();

        var workerTask = worker.RunAsync(CreateConnection(), cancellation.Token);

        await reconnectDelay.WaitForDelayCountAsync(1).WaitAsync(TimeSpan.FromSeconds(2));
        var tick = await tickChannel.ReadAsync(cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("BTCUSDT", tick.Ticker);
        Assert.Single(reconnectDelay.Delays);

        await StopWorkerAsync(workerTask, cancellation);
    }

    [Fact]
    public async Task RunAsync_WhenCancellationRequested_StopsWithoutThrowing()
    {
        var stream = new FakeExchangeMessageStream();
        var worker = CreateWorker(stream, out _);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await worker.RunAsync(CreateConnection(), cancellation.Token);
    }

    private static ExchangeConnectionWorker CreateWorker(
        FakeExchangeMessageStream stream,
        out TickChannel tickChannel)
    {
        return CreateWorker(stream, new RecordingReconnectDelay(), out tickChannel);
    }

    private static ExchangeConnectionWorker CreateWorker(
        FakeExchangeMessageStream stream,
        RecordingReconnectDelay reconnectDelay,
        out TickChannel tickChannel)
    {
        var parserResolver = new ExchangeMessageParserResolver(
        [
            new ExchangeAMessageParser(),
            new ExchangeBMessageParser(),
            new ExchangeCMessageParser()
        ]);

        var deduplicator = new InMemoryTickDeduplicator(Options.Create(new DeduplicationOptions()));
        tickChannel = new TickChannel(Options.Create(new TickChannelOptions { Capacity = 10 }));

        return new ExchangeConnectionWorker(
            stream,
            parserResolver,
            deduplicator,
            tickChannel,
            reconnectDelay,
            NullLogger<ExchangeConnectionWorker>.Instance);
    }

    private static ExchangeConnectionOptions CreateConnection()
    {
        return new ExchangeConnectionOptions
        {
            Source = ExchangeSource.ExchangeA,
            WebSocketUrl = "ws://localhost:5100/ws/exchange-a",
            InitialReconnectDelayMs = 1_000,
            MaxReconnectDelayMs = 4_000,
            IdleTimeoutSeconds = 10
        };
    }

    private static async Task StopWorkerAsync(
        Task workerTask,
        CancellationTokenSource cancellation)
    {
        await cancellation.CancelAsync();
        await workerTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class FakeExchangeMessageStream : IExchangeMessageStream
    {
        private readonly ConcurrentQueue<StreamAttempt> _attempts = new();
        private readonly TaskCompletionSource _completedAttemptsChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _completedAttempts;

        public void EnqueueMessages(bool waitForCancellationAfterMessages, params string[] messages)
        {
            _attempts.Enqueue(StreamAttempt.FromMessages(messages, waitForCancellationAfterMessages));
        }

        public void EnqueueException(Exception exception)
        {
            _attempts.Enqueue(StreamAttempt.FromException(exception));
        }

        public async Task WaitForCompletedAttemptsAsync(int expectedCompletedAttempts)
        {
            while (Volatile.Read(ref _completedAttempts) < expectedCompletedAttempts)
            {
                await _completedAttemptsChanged.Task;
            }
        }

        public IAsyncEnumerable<string> ReadMessagesAsync(
            ExchangeConnectionOptions connection,
            CancellationToken cancellationToken)
        {
            if (!_attempts.TryDequeue(out var attempt))
            {
                attempt = StreamAttempt.WaitUntilCancellation();
            }

            return ReadAttemptAsync(attempt, cancellationToken);
        }

        private async IAsyncEnumerable<string> ReadAttemptAsync(
            StreamAttempt attempt,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (attempt.Exception is not null)
            {
                MarkAttemptCompleted();
                throw attempt.Exception;
            }

            foreach (var message in attempt.Messages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return message;
            }

            MarkAttemptCompleted();

            if (attempt.WaitForCancellationAfterMessages)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }

        private void MarkAttemptCompleted()
        {
            Interlocked.Increment(ref _completedAttempts);
            _completedAttemptsChanged.TrySetResult();
        }
    }

    private sealed class RecordingReconnectDelay : IReconnectDelay
    {
        private readonly object _gate = new();
        private readonly TaskCompletionSource _delayChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<TimeSpan> _delays = [];

        public IReadOnlyList<TimeSpan> Delays
        {
            get
            {
                lock (_gate)
                {
                    return _delays.ToArray();
                }
            }
        }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _delays.Add(delay);
            }

            _delayChanged.TrySetResult();
            return Task.CompletedTask;
        }

        public async Task WaitForDelayCountAsync(int expectedDelayCount)
        {
            while (Delays.Count < expectedDelayCount)
            {
                await _delayChanged.Task;
            }
        }
    }

    private sealed record StreamAttempt(
        IReadOnlyCollection<string> Messages,
        Exception? Exception,
        bool WaitForCancellationAfterMessages)
    {
        public static StreamAttempt FromMessages(
            IReadOnlyCollection<string> messages,
            bool waitForCancellationAfterMessages)
        {
            return new StreamAttempt(messages, null, waitForCancellationAfterMessages);
        }

        public static StreamAttempt FromException(Exception exception)
        {
            return new StreamAttempt([], exception, false);
        }

        public static StreamAttempt WaitUntilCancellation()
        {
            return new StreamAttempt([], null, true);
        }
    }
}
