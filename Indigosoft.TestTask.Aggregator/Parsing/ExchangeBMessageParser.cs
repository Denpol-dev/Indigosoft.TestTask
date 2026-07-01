using System.Globalization;
using System.Text.Json;
using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Core.Models;

namespace Indigosoft.TestTask.Aggregator.Parsing;

public sealed class ExchangeBMessageParser : IExchangeMessageParser
{
    public bool CanParse(string source)
    {
        return string.Equals(source, ExchangeSource.ExchangeB, StringComparison.OrdinalIgnoreCase);
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

            if (!TryGetString(root, "s", out var symbol)
                || !TryGetString(root, "p", out var priceText)
                || !decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
                || !TryGetString(root, "q", out var volumeText)
                || !decimal.TryParse(volumeText, NumberStyles.Number, CultureInfo.InvariantCulture, out var volume)
                || !TryGetLong(root, "ts", out var timestampMilliseconds)
                || !TryGetLong(root, "seq", out var sequence))
            {
                return false;
            }

            tick = new NormalizedTick(
                ExchangeSource.ExchangeB,
                NormalizeTicker(symbol),
                price,
                volume,
                DateTimeOffset.FromUnixTimeMilliseconds(timestampMilliseconds),
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
