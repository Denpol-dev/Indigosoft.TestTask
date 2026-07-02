namespace Indigosoft.TestTask.Aggregator.Workers;

public interface IBatchWriterDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
