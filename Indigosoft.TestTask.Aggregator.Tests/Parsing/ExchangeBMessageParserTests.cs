using Indigosoft.TestTask.Aggregator.Parsing;
using Indigosoft.TestTask.Core.Models;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Parsing;

public sealed class ExchangeBMessageParserTests
{
    private readonly ExchangeBMessageParser _parser = new();

    [Fact]
    public void TryParse_WithValidMessage_ReturnsNormalizedTick()
    {
        var rawJson = """
            {
              "s": "ETH-USDT",
              "p": "3500.55",
              "q": "2.15",
              "ts": 1782812345678,
              "seq": 456
            }
            """;

        var result = _parser.TryParse(ExchangeSource.ExchangeB, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal(ExchangeSource.ExchangeB, tick.Source);
        Assert.Equal("ETHUSDT", tick.Ticker);
        Assert.Equal(3500.55m, tick.Price);
        Assert.Equal(2.15m, tick.Volume);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1782812345678), tick.Timestamp);
        Assert.Equal(456, tick.Sequence);
        Assert.Equal(rawJson, tick.RawJson);
    }

    [Fact]
    public void TryParse_NormalizesDashedTicker()
    {
        var rawJson = """{"s":"ETH-USDT","p":"3500.55","q":"2.15","ts":1782812345678,"seq":456}""";

        var result = _parser.TryParse(ExchangeSource.ExchangeB, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal("ETHUSDT", tick.Ticker);
    }

    [Fact]
    public void TryParse_ParsesPriceAndVolumeFromStrings()
    {
        var rawJson = """{"s":"ETH-USDT","p":"3500.55","q":"2.15","ts":1782812345678,"seq":456}""";

        var result = _parser.TryParse(ExchangeSource.ExchangeB, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal(3500.55m, tick.Price);
        Assert.Equal(2.15m, tick.Volume);
    }

    [Fact]
    public void TryParse_ParsesUnixMillisecondsTimestamp()
    {
        var rawJson = """{"s":"ETH-USDT","p":"3500.55","q":"2.15","ts":1782812345678,"seq":456}""";

        var result = _parser.TryParse(ExchangeSource.ExchangeB, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1782812345678), tick.Timestamp);
    }

    [Fact]
    public void TryParse_WithInvalidDecimal_ReturnsFalse()
    {
        var rawJson = """{"s":"ETH-USDT","p":"not-a-decimal","q":"2.15","ts":1782812345678,"seq":456}""";

        var result = _parser.TryParse(ExchangeSource.ExchangeB, rawJson, out var tick);

        Assert.False(result);
        Assert.Null(tick);
    }
}
