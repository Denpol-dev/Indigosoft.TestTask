namespace Indigosoft.TestTask.Core.Models;

public sealed record NormalizedTick(
    string Source,
    string Ticker,
    decimal Price,
    decimal Volume,
    DateTimeOffset Timestamp,
    long? Sequence,
    string RawJson);
