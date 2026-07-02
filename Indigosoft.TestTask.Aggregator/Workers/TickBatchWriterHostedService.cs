using System.Diagnostics;
using Indigosoft.TestTask.Aggregator.Channels;
using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Core.Models;
using Indigosoft.TestTask.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Indigosoft.TestTask.Aggregator.Workers;

public sealed class TickBatchWriterHostedService : BackgroundService
{
    private const int DefaultBatchSize = 500;
    private const int DefaultFlushIntervalMs = 1_000;
    private const int DefaultInitialRetryDelayMs = 500;
    private const int DefaultMaxRetryDelayMs = 5_000;
    private const int DefaultDrainTimeoutSeconds = 15;

    private readonly TickChannel _tickChannel;
    private readonly ITickBatchWriter _tickBatchWriter;
    private readonly IOptions<BatchWriterOptions> _options;
    private readonly IBatchWriterDelay _delay;
    private readonly TickWriteStatistics _statistics;
    private readonly ILogger<TickBatchWriterHostedService> _logger;

    public TickBatchWriterHostedService(
        TickChannel tickChannel,
        ITickBatchWriter tickBatchWriter,
        IOptions<BatchWriterOptions> options,
        IBatchWriterDelay delay,
        TickWriteStatistics statistics,
        ILogger<TickBatchWriterHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(tickChannel);
        ArgumentNullException.ThrowIfNull(tickBatchWriter);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(delay);
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(logger);

        _tickChannel = tickChannel;
        _tickBatchWriter = tickBatchWriter;
        _options = options;
        _delay = delay;
        _statistics = statistics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = CreateSettings(_options.Value);
        var batch = new List<NormalizedTick>(settings.BatchSize);
        var lastFlushTimestamp = Stopwatch.GetTimestamp();

        try
        {
            _logger.LogInformation(
                "Tick batch writer started with batch size {BatchSize} and flush interval {FlushIntervalMs} ms.",
                settings.BatchSize,
                settings.FlushInterval.TotalMilliseconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                var waitTimeout = GetReadWaitTimeout(batch, settings, lastFlushTimestamp);

                try
                {
                    using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                    if (waitTimeout != Timeout.InfiniteTimeSpan)
                    {
                        waitCancellation.CancelAfter(waitTimeout);
                    }

                    var canRead = await _tickChannel.WaitToReadAsync(waitCancellation.Token);

                    if (!canRead)
                    {
                        break;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException) when (batch.Count > 0)
                {
                    lastFlushTimestamp = await FlushBatchAsync(
                        batch,
                        settings,
                        stoppingToken,
                        lastFlushTimestamp);
                    continue;
                }

                lastFlushTimestamp = await ReadAvailableTicksAsync(
                    batch,
                    settings,
                    stoppingToken,
                    lastFlushTimestamp);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Tick batch writer stopping. Draining accepted ticks.");
        }
        finally
        {
            await DrainRemainingTicksAsync(batch, settings, lastFlushTimestamp);
            _logger.LogInformation("Tick batch writer stopped.");
        }
    }

    private async Task<long> ReadAvailableTicksAsync(
        List<NormalizedTick> batch,
        BatchWriterSettings settings,
        CancellationToken cancellationToken,
        long lastFlushTimestamp)
    {
        var currentLastFlushTimestamp = lastFlushTimestamp;

        while (_tickChannel.TryRead(out var tick))
        {
            if (tick is null)
            {
                continue;
            }

            batch.Add(tick);

            if (batch.Count >= settings.BatchSize)
            {
                currentLastFlushTimestamp = await FlushBatchAsync(
                    batch,
                    settings,
                    cancellationToken,
                    currentLastFlushTimestamp);
            }
        }

        if (batch.Count > 0 && GetElapsed(currentLastFlushTimestamp) >= settings.FlushInterval)
        {
            currentLastFlushTimestamp = await FlushBatchAsync(
                batch,
                settings,
                cancellationToken,
                currentLastFlushTimestamp);
        }

        return currentLastFlushTimestamp;
    }

    private async Task DrainRemainingTicksAsync(
        List<NormalizedTick> batch,
        BatchWriterSettings settings,
        long lastFlushTimestamp)
    {
        using var drainCancellation = new CancellationTokenSource(settings.DrainTimeout);
        var currentLastFlushTimestamp = lastFlushTimestamp;

        try
        {
            while (_tickChannel.TryRead(out var tick))
            {
                if (tick is null)
                {
                    continue;
                }

                batch.Add(tick);

                if (batch.Count >= settings.BatchSize)
                {
                    currentLastFlushTimestamp = await FlushBatchAsync(
                        batch,
                        settings,
                        drainCancellation.Token,
                        currentLastFlushTimestamp);
                }
            }

            await FlushBatchAsync(
                batch,
                settings,
                drainCancellation.Token,
                currentLastFlushTimestamp);
        }
        catch (OperationCanceledException) when (drainCancellation.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Tick batch writer drain timed out after {DrainTimeoutSeconds} seconds.",
                settings.DrainTimeout.TotalSeconds);

            DropBatchAndUnreadTicks(batch);
        }
    }

    private async Task<long> FlushBatchAsync(
        List<NormalizedTick> batch,
        BatchWriterSettings settings,
        CancellationToken cancellationToken,
        long lastFlushTimestamp)
    {
        if (batch.Count == 0)
        {
            return lastFlushTimestamp;
        }

        var snapshot = batch.ToArray();

        await WriteBatchWithRetryAsync(snapshot, settings, cancellationToken);
        batch.Clear();

        return Stopwatch.GetTimestamp();
    }

    private async Task WriteBatchWithRetryAsync(
        IReadOnlyCollection<NormalizedTick> batch,
        BatchWriterSettings settings,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        var attempt = 1;
        var currentDelay = settings.InitialRetryDelay;

        while (true)
        {
            try
            {
                await _tickBatchWriter.WriteBatchAsync(batch, cancellationToken);
                _statistics.AddWrittenTicks(batch.Count);
                _logger.LogDebug("Written tick batch with {TickCount} ticks.", batch.Count);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (attempt > settings.MaxWriteRetries)
                {
                    _statistics.IncrementFailedBatches();
                    _statistics.AddDroppedTicks(batch.Count);

                    _logger.LogError(
                        exception,
                        "Failed to write tick batch after {AttemptCount} attempts. Dropping {TickCount} ticks.",
                        attempt,
                        batch.Count);

                    return;
                }

                _logger.LogWarning(
                    exception,
                    "Failed to write tick batch on attempt {Attempt}. Retrying in {DelayMs} ms.",
                    attempt,
                    currentDelay.TotalMilliseconds);

                await _delay.DelayAsync(currentDelay, cancellationToken);

                currentDelay = NextDelay(currentDelay, settings.MaxRetryDelay);
                attempt++;
            }
        }
    }

    private void DropBatchAndUnreadTicks(List<NormalizedTick> batch)
    {
        var unreadTicks = _tickChannel.Count;
        var droppedTicks = batch.Count + unreadTicks;

        if (droppedTicks == 0)
        {
            return;
        }

        _statistics.IncrementFailedBatches();
        _statistics.AddDroppedTicks(droppedTicks);

        _logger.LogError(
            "Dropping {DroppedTickCount} ticks because batch writer drain did not complete. {BatchTickCount} ticks were already batched and {UnreadTickCount} ticks remained unread in the channel.",
            droppedTicks,
            batch.Count,
            unreadTicks);

        batch.Clear();
    }

    private static BatchWriterSettings CreateSettings(BatchWriterOptions options)
    {
        var initialRetryDelayMs = options.InitialRetryDelayMs > 0
            ? options.InitialRetryDelayMs
            : DefaultInitialRetryDelayMs;

        var maxRetryDelayMs = options.MaxRetryDelayMs > 0
            ? options.MaxRetryDelayMs
            : DefaultMaxRetryDelayMs;

        if (maxRetryDelayMs < initialRetryDelayMs)
        {
            maxRetryDelayMs = initialRetryDelayMs;
        }

        return new BatchWriterSettings(
            options.BatchSize > 0 ? options.BatchSize : DefaultBatchSize,
            TimeSpan.FromMilliseconds(options.FlushIntervalMs > 0
                ? options.FlushIntervalMs
                : DefaultFlushIntervalMs),
            Math.Max(0, options.MaxWriteRetries),
            TimeSpan.FromMilliseconds(initialRetryDelayMs),
            TimeSpan.FromMilliseconds(maxRetryDelayMs),
            TimeSpan.FromSeconds(options.DrainTimeoutSeconds > 0
                ? options.DrainTimeoutSeconds
                : DefaultDrainTimeoutSeconds));
    }

    private static TimeSpan GetReadWaitTimeout(
        IReadOnlyCollection<NormalizedTick> batch,
        BatchWriterSettings settings,
        long lastFlushTimestamp)
    {
        if (batch.Count == 0)
        {
            return Timeout.InfiniteTimeSpan;
        }

        var remaining = settings.FlushInterval - GetElapsed(lastFlushTimestamp);

        return remaining > TimeSpan.Zero
            ? remaining
            : TimeSpan.Zero;
    }

    private static TimeSpan GetElapsed(long timestamp)
    {
        return TimeSpan.FromSeconds((double)(Stopwatch.GetTimestamp() - timestamp) / Stopwatch.Frequency);
    }

    private static TimeSpan NextDelay(TimeSpan currentDelay, TimeSpan maxDelay)
    {
        var nextDelayMs = Math.Min(currentDelay.TotalMilliseconds * 2d, maxDelay.TotalMilliseconds);

        return TimeSpan.FromMilliseconds(nextDelayMs);
    }

    private sealed record BatchWriterSettings(
        int BatchSize,
        TimeSpan FlushInterval,
        int MaxWriteRetries,
        TimeSpan InitialRetryDelay,
        TimeSpan MaxRetryDelay,
        TimeSpan DrainTimeout);
}
