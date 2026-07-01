using System.Net.WebSockets;
using Indigosoft.TestTask.Aggregator.Channels;
using Indigosoft.TestTask.Aggregator.Parsing;
using Indigosoft.TestTask.Aggregator.WebSockets;
using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Core.Options;

namespace Indigosoft.TestTask.Aggregator.Workers;

public sealed class ExchangeConnectionWorker
{
    private const int DefaultInitialReconnectDelayMs = 1_000;
    private const int DefaultMaxReconnectDelayMs = 30_000;

    private readonly IExchangeMessageStream _messageStream;
    private readonly ExchangeMessageParserResolver _parserResolver;
    private readonly ITickDeduplicator _deduplicator;
    private readonly TickChannel _tickChannel;
    private readonly IReconnectDelay _reconnectDelay;
    private readonly ILogger<ExchangeConnectionWorker> _logger;

    public ExchangeConnectionWorker(
        IExchangeMessageStream messageStream,
        ExchangeMessageParserResolver parserResolver,
        ITickDeduplicator deduplicator,
        TickChannel tickChannel,
        IReconnectDelay reconnectDelay,
        ILogger<ExchangeConnectionWorker> logger)
    {
        _messageStream = messageStream;
        _parserResolver = parserResolver;
        _deduplicator = deduplicator;
        _tickChannel = tickChannel;
        _reconnectDelay = reconnectDelay;
        _logger = logger;
    }

    public async Task RunAsync(
        ExchangeConnectionOptions connection,
        CancellationToken cancellationToken)
    {
        ValidateConnection(connection);

        var initialReconnectDelay = GetInitialReconnectDelay(connection);
        var maxReconnectDelay = GetMaxReconnectDelay(connection, initialReconnectDelay);
        var currentReconnectDelay = initialReconnectDelay;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation(
                    "Starting exchange connection read loop for {ExchangeSource}",
                    connection.Source);

                await foreach (var rawMessage in _messageStream.ReadMessagesAsync(connection, cancellationToken))
                {
                    currentReconnectDelay = initialReconnectDelay;

                    if (!_parserResolver.TryParse(connection.Source, rawMessage, out var tick) || tick is null)
                    {
                        _logger.LogWarning(
                            "Failed to parse message from {ExchangeSource}",
                            connection.Source);
                        continue;
                    }

                    if (!_deduplicator.TryMarkAsProcessed(tick))
                    {
                        _logger.LogDebug(
                            "Duplicate tick skipped for {ExchangeSource} {Ticker}",
                            tick.Source,
                            tick.Ticker);
                        continue;
                    }

                    await _tickChannel.EnqueueAsync(tick, cancellationToken);

                    _logger.LogDebug(
                        "Tick enqueued for {ExchangeSource} {Ticker}",
                        tick.Source,
                        tick.Ticker);
                }

                _logger.LogWarning(
                    "Exchange stream for {ExchangeSource} completed without an exception; reconnecting",
                    connection.Source);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ExchangeWebSocketClosedException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Exchange WebSocket closed for {ExchangeSource}; reconnecting",
                    connection.Source);
            }
            catch (ExchangeWebSocketIdleTimeoutException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Exchange WebSocket idle timeout for {ExchangeSource}; reconnecting",
                    connection.Source);
            }
            catch (WebSocketException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Exchange WebSocket error for {ExchangeSource}; reconnecting",
                    connection.Source);
            }
            catch (IOException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Exchange stream I/O error for {ExchangeSource}; reconnecting",
                    connection.Source);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unexpected exchange worker error for {ExchangeSource}; reconnecting",
                    connection.Source);
            }

            try
            {
                await _reconnectDelay.DelayAsync(currentReconnectDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            currentReconnectDelay = IncreaseReconnectDelay(currentReconnectDelay, maxReconnectDelay);
        }
    }

    private static void ValidateConnection(ExchangeConnectionOptions connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(connection.Source))
        {
            throw new ArgumentException("Exchange source must not be empty.", nameof(connection));
        }

        if (string.IsNullOrWhiteSpace(connection.WebSocketUrl))
        {
            throw new ArgumentException("WebSocket URL must not be empty.", nameof(connection));
        }
    }

    private static TimeSpan GetInitialReconnectDelay(ExchangeConnectionOptions connection)
    {
        var initialDelayMs = connection.InitialReconnectDelayMs > 0
            ? connection.InitialReconnectDelayMs
            : DefaultInitialReconnectDelayMs;

        return TimeSpan.FromMilliseconds(initialDelayMs);
    }

    private static TimeSpan GetMaxReconnectDelay(
        ExchangeConnectionOptions connection,
        TimeSpan initialReconnectDelay)
    {
        var maxDelayMs = connection.MaxReconnectDelayMs > 0
            ? connection.MaxReconnectDelayMs
            : DefaultMaxReconnectDelayMs;

        var maxReconnectDelay = TimeSpan.FromMilliseconds(maxDelayMs);
        return maxReconnectDelay < initialReconnectDelay
            ? initialReconnectDelay
            : maxReconnectDelay;
    }

    private static TimeSpan IncreaseReconnectDelay(
        TimeSpan currentReconnectDelay,
        TimeSpan maxReconnectDelay)
    {
        var doubledMilliseconds = currentReconnectDelay.TotalMilliseconds * 2d;
        var cappedMilliseconds = Math.Min(doubledMilliseconds, maxReconnectDelay.TotalMilliseconds);

        return TimeSpan.FromMilliseconds(cappedMilliseconds);
    }
}
