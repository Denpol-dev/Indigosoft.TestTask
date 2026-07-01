namespace Indigosoft.TestTask.Aggregator.WebSockets;

public sealed class ExchangeWebSocketIdleTimeoutException : Exception
{
    public ExchangeWebSocketIdleTimeoutException(string exchangeSource, TimeSpan idleTimeout)
        : base($"WebSocket for source '{exchangeSource}' did not receive a frame within {idleTimeout}.")
    {
        ExchangeSource = exchangeSource;
        IdleTimeout = idleTimeout;
    }

    public string ExchangeSource { get; }

    public TimeSpan IdleTimeout { get; }
}
