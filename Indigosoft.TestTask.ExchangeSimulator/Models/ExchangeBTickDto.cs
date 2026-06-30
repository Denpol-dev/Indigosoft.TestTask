using System.Text.Json.Serialization;

namespace Indigosoft.TestTask.ExchangeSimulator.Models;

public sealed record ExchangeBTickDto(
    [property: JsonPropertyName("s")] string Symbol,
    [property: JsonPropertyName("p")] string Price,
    [property: JsonPropertyName("q")] string Quantity,
    [property: JsonPropertyName("ts")] long Timestamp,
    [property: JsonPropertyName("seq")] long Sequence);
