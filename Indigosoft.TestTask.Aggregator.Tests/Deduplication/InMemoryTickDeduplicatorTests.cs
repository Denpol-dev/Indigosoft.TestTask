using Indigosoft.TestTask.Aggregator.Deduplication;
using Indigosoft.TestTask.Core.Models;
using Indigosoft.TestTask.Core.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Deduplication;

public sealed class InMemoryTickDeduplicatorTests
{
    [Fact]
    public void TryMarkAsProcessed_WithNewTick_ReturnsTrue()
    {
        var deduplicator = CreateDeduplicator();
        var tick = CreateTick();

        var result = deduplicator.TryMarkAsProcessed(tick);

        Assert.True(result);
    }

    [Fact]
    public void TryMarkAsProcessed_WithSameTickTwice_ReturnsFalseOnSecondCall()
    {
        var deduplicator = CreateDeduplicator();
        var tick = CreateTick();

        var firstResult = deduplicator.TryMarkAsProcessed(tick);
        var secondResult = deduplicator.TryMarkAsProcessed(tick);

        Assert.True(firstResult);
        Assert.False(secondResult);
    }

    [Fact]
    public void TryMarkAsProcessed_WithDifferentPrice_ReturnsTrue()
    {
        var deduplicator = CreateDeduplicator();

        Assert.True(deduplicator.TryMarkAsProcessed(CreateTick(price: 65000.12m)));
        Assert.True(deduplicator.TryMarkAsProcessed(CreateTick(price: 65000.13m)));
    }

    [Fact]
    public void TryMarkAsProcessed_WithDifferentVolume_ReturnsTrue()
    {
        var deduplicator = CreateDeduplicator();

        Assert.True(deduplicator.TryMarkAsProcessed(CreateTick(volume: 0.42m)));
        Assert.True(deduplicator.TryMarkAsProcessed(CreateTick(volume: 0.43m)));
    }

    [Fact]
    public void TryMarkAsProcessed_WithDifferentTimestamp_ReturnsTrue()
    {
        var deduplicator = CreateDeduplicator();
        var timestamp = DateTimeOffset.Parse("2026-06-30T12:00:00.000Z");

        Assert.True(deduplicator.TryMarkAsProcessed(CreateTick(timestamp: timestamp)));
        Assert.True(deduplicator.TryMarkAsProcessed(CreateTick(timestamp: timestamp.AddMilliseconds(1))));
    }

    [Fact]
    public void TryMarkAsProcessed_WithSameTickButDifferentSequence_ReturnsFalse()
    {
        var deduplicator = CreateDeduplicator();

        Assert.True(deduplicator.TryMarkAsProcessed(CreateTick(sequence: 123)));
        Assert.False(deduplicator.TryMarkAsProcessed(CreateTick(sequence: 456)));
    }

    [Fact]
    public void TryMarkAsProcessed_AfterWindowExpired_ReturnsTrueAgain()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.Parse("2026-06-30T12:00:00.000Z"));
        var deduplicator = CreateDeduplicator(
            new DeduplicationOptions
            {
                WindowSeconds = 60,
                CleanupIntervalSeconds = 10
            },
            timeProvider);
        var tick = CreateTick();

        Assert.True(deduplicator.TryMarkAsProcessed(tick));

        timeProvider.Advance(TimeSpan.FromSeconds(61));

        Assert.True(deduplicator.TryMarkAsProcessed(tick));
    }

    [Fact]
    public async Task TryMarkAsProcessed_WhenCalledConcurrentlyForSameTick_AllowsOnlyOne()
    {
        var deduplicator = CreateDeduplicator();
        var tick = CreateTick();
        var tasks = Enumerable
            .Range(0, 1000)
            .Select(_ => Task.Run(() => deduplicator.TryMarkAsProcessed(tick)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(result => result));
    }

    [Fact]
    public void TryMarkAsProcessed_WithNullTick_ThrowsArgumentNullException()
    {
        var deduplicator = CreateDeduplicator();

        Assert.Throws<ArgumentNullException>(() => deduplicator.TryMarkAsProcessed(null!));
    }

    private static InMemoryTickDeduplicator CreateDeduplicator(
        DeduplicationOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        return new InMemoryTickDeduplicator(
            Options.Create(options ?? new DeduplicationOptions()),
            timeProvider);
    }

    private static NormalizedTick CreateTick(
        decimal price = 65000.12m,
        decimal volume = 0.42m,
        DateTimeOffset? timestamp = null,
        long? sequence = 123)
    {
        return new NormalizedTick(
            ExchangeSource.ExchangeA,
            "BTCUSDT",
            price,
            volume,
            timestamp ?? DateTimeOffset.Parse("2026-06-30T12:00:00.000Z"),
            sequence,
            "{}");
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan timeSpan)
        {
            _utcNow = _utcNow.Add(timeSpan);
        }
    }
}
