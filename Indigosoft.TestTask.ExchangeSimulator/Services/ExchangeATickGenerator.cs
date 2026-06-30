using System.Globalization;
using System.Text.Json;
using Indigosoft.TestTask.ExchangeSimulator.Models;

namespace Indigosoft.TestTask.ExchangeSimulator.Services;

public sealed class ExchangeATickGenerator(PriceStateService priceStateService) : IExchangeTickGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private long _sequence;

    public ExchangeName ExchangeName => ExchangeName.ExchangeA;

    public string GenerateTick()
    {
        var ticker = priceStateService.GetRandomTicker();
        var price = Math.Round(priceStateService.GetNextPrice(ExchangeName, ticker), 2);
        var volume = Math.Round(0.01m + (decimal)Random.Shared.NextDouble() * 5m, 4);
        var sequence = Interlocked.Increment(ref _sequence);

        var tick = new ExchangeATickDto(
            ticker,
            price,
            volume,
            DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            sequence);

        return JsonSerializer.Serialize(tick, JsonOptions);
    }
}
