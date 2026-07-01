using Indigosoft.TestTask.Aggregator.WebSockets;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.WebSockets;

public sealed class ExchangeWebSocketExceptionTests
{
    [Fact]
    public void ExchangeWebSocketClosedException_ExposesSourceAndMessage()
    {
        var exception = new ExchangeWebSocketClosedException("ExchangeA", "Status: NormalClosure.");

        Assert.Equal("ExchangeA", exception.ExchangeSource);
        Assert.Contains("ExchangeA", exception.Message);
        Assert.Contains("Status: NormalClosure.", exception.Message);
    }

    [Fact]
    public void ExchangeWebSocketIdleTimeoutException_ExposesSourceAndIdleTimeout()
    {
        var idleTimeout = TimeSpan.FromSeconds(10);

        var exception = new ExchangeWebSocketIdleTimeoutException("ExchangeB", idleTimeout);

        Assert.Equal("ExchangeB", exception.ExchangeSource);
        Assert.Equal(idleTimeout, exception.IdleTimeout);
        Assert.Contains("ExchangeB", exception.Message);
    }
}
