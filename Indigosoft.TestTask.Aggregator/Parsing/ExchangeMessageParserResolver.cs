using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Core.Models;

namespace Indigosoft.TestTask.Aggregator.Parsing;

public sealed class ExchangeMessageParserResolver
{
    private readonly IReadOnlyCollection<IExchangeMessageParser> _parsers;

    public ExchangeMessageParserResolver(IEnumerable<IExchangeMessageParser> parsers)
    {
        _parsers = parsers.ToArray();
    }

    public bool TryParse(string source, string rawJson, out NormalizedTick? tick)
    {
        tick = null;

        var parser = _parsers.FirstOrDefault(candidate => candidate.CanParse(source));
        return parser is not null && parser.TryParse(source, rawJson, out tick);
    }
}
