namespace Indigosoft.TestTask.Aggregator.Workers;

public sealed class SystemBatchWriterDelay : IBatchWriterDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
