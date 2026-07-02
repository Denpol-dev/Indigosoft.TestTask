using Indigosoft.TestTask.Core.Options;

namespace Indigosoft.TestTask.Aggregator.Workers;

public interface IExchangeConnectionWorker
{
    Task RunAsync(
        ExchangeConnectionOptions connection,
        CancellationToken cancellationToken);
}
