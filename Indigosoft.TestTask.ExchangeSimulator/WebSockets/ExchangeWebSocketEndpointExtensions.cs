using Indigosoft.TestTask.ExchangeSimulator.Models;

namespace Indigosoft.TestTask.ExchangeSimulator.WebSockets;

public static class ExchangeWebSocketEndpointExtensions
{
    public static IEndpointRouteBuilder MapExchangeWebSocketEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.Map("/ws/exchange-a", context => HandleAsync(context, ExchangeName.ExchangeA));
        endpoints.Map("/ws/exchange-b", context => HandleAsync(context, ExchangeName.ExchangeB));
        endpoints.Map("/ws/exchange-c", context => HandleAsync(context, ExchangeName.ExchangeC));

        return endpoints;
    }

    private static Task HandleAsync(HttpContext context, ExchangeName exchangeName)
    {
        var handler = context.RequestServices.GetRequiredService<ExchangeWebSocketHandler>();
        return handler.HandleAsync(context, exchangeName);
    }
}
