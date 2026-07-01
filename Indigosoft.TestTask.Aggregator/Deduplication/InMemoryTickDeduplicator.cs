using System.Collections.Concurrent;
using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Core.Models;
using Indigosoft.TestTask.Core.Options;
using Microsoft.Extensions.Options;

namespace Indigosoft.TestTask.Aggregator.Deduplication;

public sealed class InMemoryTickDeduplicator : ITickDeduplicator
{
    private readonly ConcurrentDictionary<DeduplicationKey, DateTimeOffset> _processedTicks = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _window;
    private readonly TimeSpan _cleanupInterval;
    private long _lastCleanupUnixMilliseconds;

    public InMemoryTickDeduplicator(
        IOptions<DeduplicationOptions> options,
        TimeProvider? timeProvider = null)
        : this(options?.Value ?? throw new ArgumentNullException(nameof(options)), timeProvider)
    {
    }

    internal InMemoryTickDeduplicator(
        DeduplicationOptions options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _timeProvider = timeProvider ?? TimeProvider.System;
        _window = TimeSpan.FromSeconds(options.WindowSeconds > 0 ? options.WindowSeconds : 60);
        _cleanupInterval = TimeSpan.FromSeconds(options.CleanupIntervalSeconds > 0 ? options.CleanupIntervalSeconds : 10);
    }

    public bool TryMarkAsProcessed(NormalizedTick tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

        var now = _timeProvider.GetUtcNow();
        TryCleanup(now);

        var key = DeduplicationKey.FromTick(tick);

        while (true)
        {
            if (!_processedTicks.TryGetValue(key, out var processedAt))
            {
                if (_processedTicks.TryAdd(key, now))
                {
                    return true;
                }

                continue;
            }

            if (now - processedAt <= _window)
            {
                return false;
            }

            if (_processedTicks.TryUpdate(key, now, processedAt))
            {
                return true;
            }
        }
    }

    private void TryCleanup(DateTimeOffset now)
    {
        var nowUnixMilliseconds = now.ToUnixTimeMilliseconds();
        var cleanupIntervalMilliseconds = (long)_cleanupInterval.TotalMilliseconds;
        var lastCleanupUnixMilliseconds = Volatile.Read(ref _lastCleanupUnixMilliseconds);

        if (nowUnixMilliseconds - lastCleanupUnixMilliseconds < cleanupIntervalMilliseconds)
        {
            return;
        }

        if (Interlocked.CompareExchange(
                ref _lastCleanupUnixMilliseconds,
                nowUnixMilliseconds,
                lastCleanupUnixMilliseconds) != lastCleanupUnixMilliseconds)
        {
            return;
        }

        var expiresBefore = now - _window;
        foreach (var processedTick in _processedTicks)
        {
            if (processedTick.Value < expiresBefore)
            {
                ((ICollection<KeyValuePair<DeduplicationKey, DateTimeOffset>>)_processedTicks).Remove(processedTick);
            }
        }
    }
}
