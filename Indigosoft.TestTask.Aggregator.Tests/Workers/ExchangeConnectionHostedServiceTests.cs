using System.Collections.Concurrent;
using Indigosoft.TestTask.Aggregator.Channels;
using Indigosoft.TestTask.Aggregator.Workers;
using Indigosoft.TestTask.Core.Models;
using Indigosoft.TestTask.Core.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Workers;

public sealed class ExchangeConnectionHostedServiceTests
{
    [Fact]
    public async Task StartAsync_WithConfiguredExchanges_StartsWorkerForEveryExchange()
    {
        var fakeWorker = new FakeExchangeConnectionWorker();
        var service = CreateService(fakeWorker, CreateOptions(
            ExchangeSource.ExchangeA,
            ExchangeSource.ExchangeB,
            ExchangeSource.ExchangeC));

        await service.StartAsync(CancellationToken.None);
        await fakeWorker.WaitForStartedCountAsync(3).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(3, fakeWorker.StartedCount);
        Assert.Contains(ExchangeSource.ExchangeA, fakeWorker.StartedSources);
        Assert.Contains(ExchangeSource.ExchangeB, fakeWorker.StartedSources);
        Assert.Contains(ExchangeSource.ExchangeC, fakeWorker.StartedSources);

        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task StopAsync_CancelsWorkersAndCompletes()
    {
        var fakeWorker = new FakeExchangeConnectionWorker();
        var service = CreateService(fakeWorker, CreateOptions(ExchangeSource.ExchangeA));

        await service.StartAsync(CancellationToken.None);
        await fakeWorker.WaitForStartedCountAsync(1).WaitAsync(TimeSpan.FromSeconds(2));

        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, fakeWorker.StartedCount);
    }

    [Fact]
    public async Task StartAsync_WithNoExchanges_DoesNotThrow()
    {
        var fakeWorker = new FakeExchangeConnectionWorker();
        var service = CreateService(fakeWorker, new AggregatorOptions());

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0, fakeWorker.StartedCount);
    }

    [Fact]
    public async Task WorkerException_DoesNotStopOtherWorkers()
    {
        var fakeWorker = new FakeExchangeConnectionWorker(ExchangeSource.ExchangeA);
        var service = CreateService(fakeWorker, CreateOptions(
            ExchangeSource.ExchangeA,
            ExchangeSource.ExchangeB));

        await service.StartAsync(CancellationToken.None);
        await fakeWorker.WaitForStartedCountAsync(2).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains(ExchangeSource.ExchangeB, fakeWorker.StartedSources);

        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static ExchangeConnectionHostedService CreateService(
        FakeExchangeConnectionWorker fakeWorker,
        AggregatorOptions options)
    {
        return new ExchangeConnectionHostedService(
            Options.Create(options),
            fakeWorker,
            new TickChannel(Options.Create(new TickChannelOptions { Capacity = 10 })),
            NullLogger<ExchangeConnectionHostedService>.Instance);
    }

    private static AggregatorOptions CreateOptions(params string[] sources)
    {
        return new AggregatorOptions
        {
            Exchanges = sources
                .Select(source => new ExchangeConnectionOptions
                {
                    Source = source,
                    WebSocketUrl = $"ws://localhost:5100/ws/{source.ToLowerInvariant()}"
                })
                .ToList()
        };
    }

    private sealed class FakeExchangeConnectionWorker(string? throwingSource = null) : IExchangeConnectionWorker
    {
        private readonly ConcurrentQueue<string> _startedSources = new();
        private readonly SemaphoreSlim _startedSignal = new(0);
        private int _startedCount;

        public int StartedCount => Volatile.Read(ref _startedCount);

        public IReadOnlyCollection<string> StartedSources => _startedSources.ToArray();

        public async Task RunAsync(
            ExchangeConnectionOptions connection,
            CancellationToken cancellationToken)
        {
            _startedSources.Enqueue(connection.Source);
            Interlocked.Increment(ref _startedCount);
            _startedSignal.Release();

            if (string.Equals(connection.Source, throwingSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Boom for {connection.Source}");
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public async Task WaitForStartedCountAsync(int expected)
        {
            while (StartedCount < expected)
            {
                await _startedSignal.WaitAsync();
            }
        }
    }
}
