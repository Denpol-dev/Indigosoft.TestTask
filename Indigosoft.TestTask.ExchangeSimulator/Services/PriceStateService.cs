using Indigosoft.TestTask.ExchangeSimulator.Models;

namespace Indigosoft.TestTask.ExchangeSimulator.Services;

public sealed class PriceStateService
{
    private static readonly IReadOnlyDictionary<string, decimal> BasePrices = new Dictionary<string, decimal>
    {
        ["BTCUSDT"] = 65_000m,
        ["ETHUSDT"] = 3_500m,
        ["SOLUSDT"] = 150m,
        ["XRPUSDT"] = 0.55m,
        ["DOGEUSDT"] = 0.12m
    };

    private static readonly string[] Tickers = BasePrices.Keys.ToArray();

    private readonly object _gate = new();
    private readonly Dictionary<(ExchangeName ExchangeName, string Ticker), decimal> _prices = [];

    public string GetRandomTicker()
    {
        return Tickers[Random.Shared.Next(Tickers.Length)];
    }

    public decimal GetNextPrice(ExchangeName exchangeName, string ticker)
    {
        lock (_gate)
        {
            var key = (exchangeName, ticker);
            var basePrice = BasePrices[ticker];
            var currentPrice = _prices.TryGetValue(key, out var existingPrice)
                ? existingPrice
                : basePrice;

            var randomChangePercent = ((decimal)Random.Shared.NextDouble() - 0.5m) * 0.004m;
            var meanReversion = (basePrice - currentPrice) * 0.02m;
            var nextPrice = currentPrice + currentPrice * randomChangePercent + meanReversion;

            if (nextPrice <= 0)
            {
                nextPrice = basePrice * 0.5m;
            }

            _prices[key] = nextPrice;

            return nextPrice;
        }
    }
}
