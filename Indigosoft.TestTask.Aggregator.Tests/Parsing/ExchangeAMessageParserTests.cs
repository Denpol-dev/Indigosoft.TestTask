using System.Globalization;
using Indigosoft.TestTask.Aggregator.Parsing;
using Indigosoft.TestTask.Core.Models;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Parsing;

public sealed class ExchangeAMessageParserTests
{
    private readonly ExchangeAMessageParser _parser = new();

    [Fact]
    public void TryParse_WithValidMessage_ReturnsNormalizedTick()
    {
        var rawJson = """
            {
              "ticker": "BTCUSDT",
              "price": 65000.12,
              "volume": 0.42,
              "timestamp": "2026-06-30T12:00:00.000Z",
              "sequence": 123
            }
            """;

        var result = _parser.TryParse(ExchangeSource.ExchangeA, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal(ExchangeSource.ExchangeA, tick.Source);
        Assert.Equal("BTCUSDT", tick.Ticker);
        Assert.Equal(65000.12m, tick.Price);
        Assert.Equal(0.42m, tick.Volume);
        Assert.Equal(DateTimeOffset.Parse("2026-06-30T12:00:00.000Z", CultureInfo.InvariantCulture), tick.Timestamp);
        Assert.Equal(123, tick.Sequence);
    }

    [Fact]
    public void TryParse_PreservesRawJson()
    {
        var rawJson = """{"ticker":"BTCUSDT","price":65000.12,"volume":0.42,"timestamp":"2026-06-30T12:00:00.000Z","sequence":123}""";

        var result = _parser.TryParse(ExchangeSource.ExchangeA, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal(rawJson, tick.RawJson);
    }

    [Fact]
    public void CanParse_IsCaseInsensitive()
    {
        Assert.True(_parser.CanParse("exchangea"));
    }

    [Fact]
    public void TryParse_WithInvalidJson_ReturnsFalse()
    {
        var result = _parser.TryParse(ExchangeSource.ExchangeA, "{ invalid json", out var tick);

        Assert.False(result);
        Assert.Null(tick);
    }
}
