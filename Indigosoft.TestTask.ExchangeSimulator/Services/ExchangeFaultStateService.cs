using Indigosoft.TestTask.ExchangeSimulator.Models;

namespace Indigosoft.TestTask.ExchangeSimulator.Services;

public sealed class ExchangeFaultStateService(ILogger<ExchangeFaultStateService> logger)
{
    private readonly object _gate = new();
    private readonly Dictionary<ExchangeName, MutableFaultState> _states = new()
    {
        [ExchangeName.ExchangeA] = new MutableFaultState(),
        [ExchangeName.ExchangeB] = new MutableFaultState(),
        [ExchangeName.ExchangeC] = new MutableFaultState()
    };

    public IReadOnlyCollection<FaultState> GetAll()
    {
        lock (_gate)
        {
            return _states
                .OrderBy(state => state.Key)
                .Select(state => CreateSnapshot(state.Key, state.Value, DateTimeOffset.UtcNow))
                .ToArray();
        }
    }

    public FaultState RequestDisconnectOnce(ExchangeName exchangeName)
    {
        lock (_gate)
        {
            var state = _states[exchangeName];
            state.DisconnectOncePending = true;
            logger.LogWarning("Disconnect-once fault requested for {ExchangeName}", exchangeName);

            return CreateSnapshot(exchangeName, state, DateTimeOffset.UtcNow);
        }
    }

    public bool TryConsumeDisconnectOnce(ExchangeName exchangeName)
    {
        lock (_gate)
        {
            var state = _states[exchangeName];
            if (!state.DisconnectOncePending)
            {
                return false;
            }

            state.DisconnectOncePending = false;
            return true;
        }
    }

    public FaultState SetDuplicatesEnabled(ExchangeName exchangeName, bool enabled)
    {
        lock (_gate)
        {
            var state = _states[exchangeName];
            state.DuplicatesEnabled = enabled;
            logger.LogWarning("Duplicate fault mode {State} for {ExchangeName}", enabled ? "enabled" : "disabled", exchangeName);

            return CreateSnapshot(exchangeName, state, DateTimeOffset.UtcNow);
        }
    }

    public bool IsDuplicatesEnabled(ExchangeName exchangeName)
    {
        lock (_gate)
        {
            return _states[exchangeName].DuplicatesEnabled;
        }
    }

    public FaultState Pause(ExchangeName exchangeName, TimeSpan duration)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var state = _states[exchangeName];
            state.PausedUntilUtc = now.Add(duration);
            logger.LogWarning("Pause fault enabled for {ExchangeName} during {DurationMs} ms", exchangeName, (long)duration.TotalMilliseconds);

            return CreateSnapshot(exchangeName, state, now);
        }
    }

    public TimeSpan GetPauseRemaining(ExchangeName exchangeName)
    {
        lock (_gate)
        {
            var state = _states[exchangeName];
            if (state.PausedUntilUtc is null)
            {
                return TimeSpan.Zero;
            }

            var remaining = state.PausedUntilUtc.Value - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                state.PausedUntilUtc = null;
                return TimeSpan.Zero;
            }

            return remaining;
        }
    }

    private static FaultState CreateSnapshot(ExchangeName exchangeName, MutableFaultState state, DateTimeOffset now)
    {
        var pausedUntilUtc = state.PausedUntilUtc;
        var remainingPause = pausedUntilUtc is null
            ? TimeSpan.Zero
            : pausedUntilUtc.Value - now;

        if (remainingPause <= TimeSpan.Zero)
        {
            pausedUntilUtc = null;
            remainingPause = TimeSpan.Zero;
        }

        return new FaultState(
            exchangeName.ToString(),
            state.DisconnectOncePending,
            state.DuplicatesEnabled,
            pausedUntilUtc,
            (long)remainingPause.TotalMilliseconds);
    }

    private sealed class MutableFaultState
    {
        public bool DisconnectOncePending { get; set; }

        public bool DuplicatesEnabled { get; set; }

        public DateTimeOffset? PausedUntilUtc { get; set; }
    }
}
