using System.Text.Json.Serialization;

namespace Indigosoft.TestTask.ExchangeSimulator.Models;

public sealed record ExchangeATickDto(
    [property: JsonPropertyName("ticker")] string Ticker,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("volume")] decimal Volume,
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("sequence")] long Sequence);
