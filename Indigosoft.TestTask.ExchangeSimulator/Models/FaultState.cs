namespace Indigosoft.TestTask.ExchangeSimulator.Models;

public sealed record FaultState(
    string ExchangeName,
    bool DisconnectOncePending,
    bool DuplicatesEnabled,
    DateTimeOffset? PausedUntilUtc,
    long RemainingPauseMilliseconds);
