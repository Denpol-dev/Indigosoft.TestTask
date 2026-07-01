using Indigosoft.TestTask.Aggregator.Channels;
using Indigosoft.TestTask.Core.Models;
using Indigosoft.TestTask.Core.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Channels;

public sealed class TickChannelTests
{
    [Fact]
    public async Task EnqueueAsync_WithTick_IncreasesCount()
    {
        var channel = CreateChannel();

        await channel.EnqueueAsync(CreateTick(), CancellationToken.None);

        Assert.Equal(1, channel.Count);
    }

    [Fact]
    public async Task ReadAsync_AfterEnqueue_ReturnsSameTickAndDecreasesCount()
    {
        var channel = CreateChannel();
        var tick = CreateTick();

        await channel.EnqueueAsync(tick, CancellationToken.None);
        var result = await channel.ReadAsync(CancellationToken.None);

        Assert.Same(tick, result);
        Assert.Equal(0, channel.Count);
    }

    [Fact]
    public async Task FillRatio_ReturnsExpectedValue()
    {
        var channel = CreateChannel(capacity: 2);

        await channel.EnqueueAsync(CreateTick(), CancellationToken.None);

        Assert.Equal(0.5d, channel.FillRatio);
    }

    [Fact]
    public async Task EnqueueAsync_WithNullTick_ThrowsArgumentNullException()
    {
        var channel = CreateChannel();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await channel.EnqueueAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Complete_AfterExistingItems_AllowsReadingAlreadyQueuedItems()
    {
        var channel = CreateChannel();
        var tick = CreateTick();

        await channel.EnqueueAsync(tick, CancellationToken.None);
        channel.Complete();
        var result = await channel.ReadAsync(CancellationToken.None);

        Assert.Same(tick, result);
    }

    [Fact]
    public async Task EnqueueAsync_WhenChannelIsFull_WaitsUntilReaderConsumesItem()
    {
        var channel = CreateChannel(capacity: 1);
        var firstTick = CreateTick(sequence: 1);
        var secondTick = CreateTick(sequence: 2);

        await channel.EnqueueAsync(firstTick, CancellationToken.None);

        var secondEnqueue = channel.EnqueueAsync(secondTick, CancellationToken.None).AsTask();
        var completedBeforeRead = await Task.WhenAny(secondEnqueue, Task.Delay(TimeSpan.FromMilliseconds(100)));

        Assert.NotSame(secondEnqueue, completedBeforeRead);

        var result = await channel.ReadAsync(CancellationToken.None);

        Assert.Same(firstTick, result);
        await secondEnqueue.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(1, channel.Count);
    }

    [Fact]
    public async Task EnqueueAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var channel = CreateChannel(capacity: 1);
        using var cancellationTokenSource = new CancellationTokenSource();

        await channel.EnqueueAsync(CreateTick(sequence: 1), CancellationToken.None);

        var secondEnqueue = channel.EnqueueAsync(CreateTick(sequence: 2), cancellationTokenSource.Token).AsTask();
        var completedBeforeCancellation = await Task.WhenAny(secondEnqueue, Task.Delay(TimeSpan.FromMilliseconds(100)));

        Assert.NotSame(secondEnqueue, completedBeforeCancellation);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await secondEnqueue);
        Assert.Equal(1, channel.Count);
    }

    private static TickChannel CreateChannel(int capacity = 100_000)
    {
        return new TickChannel(Options.Create(new TickChannelOptions
        {
            Capacity = capacity
        }));
    }

    private static NormalizedTick CreateTick(long? sequence = 1)
    {
        return new NormalizedTick(
            ExchangeSource.ExchangeA,
            "BTCUSDT",
            65000.12m,
            0.42m,
            DateTimeOffset.Parse("2026-06-30T12:00:00.000Z"),
            sequence,
            "{}");
    }
}
