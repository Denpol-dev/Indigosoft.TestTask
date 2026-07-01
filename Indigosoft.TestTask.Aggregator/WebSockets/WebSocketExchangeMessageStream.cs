using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using Indigosoft.TestTask.Core.Options;

namespace Indigosoft.TestTask.Aggregator.WebSockets;

public sealed class WebSocketExchangeMessageStream : IExchangeMessageStream
{
    private const int ReceiveBufferSize = 16 * 1024;
    private const int DefaultIdleTimeoutSeconds = 10;

    public IAsyncEnumerable<string> ReadMessagesAsync(
        ExchangeConnectionOptions connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(connection.WebSocketUrl))
        {
            throw new ArgumentException("WebSocket URL must not be empty.", nameof(connection));
        }

        if (!Uri.TryCreate(connection.WebSocketUrl, UriKind.Absolute, out var webSocketUri)
            || !IsWebSocketUri(webSocketUri))
        {
            throw new ArgumentException("WebSocket URL must be a valid absolute ws:// or wss:// URI.", nameof(connection));
        }

        var idleTimeoutSeconds = connection.IdleTimeoutSeconds > 0
            ? connection.IdleTimeoutSeconds
            : DefaultIdleTimeoutSeconds;

        return ReadMessagesCoreAsync(
            connection.Source,
            webSocketUri,
            TimeSpan.FromSeconds(idleTimeoutSeconds),
            cancellationToken);
    }

    private static async IAsyncEnumerable<string> ReadMessagesCoreAsync(
        string source,
        Uri webSocketUri,
        TimeSpan idleTimeout,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var webSocket = new ClientWebSocket();
        await webSocket.ConnectAsync(webSocketUri, cancellationToken);

        var buffer = new byte[ReceiveBufferSize];

        while (webSocket.State == WebSocketState.Open)
        {
            using var messageStream = new MemoryStream();
            var skipMessage = false;
            WebSocketReceiveResult receiveResult;

            do
            {
                receiveResult = await ReceiveFrameAsync(
                    webSocket,
                    buffer,
                    source,
                    idleTimeout,
                    cancellationToken);

                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await CloseAfterCloseFrameAsync(webSocket);
                    throw new ExchangeWebSocketClosedException(
                        source,
                        CreateCloseMessage(receiveResult));
                }

                if (receiveResult.MessageType == WebSocketMessageType.Text && !skipMessage)
                {
                    messageStream.Write(buffer, 0, receiveResult.Count);
                }
                else
                {
                    skipMessage = true;
                }
            }
            while (!receiveResult.EndOfMessage);

            if (!skipMessage)
            {
                yield return Encoding.UTF8.GetString(messageStream.ToArray());
            }
        }
    }

    private static async Task<WebSocketReceiveResult> ReceiveFrameAsync(
        ClientWebSocket webSocket,
        byte[] buffer,
        string source,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken)
    {
        using var idleCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idleCancellation.CancelAfter(idleTimeout);

        try
        {
            return await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                idleCancellation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExchangeWebSocketIdleTimeoutException(source, idleTimeout);
        }
    }

    private static async Task CloseAfterCloseFrameAsync(ClientWebSocket webSocket)
    {
        try
        {
            if (webSocket.State == WebSocketState.CloseReceived)
            {
                await webSocket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Close frame received",
                    CancellationToken.None);
            }
            else if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Close frame received",
                    CancellationToken.None);
            }
        }
        catch (WebSocketException)
        {
        }
    }

    private static bool IsWebSocketUri(Uri uri)
    {
        return string.Equals(uri.Scheme, "ws", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateCloseMessage(WebSocketReceiveResult receiveResult)
    {
        var closeStatus = receiveResult.CloseStatus?.ToString() ?? "Unknown";
        var description = string.IsNullOrWhiteSpace(receiveResult.CloseStatusDescription)
            ? "No close description was provided."
            : receiveResult.CloseStatusDescription;

        return $"Status: {closeStatus}. Description: {description}";
    }
}
