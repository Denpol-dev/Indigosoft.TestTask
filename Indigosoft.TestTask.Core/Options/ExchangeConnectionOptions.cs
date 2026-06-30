namespace Indigosoft.TestTask.Core.Options;

public sealed class ExchangeConnectionOptions
{
    public string Source { get; init; } = string.Empty;

    public string WebSocketUrl { get; init; } = string.Empty;

    public int InitialReconnectDelayMs { get; init; } = 1_000;

    public int MaxReconnectDelayMs { get; init; } = 30_000;

    public int IdleTimeoutSeconds { get; init; } = 10;
}
