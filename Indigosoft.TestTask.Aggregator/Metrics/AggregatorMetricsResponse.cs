namespace Indigosoft.TestTask.Aggregator.Metrics;

public sealed record AggregatorMetricsResponse(
    long ReceivedMessages,
    long ParsedMessages,
    long ParseFailures,
    long DuplicateTicks,
    long EnqueuedTicks,
    long WrittenTicks,
    long DroppedTicks,
    long FailedBatches,
    int ChannelCount,
    int ChannelCapacity,
    double ChannelFillRatio);
