using System.Net.WebSockets;
using System.Text;
using Indigosoft.TestTask.ExchangeSimulator.Models;
using Indigosoft.TestTask.ExchangeSimulator.Options;
using Indigosoft.TestTask.ExchangeSimulator.Services;
using Microsoft.Extensions.Options;

namespace Indigosoft.TestTask.ExchangeSimulator.WebSockets;

public sealed class ExchangeWebSocketHandler
{
    private readonly IOptionsMonitor<ExchangeSimulatorOptions> _options;
    private readonly ExchangeFaultStateService _faultStateService;
    private readonly ILogger<ExchangeWebSocketHandler> _logger;
    private readonly IReadOnlyDictionary<ExchangeName, IExchangeTickGenerator> _generators;

    public ExchangeWebSocketHandler(
        IOptionsMonitor<ExchangeSimulatorOptions> options,
        ExchangeFaultStateService faultStateService,
        IEnumerable<IExchangeTickGenerator> generators,
        ILogger<ExchangeWebSocketHandler> logger)
    {
        _options = options;
        _faultStateService = faultStateService;
        _logger = logger;
        _generators = generators.ToDictionary(generator => generator.ExchangeName);
    }

    public async Task HandleAsync(HttpContext context, ExchangeName exchangeName)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Expected a WebSocket request.", context.RequestAborted);
            return;
        }

        if (!_generators.TryGetValue(exchangeName, out var generator))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync($"Generator for {exchangeName} is not registered.", context.RequestAborted);
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var receiveMonitor = MonitorCloseAsync(webSocket, connectionCancellation);
        var sendIntervalMs = GetSendIntervalMs(exchangeName);
        using var sendTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(sendIntervalMs));
        var messageAccumulator = 0d;
        string? previousMessage = null;

        _logger.LogInformation("WebSocket client connected to {ExchangeName}", exchangeName);

        try
        {
            while (webSocket.State == WebSocketState.Open
                && await sendTimer.WaitForNextTickAsync(connectionCancellation.Token))
            {
                if (await TryCloseForDisconnectOnceAsync(webSocket, exchangeName))
                {
                    break;
                }

                if (_faultStateService.GetPauseRemaining(exchangeName) > TimeSpan.Zero)
                {
                    messageAccumulator = 0d;
                    continue;
                }

                var messagesPerTick = GetMessagesPerSecond(exchangeName) * sendIntervalMs / 1000d;
                messageAccumulator += messagesPerTick;

                var messagesToSend = (int)Math.Floor(messageAccumulator);
                messageAccumulator -= messagesToSend;

                for (var messageIndex = 0; messageIndex < messagesToSend; messageIndex++)
                {
                    if (connectionCancellation.IsCancellationRequested || webSocket.State != WebSocketState.Open)
                    {
                        break;
                    }

                    if (await TryCloseForDisconnectOnceAsync(webSocket, exchangeName))
                    {
                        break;
                    }

                    if (_faultStateService.GetPauseRemaining(exchangeName) > TimeSpan.Zero)
                    {
                        messageAccumulator = 0d;
                        break;
                    }

                    var sendDuplicate = ShouldSendDuplicate(exchangeName, previousMessage);
                    var message = sendDuplicate
                        ? previousMessage!
                        : generator.GenerateTick();

                    if (!sendDuplicate)
                    {
                        previousMessage = message;
                    }

                    await SendTextAsync(webSocket, message, connectionCancellation.Token);
                }
            }
        }
        catch (OperationCanceledException) when (connectionCancellation.IsCancellationRequested)
        {
        }
        catch (WebSocketException exception)
        {
            _logger.LogInformation(exception, "WebSocket connection for {ExchangeName} ended with a socket error", exchangeName);
        }
        finally
        {
            connectionCancellation.Cancel();

            if (webSocket.State == WebSocketState.CloseReceived)
            {
                await CloseOutputAsync(webSocket);
            }
            else if (webSocket.State == WebSocketState.Open)
            {
                await CloseNormalAsync(webSocket);
            }

            await ObserveReceiveMonitorAsync(receiveMonitor);
            _logger.LogInformation("WebSocket client disconnected from {ExchangeName}", exchangeName);
        }
    }

    private bool ShouldSendDuplicate(ExchangeName exchangeName, string? previousMessage)
    {
        if (previousMessage is null || !_faultStateService.IsDuplicatesEnabled(exchangeName))
        {
            return false;
        }

        var probability = Math.Clamp(_options.CurrentValue.Faults.DuplicateProbability, 0d, 1d);
        return Random.Shared.NextDouble() < probability;
    }

    private int GetMessagesPerSecond(ExchangeName exchangeName)
    {
        var configuredMessagesPerSecond = _options.CurrentValue
            .GetExchangeOptions(exchangeName)
            .MessagesPerSecond;

        return configuredMessagesPerSecond > 0
            ? configuredMessagesPerSecond
            : 150;
    }

    private int GetSendIntervalMs(ExchangeName exchangeName)
    {
        var configuredSendIntervalMs = _options.CurrentValue
            .GetExchangeOptions(exchangeName)
            .SendIntervalMs;

        return configuredSendIntervalMs > 0
            ? configuredSendIntervalMs
            : 100;
    }

    private async Task<bool> TryCloseForDisconnectOnceAsync(WebSocket webSocket, ExchangeName exchangeName)
    {
        if (!_faultStateService.TryConsumeDisconnectOnce(exchangeName))
        {
            return false;
        }

        _logger.LogWarning("Closing WebSocket for {ExchangeName} due to disconnect-once fault", exchangeName);
        await webSocket.CloseOutputAsync(
            WebSocketCloseStatus.EndpointUnavailable,
            "Fault injected disconnect",
            CancellationToken.None);

        return true;
    }

    private static async Task SendTextAsync(WebSocket webSocket, string message, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(message);
        await webSocket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task MonitorCloseAsync(WebSocket webSocket, CancellationTokenSource connectionCancellation)
    {
        var buffer = new byte[1];

        try
        {
            while (!connectionCancellation.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(buffer, connectionCancellation.Token);
                if (result.CloseStatus.HasValue)
                {
                    connectionCancellation.Cancel();
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (connectionCancellation.IsCancellationRequested)
        {
        }
        catch (WebSocketException)
        {
            connectionCancellation.Cancel();
        }
    }

    private static async Task CloseOutputAsync(WebSocket webSocket)
    {
        try
        {
            await webSocket.CloseOutputAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client requested close",
                CancellationToken.None);
        }
        catch (WebSocketException)
        {
        }
    }

    private static async Task CloseNormalAsync(WebSocket webSocket)
    {
        try
        {
            await webSocket.CloseOutputAsync(
                WebSocketCloseStatus.NormalClosure,
                "Server closing connection",
                CancellationToken.None);
        }
        catch (WebSocketException)
        {
        }
    }

    private static async Task ObserveReceiveMonitorAsync(Task receiveMonitor)
    {
        try
        {
            await receiveMonitor;
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
    }
}
