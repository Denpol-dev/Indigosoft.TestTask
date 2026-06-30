using Indigosoft.TestTask.Core.Models;

namespace Indigosoft.TestTask.Core.Interfaces;

public interface ITickBatchWriter
{
    Task WriteBatchAsync(
        IReadOnlyCollection<NormalizedTick> ticks,
        CancellationToken cancellationToken);
}
