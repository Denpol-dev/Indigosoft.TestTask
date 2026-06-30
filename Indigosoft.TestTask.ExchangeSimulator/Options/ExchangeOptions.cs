namespace Indigosoft.TestTask.ExchangeSimulator.Options;

public sealed class ExchangeOptions
{
    public int MessagesPerSecond { get; set; } = 150;

    public int SendIntervalMs { get; set; } = 100;
}
