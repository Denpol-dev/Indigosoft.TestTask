using Indigosoft.TestTask.Core.Models;

namespace Indigosoft.TestTask.Core.Interfaces;

public interface IExchangeMessageParser
{
    bool CanParse(string source);

    bool TryParse(
        string source,
        string rawJson,
        out NormalizedTick? tick);
}
