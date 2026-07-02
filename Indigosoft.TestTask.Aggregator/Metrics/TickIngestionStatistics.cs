namespace Indigosoft.TestTask.Aggregator.Metrics;

public sealed class TickIngestionStatistics
{
    private long _receivedMessages;
    private long _parsedMessages;
    private long _parseFailures;
    private long _duplicateTicks;
    private long _enqueuedTicks;

    public long ReceivedMessages => Volatile.Read(ref _receivedMessages);

    public long ParsedMessages => Volatile.Read(ref _parsedMessages);

    public long ParseFailures => Volatile.Read(ref _parseFailures);

    public long DuplicateTicks => Volatile.Read(ref _duplicateTicks);

    public long EnqueuedTicks => Volatile.Read(ref _enqueuedTicks);

    public void IncrementReceivedMessages()
    {
        Interlocked.Increment(ref _receivedMessages);
    }

    public void IncrementParsedMessages()
    {
        Interlocked.Increment(ref _parsedMessages);
    }

    public void IncrementParseFailures()
    {
        Interlocked.Increment(ref _parseFailures);
    }

    public void IncrementDuplicateTicks()
    {
        Interlocked.Increment(ref _duplicateTicks);
    }

    public void IncrementEnqueuedTicks()
    {
        Interlocked.Increment(ref _enqueuedTicks);
    }
}
