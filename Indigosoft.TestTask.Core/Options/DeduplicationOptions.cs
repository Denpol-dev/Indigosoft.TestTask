namespace Indigosoft.TestTask.Core.Options;

public sealed class DeduplicationOptions
{
    public int WindowSeconds { get; init; } = 60;

    public int CleanupIntervalSeconds { get; init; } = 10;
}
