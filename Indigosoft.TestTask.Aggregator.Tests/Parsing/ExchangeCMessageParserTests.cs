using Indigosoft.TestTask.Aggregator.Parsing;
using Indigosoft.TestTask.Core.Models;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Parsing;

public sealed class ExchangeCMessageParserTests
{
    private readonly ExchangeCMessageParser _parser = new();

    [Fact]
    public void TryParse_WithValidMessage_ReturnsNormalizedTick()
    {
        var rawJson = """
            {
              "instrument": "SOL/USDT",
              "last": {
                "amount": 123.45,
                "currency": "USDT"
              },
              "size": 10,
              "time": {
                "unixSeconds": 1782812345,
                "nanoseconds": 123000000
              },
              "eventId": "exchange-c-789"
            }
            """;

        var result = _parser.TryParse(ExchangeSource.ExchangeC, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal(ExchangeSource.ExchangeC, tick.Source);
        Assert.Equal("SOLUSDT", tick.Ticker);
        Assert.Equal(123.45m, tick.Price);
        Assert.Equal(10m, tick.Volume);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1782812345).AddTicks(123000000 / 100), tick.Timestamp);
        Assert.Equal(789, tick.Sequence);
        Assert.Equal(rawJson, tick.RawJson);
    }

    [Fact]
    public void TryParse_NormalizesSlashedTicker()
    {
        var rawJson = CreateRawJson("SOL/USDT", "exchange-c-789");

        var result = _parser.TryParse(ExchangeSource.ExchangeC, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal("SOLUSDT", tick.Ticker);
    }

    [Fact]
    public void TryParse_ParsesUnixSecondsAndNanosecondsTimestamp()
    {
        var rawJson = CreateRawJson("SOL/USDT", "exchange-c-789");

        var result = _parser.TryParse(ExchangeSource.ExchangeC, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1782812345).AddTicks(123000000 / 100), tick.Timestamp);
    }

    [Fact]
    public void TryParse_ExtractsSequenceFromEventId()
    {
        var rawJson = CreateRawJson("SOL/USDT", "exchange-c-789");

        var result = _parser.TryParse(ExchangeSource.ExchangeC, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal(789, tick.Sequence);
    }

    [Fact]
    public void TryParse_WithEventIdWithoutNumber_ReturnsNullSequence()
    {
        var rawJson = CreateRawJson("SOL/USDT", "exchange-c-latest");

        var result = _parser.TryParse(ExchangeSource.ExchangeC, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Null(tick.Sequence);
    }

    [Fact]
    public void TryParse_WithInvalidJson_ReturnsFalse()
    {
        var result = _parser.TryParse(ExchangeSource.ExchangeC, "{ invalid json", out var tick);

        Assert.False(result);
        Assert.Null(tick);
    }

    private static string CreateRawJson(string instrument, string eventId)
    {
        return $$"""{"instrument":"{{instrument}}","last":{"amount":123.45,"currency":"USDT"},"size":10,"time":{"unixSeconds":1782812345,"nanoseconds":123000000},"eventId":"{{eventId}}"}""";
    }
}
