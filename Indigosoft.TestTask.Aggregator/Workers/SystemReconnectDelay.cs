namespace Indigosoft.TestTask.Aggregator.Workers;

public sealed class SystemReconnectDelay : IReconnectDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
