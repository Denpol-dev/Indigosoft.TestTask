using Indigosoft.TestTask.Aggregator.Channels;
using Indigosoft.TestTask.Aggregator.Workers;
using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Core.Models;
using Indigosoft.TestTask.Core.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Workers;

public sealed class TickBatchWriterHostedServiceTests
{
    [Fact]
    public async Task WritesBatch_WhenBatchSizeReached()
    {
        var tickChannel = CreateChannel();
        var writer = new FakeTickBatchWriter();
        var service = CreateService(tickChannel, writer, new BatchWriterOptions
        {
            BatchSize = 3,
            FlushIntervalMs = 60_000
        });

        await service.StartAsync(CancellationToken.None);
        await tickChannel.EnqueueAsync(CreateTick(1), CancellationToken.None);
        await tickChannel.EnqueueAsync(CreateTick(2), CancellationToken.None);
        await tickChannel.EnqueueAsync(CreateTick(3), CancellationToken.None);

        await writer.WaitForBatchCountAsync(1).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Single(writer.Batches);
        Assert.Equal(3, writer.Batches[0].Count);

        tickChannel.Complete();
        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task WritesPartialBatch_WhenChannelCompleted()
    {
        var tickChannel = CreateChannel();
        var writer = new FakeTickBatchWriter();
        var service = CreateService(tickChannel, writer, new BatchWriterOptions
        {
            BatchSize = 10,
            FlushIntervalMs = 60_000
        });

        await service.StartAsync(CancellationToken.None);
        await tickChannel.EnqueueAsync(CreateTick(1), CancellationToken.None);
        await tickChannel.EnqueueAsync(CreateTick(2), CancellationToken.None);
        tickChannel.Complete();

        await writer.WaitForBatchCountAsync(1).WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Single(writer.Batches);
        Assert.Equal(2, writer.Batches[0].Count);
    }

    [Fact]
    public async Task Retries_WhenWriterThrowsThenSucceeds()
    {
        var tickChannel = CreateChannel();
        var writer = new FakeTickBatchWriter(failuresBeforeSuccess: 2);
        var delay = new FakeBatchWriterDelay();
        var statistics = new TickWriteStatistics();
        var service = CreateService(tickChannel, writer, new BatchWriterOptions
        {
            BatchSize = 1,
            FlushIntervalMs = 60_000,
            MaxWriteRetries = 3,
            InitialRetryDelayMs = 500,
            MaxRetryDelayMs = 5_000
        }, delay, statistics);

        await service.StartAsync(CancellationToken.None);
        await tickChannel.EnqueueAsync(CreateTick(1), CancellationToken.None);
        tickChannel.Complete();

        await delay.WaitForDelayCountAsync(2).WaitAsync(TimeSpan.FromSeconds(2));
        await writer.WaitForBatchCountAsync(1).WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, delay.Delays.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(500), delay.Delays[0]);
        Assert.Equal(TimeSpan.FromMilliseconds(1_000), delay.Delays[1]);
        Assert.Equal(1, statistics.WrittenTicks);
        Assert.Equal(0, statistics.DroppedTicks);
        Assert.Equal(0, statistics.FailedBatches);
    }

    [Fact]
    public async Task DropsBatch_WhenRetriesExhausted()
    {
        var tickChannel = CreateChannel();
        var writer = new FakeTickBatchWriter(alwaysFail: true);
        var statistics = new TickWriteStatistics();
        var service = CreateService(tickChannel, writer, new BatchWriterOptions
        {
            BatchSize = 1,
            FlushIntervalMs = 60_000,
            MaxWriteRetries = 2,
            InitialRetryDelayMs = 500,
            MaxRetryDelayMs = 5_000
        }, statistics: statistics);

        await service.StartAsync(CancellationToken.None);
        await tickChannel.EnqueueAsync(CreateTick(1), CancellationToken.None);
        tickChannel.Complete();

        await writer.WaitForAttemptCountAsync(3).WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(writer.Batches);
        Assert.Equal(0, statistics.WrittenTicks);
        Assert.Equal(1, statistics.DroppedTicks);
        Assert.Equal(1, statistics.FailedBatches);
    }

