namespace Indigosoft.TestTask.ExchangeSimulator.Models;

public enum ExchangeName
{
    ExchangeA,
    ExchangeB,
    ExchangeC
}

public static class ExchangeNameExtensions
{
    public static readonly string[] AllRouteNames =
    [
        ExchangeName.ExchangeA.ToRouteName(),
        ExchangeName.ExchangeB.ToRouteName(),
        ExchangeName.ExchangeC.ToRouteName()
    ];

    public static bool TryParse(string? value, out ExchangeName exchangeName)
    {
        var normalized = Normalize(value);

        switch (normalized)
        {
            case "exchangea":
            case "a":
                exchangeName = ExchangeName.ExchangeA;
                return true;
            case "exchangeb":
            case "b":
                exchangeName = ExchangeName.ExchangeB;
                return true;
            case "exchangec":
            case "c":
                exchangeName = ExchangeName.ExchangeC;
                return true;
            default:
                exchangeName = default;
                return false;
        }
    }

    public static string ToRouteName(this ExchangeName exchangeName)
    {
        return exchangeName switch
        {
            ExchangeName.ExchangeA => "exchange-a",
            ExchangeName.ExchangeB => "exchange-b",
            ExchangeName.ExchangeC => "exchange-c",
            _ => throw new ArgumentOutOfRangeException(nameof(exchangeName), exchangeName, null)
        };
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Trim()
                .ToLowerInvariant();
    }
}
