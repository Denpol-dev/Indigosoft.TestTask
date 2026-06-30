using Indigosoft.TestTask.Core.Models;

namespace Indigosoft.TestTask.Core.Interfaces;

public interface ITickDeduplicator
{
    bool TryMarkAsProcessed(NormalizedTick tick);
}
