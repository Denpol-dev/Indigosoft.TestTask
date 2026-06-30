using Indigosoft.TestTask.ExchangeSimulator.Models;

namespace Indigosoft.TestTask.ExchangeSimulator.Services;

public interface IExchangeTickGenerator
{
    ExchangeName ExchangeName { get; }

    string GenerateTick();
}
