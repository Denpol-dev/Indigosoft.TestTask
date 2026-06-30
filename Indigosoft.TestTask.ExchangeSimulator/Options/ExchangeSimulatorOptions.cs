using Indigosoft.TestTask.ExchangeSimulator.Models;

namespace Indigosoft.TestTask.ExchangeSimulator.Options;

public sealed class ExchangeSimulatorOptions
{
    public const string SectionName = "ExchangeSimulator";

    public Dictionary<string, ExchangeOptions> Exchanges { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [ExchangeName.ExchangeA.ToString()] = new(),
        [ExchangeName.ExchangeB.ToString()] = new(),
        [ExchangeName.ExchangeC.ToString()] = new()
    };

    public FaultOptions Faults { get; set; } = new();

    public ExchangeOptions GetExchangeOptions(ExchangeName exchangeName)
    {
        foreach (var (configuredName, options) in Exchanges)
        {
            if (string.Equals(configuredName, exchangeName.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(configuredName, exchangeName.ToRouteName(), StringComparison.OrdinalIgnoreCase))
            {
                return options;
            }
        }

        return new ExchangeOptions();
    }
}
