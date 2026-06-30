namespace Indigosoft.TestTask.Core.Options;

public sealed class AggregatorOptions
{
    public List<ExchangeConnectionOptions> Exchanges { get; init; } = [];

    public TickChannelOptions Channel { get; init; } = new();

    public DeduplicationOptions Deduplication { get; init; } = new();

    public BatchWriterOptions BatchWriter { get; init; } = new();
}
