using Indigosoft.TestTask.Aggregator.Metrics;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Metrics;

public sealed class TickIngestionStatisticsTests
{
    [Fact]
    public void IncrementReceivedMessages_IncreasesCounter()
    {
        var statistics = new TickIngestionStatistics();

        statistics.IncrementReceivedMessages();

        Assert.Equal(1, statistics.ReceivedMessages);
    }

    [Fact]
    public void IncrementParsedMessages_IncreasesCounter()
    {
        var statistics = new TickIngestionStatistics();

        statistics.IncrementParsedMessages();

        Assert.Equal(1, statistics.ParsedMessages);
    }

    [Fact]
    public void IncrementParseFailures_IncreasesCounter()
    {
        var statistics = new TickIngestionStatistics();

        statistics.IncrementParseFailures();

        Assert.Equal(1, statistics.ParseFailures);
    }

    [Fact]
    public void IncrementDuplicateTicks_IncreasesCounter()
    {
        var statistics = new TickIngestionStatistics();

        statistics.IncrementDuplicateTicks();

        Assert.Equal(1, statistics.DuplicateTicks);
    }

    [Fact]
    public void IncrementEnqueuedTicks_IncreasesCounter()
    {
        var statistics = new TickIngestionStatistics();

        statistics.IncrementEnqueuedTicks();

        Assert.Equal(1, statistics.EnqueuedTicks);
    }

    [Fact]
    public async Task ConcurrentIncrements_AreThreadSafe()
    {
        var statistics = new TickIngestionStatistics();
        var tasks = Enumerable.Range(0, 1_000)
            .Select(_ => Task.Run(statistics.IncrementReceivedMessages));

        await Task.WhenAll(tasks);

        Assert.Equal(1_000, statistics.ReceivedMessages);
    }
}
