using System.Globalization;
using System.Text.Json;
using Indigosoft.TestTask.ExchangeSimulator.Models;

namespace Indigosoft.TestTask.ExchangeSimulator.Services;

public sealed class ExchangeBTickGenerator(PriceStateService priceStateService) : IExchangeTickGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private long _sequence;

    public ExchangeName ExchangeName => ExchangeName.ExchangeB;

    public string GenerateTick()
    {
        var ticker = priceStateService.GetRandomTicker();
        var price = Math.Round(priceStateService.GetNextPrice(ExchangeName, ticker), 2);
        var quantity = Math.Round(0.01m + (decimal)Random.Shared.NextDouble() * 8m, 4);
        var sequence = Interlocked.Increment(ref _sequence);

        var tick = new ExchangeBTickDto(
            ToSymbol(ticker),
            price.ToString("0.00", CultureInfo.InvariantCulture),
            quantity.ToString("0.####", CultureInfo.InvariantCulture),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            sequence);

        return JsonSerializer.Serialize(tick, JsonOptions);
    }

    private static string ToSymbol(string ticker)
    {
        return ticker.EndsWith("USDT", StringComparison.Ordinal)
            ? $"{ticker[..^4]}-USDT"
            : ticker;
    }
}
