using Indigosoft.TestTask.Core.Options;

namespace Indigosoft.TestTask.Aggregator.WebSockets;

public interface IExchangeMessageStream
{
    IAsyncEnumerable<string> ReadMessagesAsync(
        ExchangeConnectionOptions connection,
        CancellationToken cancellationToken);
}
