namespace Indigosoft.TestTask.Aggregator.Workers;

public interface IReconnectDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
