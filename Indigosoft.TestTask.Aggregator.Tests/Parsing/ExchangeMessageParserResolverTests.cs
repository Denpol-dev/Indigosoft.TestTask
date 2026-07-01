using Indigosoft.TestTask.Aggregator.Parsing;
using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Core.Models;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Parsing;

public sealed class ExchangeMessageParserResolverTests
{
    [Fact]
    public void TryParse_SelectsExchangeAParser()
    {
        var resolver = CreateResolver();
        var rawJson = """{"ticker":"BTCUSDT","price":65000.12,"volume":0.42,"timestamp":"2026-06-30T12:00:00.000Z","sequence":123}""";

        var result = resolver.TryParse(ExchangeSource.ExchangeA, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal(ExchangeSource.ExchangeA, tick.Source);
    }

    [Fact]
    public void TryParse_SelectsExchangeBParser()
    {
        var resolver = CreateResolver();
        var rawJson = """{"s":"ETH-USDT","p":"3500.55","q":"2.15","ts":1782812345678,"seq":456}""";

        var result = resolver.TryParse(ExchangeSource.ExchangeB, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal(ExchangeSource.ExchangeB, tick.Source);
    }

    [Fact]
    public void TryParse_SelectsExchangeCParser()
    {
        var resolver = CreateResolver();
        var rawJson = """{"instrument":"SOL/USDT","last":{"amount":123.45,"currency":"USDT"},"size":10,"time":{"unixSeconds":1782812345,"nanoseconds":123000000},"eventId":"exchange-c-789"}""";

        var result = resolver.TryParse(ExchangeSource.ExchangeC, rawJson, out var tick);

        Assert.True(result);
        Assert.NotNull(tick);
        Assert.Equal(ExchangeSource.ExchangeC, tick.Source);
    }

    [Fact]
    public void TryParse_WithUnknownSource_ReturnsFalse()
    {
        var resolver = CreateResolver();

        var result = resolver.TryParse("UnknownExchange", "{}", out var tick);

        Assert.False(result);
        Assert.Null(tick);
    }

    private static ExchangeMessageParserResolver CreateResolver()
    {
        IExchangeMessageParser[] parsers =
        [
            new ExchangeAMessageParser(),
            new ExchangeBMessageParser(),
            new ExchangeCMessageParser()
        ];

        return new ExchangeMessageParserResolver(parsers);
    }
}