    [Fact]
    public async Task StopAsync_DrainsRemainingTicks()
    {
        var tickChannel = CreateChannel();
        var writer = new FakeTickBatchWriter();
        var service = CreateService(tickChannel, writer, new BatchWriterOptions
        {
            BatchSize = 10,
            FlushIntervalMs = 60_000
        });

        await service.StartAsync(CancellationToken.None);
        await tickChannel.EnqueueAsync(CreateTick(1), CancellationToken.None);
        await tickChannel.EnqueueAsync(CreateTick(2), CancellationToken.None);
        await tickChannel.EnqueueAsync(CreateTick(3), CancellationToken.None);

        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Single(writer.Batches);
        Assert.Equal(3, writer.Batches[0].Count);
    }

    private static TickBatchWriterHostedService CreateService(
        TickChannel tickChannel,
        FakeTickBatchWriter writer,
        BatchWriterOptions options,
        FakeBatchWriterDelay? delay = null,
        TickWriteStatistics? statistics = null)
    {
        return new TickBatchWriterHostedService(
            tickChannel,
            writer,
            Options.Create(options),
            delay ?? new FakeBatchWriterDelay(),
            statistics ?? new TickWriteStatistics(),
            NullLogger<TickBatchWriterHostedService>.Instance);
    }

    private static TickChannel CreateChannel()
    {
        return new TickChannel(Options.Create(new TickChannelOptions
        {
            Capacity = 100
        }));
    }

    private static NormalizedTick CreateTick(int index)
    {
        return new NormalizedTick(
            ExchangeSource.ExchangeA,
            "BTCUSDT",
            65000m + index,
            0.1m + index,
            DateTimeOffset.Parse("2026-06-30T12:00:00.000Z").AddMilliseconds(index),
            index,
            $$"""{"sequence":{{index}}}""");
    }

    private sealed class FakeTickBatchWriter : ITickBatchWriter
    {
        private readonly object _gate = new();
        private readonly SemaphoreSlim _attemptSignal = new(0);
        private readonly SemaphoreSlim _batchSignal = new(0);
        private readonly List<IReadOnlyList<NormalizedTick>> _batches = [];
        private readonly bool _alwaysFail;
        private int _failuresBeforeSuccess;
        private int _attemptCount;

        public FakeTickBatchWriter(
            int failuresBeforeSuccess = 0,
            bool alwaysFail = false)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
            _alwaysFail = alwaysFail;
        }

        public IReadOnlyList<IReadOnlyList<NormalizedTick>> Batches
        {
            get
            {
                lock (_gate)
                {
                    return _batches.ToArray();
                }
            }
        }

        public async Task WriteBatchAsync(
            IReadOnlyCollection<NormalizedTick> ticks,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _attemptCount);
            _attemptSignal.Release();

            var shouldFail = false;

            lock (_gate)
            {
                if (_alwaysFail || _failuresBeforeSuccess > 0)
                {
                    _failuresBeforeSuccess--;
                    shouldFail = true;
                }
            }

            if (shouldFail)
            {
                throw new InvalidOperationException("Simulated database write failure.");
            }

            lock (_gate)
            {
                _batches.Add(ticks.ToArray());
            }

            _batchSignal.Release();
            await Task.CompletedTask;
        }

        public async Task WaitForBatchCountAsync(int expected)
        {
            while (Batches.Count < expected)
            {
                await _batchSignal.WaitAsync();
            }
        }

        public async Task WaitForAttemptCountAsync(int expected)
        {
            while (Volatile.Read(ref _attemptCount) < expected)
            {
                await _attemptSignal.WaitAsync();
            }
        }
    }

    private sealed class FakeBatchWriterDelay : IBatchWriterDelay
    {
        private readonly object _gate = new();
        private readonly SemaphoreSlim _delaySignal = new(0);
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
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                _delays.Add(delay);
            }

            _delaySignal.Release();
            return Task.CompletedTask;
        }

        public async Task WaitForDelayCountAsync(int expected)
        {
            while (Delays.Count < expected)
            {
                await _delaySignal.WaitAsync();
            }
        }
    }
}
