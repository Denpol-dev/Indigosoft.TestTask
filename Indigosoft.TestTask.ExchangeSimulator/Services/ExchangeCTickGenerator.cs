using System.Text.Json;
using Indigosoft.TestTask.ExchangeSimulator.Models;

namespace Indigosoft.TestTask.ExchangeSimulator.Services;

public sealed class ExchangeCTickGenerator(PriceStateService priceStateService) : IExchangeTickGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private long _sequence;

    public ExchangeName ExchangeName => ExchangeName.ExchangeC;

    public string GenerateTick()
    {
        var ticker = priceStateService.GetRandomTicker();
        var price = Math.Round(priceStateService.GetNextPrice(ExchangeName, ticker), 4);
        var sequence = Interlocked.Increment(ref _sequence);
        var timestamp = DateTimeOffset.UtcNow;
        var unixSeconds = timestamp.ToUnixTimeSeconds();
        var nanoseconds = (int)((timestamp.ToUnixTimeMilliseconds() - unixSeconds * 1000) * 1_000_000);

        var tick = new ExchangeCTickDto(
            ToInstrument(ticker),
            new ExchangeCLastDto(price, "USDT"),
            Random.Shared.Next(1, 101),
            new ExchangeCTimeDto(unixSeconds, nanoseconds),
            $"exchange-c-{sequence}");

        return JsonSerializer.Serialize(tick, JsonOptions);
    }

    private static string ToInstrument(string ticker)
    {
        return ticker.EndsWith("USDT", StringComparison.Ordinal)
            ? $"{ticker[..^4]}/USDT"
            : ticker;
    }
}
