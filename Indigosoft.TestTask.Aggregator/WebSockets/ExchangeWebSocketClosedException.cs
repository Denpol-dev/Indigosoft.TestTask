namespace Indigosoft.TestTask.Aggregator.WebSockets;

public sealed class ExchangeWebSocketClosedException : Exception
{
    public ExchangeWebSocketClosedException(string exchangeSource, string message)
        : base($"WebSocket for source '{exchangeSource}' was closed. {message}")
    {
        ExchangeSource = exchangeSource;
    }

    public string ExchangeSource { get; }
}
