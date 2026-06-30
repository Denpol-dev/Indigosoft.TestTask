namespace Indigosoft.TestTask.Core.Models;

public static class ExchangeSource
{
    public const string ExchangeA = "ExchangeA";
    public const string ExchangeB = "ExchangeB";
    public const string ExchangeC = "ExchangeC";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ExchangeA,
        ExchangeB,
        ExchangeC
    };

    public static bool IsKnown(string source)
    {
        return All.Contains(source);
    }
}
