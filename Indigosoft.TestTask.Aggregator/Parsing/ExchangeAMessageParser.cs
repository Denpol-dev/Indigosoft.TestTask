using System.Globalization;
using System.Text.Json;
using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Core.Models;

namespace Indigosoft.TestTask.Aggregator.Parsing;

public sealed class ExchangeAMessageParser : IExchangeMessageParser
{
    public bool CanParse(string source)
    {
        return string.Equals(source, ExchangeSource.ExchangeA, StringComparison.OrdinalIgnoreCase);
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

            if (!TryGetString(root, "ticker", out var ticker)
                || !TryGetDecimal(root, "price", out var price)
                || !TryGetDecimal(root, "volume", out var volume)
                || !TryGetString(root, "timestamp", out var timestampText)
                || !DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
                || !TryGetLong(root, "sequence", out var sequence))
            {
                return false;
            }

            tick = new NormalizedTick(
                ExchangeSource.ExchangeA,
                NormalizeTicker(ticker),
                price,
                volume,
                timestamp,
                sequence,
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

    private static string NormalizeTicker(string ticker)
    {
        return ticker
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }
}
