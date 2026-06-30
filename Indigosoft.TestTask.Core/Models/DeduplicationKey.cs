namespace Indigosoft.TestTask.Core.Models;

public sealed record DeduplicationKey(
    string Source,
    string Ticker,
    decimal Price,
    decimal Volume,
    DateTimeOffset Timestamp)
{
    public static DeduplicationKey FromTick(NormalizedTick tick)
    {
        return new DeduplicationKey(
            tick.Source,
            tick.Ticker,
            tick.Price,
            tick.Volume,
            tick.Timestamp);
    }
}
