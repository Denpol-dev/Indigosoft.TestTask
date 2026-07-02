namespace Indigosoft.TestTask.Aggregator.Workers;

public sealed class TickWriteStatistics
{
    private long _writtenTicks;
    private long _droppedTicks;
    private long _failedBatches;

    public long WrittenTicks => Volatile.Read(ref _writtenTicks);

    public long DroppedTicks => Volatile.Read(ref _droppedTicks);

    public long FailedBatches => Volatile.Read(ref _failedBatches);

    public void AddWrittenTicks(int count)
    {
        Interlocked.Add(ref _writtenTicks, count);
    }

    public void AddDroppedTicks(int count)
    {
        Interlocked.Add(ref _droppedTicks, count);
    }

    public void IncrementFailedBatches()
    {
        Interlocked.Increment(ref _failedBatches);
    }
}
