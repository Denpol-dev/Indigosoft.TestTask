using System.Text.Json.Serialization;

namespace Indigosoft.TestTask.ExchangeSimulator.Models;

public sealed record ExchangeCTickDto(
    [property: JsonPropertyName("instrument")] string Instrument,
    [property: JsonPropertyName("last")] ExchangeCLastDto Last,
    [property: JsonPropertyName("size")] int Size,
    [property: JsonPropertyName("time")] ExchangeCTimeDto Time,
    [property: JsonPropertyName("eventId")] string EventId);

public sealed record ExchangeCLastDto(
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string Currency);

public sealed record ExchangeCTimeDto(
    [property: JsonPropertyName("unixSeconds")] long UnixSeconds,
    [property: JsonPropertyName("nanoseconds")] int Nanoseconds);
