using Indigosoft.TestTask.Aggregator.WebSockets;
using Indigosoft.TestTask.Core.Options;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.WebSockets;

public sealed class WebSocketExchangeMessageStreamValidationTests
{
    private readonly WebSocketExchangeMessageStream _stream = new();

    [Fact]
    public void ReadMessagesAsync_WithNullConnection_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _stream.ReadMessagesAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ReadMessagesAsync_WithEmptyWebSocketUrl_ThrowsArgumentException()
    {
        var connection = new ExchangeConnectionOptions
        {
            Source = "ExchangeA",
            WebSocketUrl = ""
        };

        Assert.Throws<ArgumentException>(() =>
            _stream.ReadMessagesAsync(connection, CancellationToken.None));
    }

    [Fact]
    public void ReadMessagesAsync_WithInvalidWebSocketUrl_ThrowsArgumentException()
    {
        var connection = new ExchangeConnectionOptions
        {
            Source = "ExchangeA",
            WebSocketUrl = "not-a-url"
        };

        Assert.Throws<ArgumentException>(() =>
            _stream.ReadMessagesAsync(connection, CancellationToken.None));
    }

    [Fact]
    public void ReadMessagesAsync_WithNonWebSocketUrl_ThrowsArgumentException()
    {
        var connection = new ExchangeConnectionOptions
        {
            Source = "ExchangeA",
            WebSocketUrl = "http://localhost:5100/ws/exchange-a"
        };

        Assert.Throws<ArgumentException>(() =>
            _stream.ReadMessagesAsync(connection, CancellationToken.None));
    }

    [Fact]
    public void ReadMessagesAsync_WithValidWebSocketUrl_ReturnsEnumerableWithoutConnectingImmediately()
    {
        var connection = new ExchangeConnectionOptions
        {
            Source = "ExchangeA",
            WebSocketUrl = "ws://localhost:5100/ws/exchange-a"
        };

        var messages = _stream.ReadMessagesAsync(connection, CancellationToken.None);

        Assert.NotNull(messages);
    }
}
