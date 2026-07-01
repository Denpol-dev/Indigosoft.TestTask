using System.Text.Json;
using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Core.Models;

namespace Indigosoft.TestTask.Aggregator.Parsing;

public sealed class ExchangeCMessageParser : IExchangeMessageParser
{
    public bool CanParse(string source)
    {
        return string.Equals(source, ExchangeSource.ExchangeC, StringComparison.OrdinalIgnoreCase);
    }

    public bool TryParse(string source, string rawJson, out NormalizedTick? tick)
    {
        tick = null;

        if (!CanParse(source) || string.IsNullOrWhiteSpace(rawJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;

            if (!TryGetString(root, "instrument", out var instrument)
                || !root.TryGetProperty("last", out var last)
                || !TryGetDecimal(last, "amount", out var price)
                || !TryGetDecimal(root, "size", out var volume)
                || !root.TryGetProperty("time", out var time)
                || !TryGetLong(time, "unixSeconds", out var unixSeconds)
                || !TryGetInt(time, "nanoseconds", out var nanoseconds)
                || nanoseconds < 0
                || nanoseconds >= 1_000_000_000)
            {
                return false;
            }

            TryGetString(root, "eventId", out var eventId);

            tick = new NormalizedTick(
                ExchangeSource.ExchangeC,
                NormalizeTicker(instrument),
                price,
                volume,
                DateTimeOffset.FromUnixTimeSeconds(unixSeconds).AddTicks(nanoseconds / 100),
                TryExtractSequence(eventId),
                rawJson);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;

        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetDecimal(JsonElement root, string propertyName, out decimal value)
    {
        value = default;

        return root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetDecimal(out value);
    }

    private static bool TryGetLong(JsonElement root, string propertyName, out long value)
    {
        value = default;

        return root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out value);
    }

    private static bool TryGetInt(JsonElement root, string propertyName, out int value)
    {
        value = default;

        return root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static long? TryExtractSequence(string eventId)
    {
        var end = eventId.Length - 1;

        while (end >= 0 && !char.IsDigit(eventId[end]))
        {
            end--;
        }

        if (end < 0)
        {
            return null;
        }

        var start = end;
        while (start >= 0 && char.IsDigit(eventId[start]))
        {
            start--;
        }

        var digits = eventId.Substring(start + 1, end - start);
        return long.TryParse(digits, out var sequence)
            ? sequence
            : null;
    }

    private static string NormalizeTicker(string ticker)
    {
        return ticker
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }
}
