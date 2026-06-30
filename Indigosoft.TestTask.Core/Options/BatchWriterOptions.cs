namespace Indigosoft.TestTask.Core.Options;

public sealed class BatchWriterOptions
{
    public int BatchSize { get; init; } = 500;

    public int FlushIntervalMs { get; init; } = 1_000;

    public int MaxWriteRetries { get; init; } = 3;

    public int InitialRetryDelayMs { get; init; } = 500;

    public int MaxRetryDelayMs { get; init; } = 5_000;

    public int DrainTimeoutSeconds { get; init; } = 15;
}
