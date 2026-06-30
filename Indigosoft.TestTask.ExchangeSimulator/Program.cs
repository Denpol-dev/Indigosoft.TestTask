using System.Globalization;
using System.Text.Json.Serialization;
using Indigosoft.TestTask.ExchangeSimulator.Models;
using Indigosoft.TestTask.ExchangeSimulator.Options;
using Indigosoft.TestTask.ExchangeSimulator.Services;
using Indigosoft.TestTask.ExchangeSimulator.WebSockets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ExchangeSimulatorOptions>(
    builder.Configuration.GetSection(ExchangeSimulatorOptions.SectionName));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<PriceStateService>();
builder.Services.AddSingleton<ExchangeFaultStateService>();
builder.Services.AddSingleton<IExchangeTickGenerator, ExchangeATickGenerator>();
builder.Services.AddSingleton<IExchangeTickGenerator, ExchangeBTickGenerator>();
builder.Services.AddSingleton<IExchangeTickGenerator, ExchangeCTickGenerator>();
builder.Services.AddSingleton<ExchangeWebSocketHandler>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.MapExchangeWebSocketEndpoints();

app.MapGet("/faults", (ExchangeFaultStateService faultStateService) =>
    Results.Ok(faultStateService.GetAll()));

app.MapPost("/faults/{exchangeName}/disconnect-once", (
    string exchangeName,
    ExchangeFaultStateService faultStateService) =>
{
    if (!ExchangeNameExtensions.TryParse(exchangeName, out var parsedExchangeName))
    {
        return UnknownExchange(exchangeName);
    }

    return Results.Ok(faultStateService.RequestDisconnectOnce(parsedExchangeName));
});

app.MapPost("/faults/{exchangeName}/duplicates/enable", (
    string exchangeName,
    ExchangeFaultStateService faultStateService) =>
{
    if (!ExchangeNameExtensions.TryParse(exchangeName, out var parsedExchangeName))
    {
        return UnknownExchange(exchangeName);
    }

    return Results.Ok(faultStateService.SetDuplicatesEnabled(parsedExchangeName, true));
});

app.MapPost("/faults/{exchangeName}/duplicates/disable", (
    string exchangeName,
    ExchangeFaultStateService faultStateService) =>
{
    if (!ExchangeNameExtensions.TryParse(exchangeName, out var parsedExchangeName))
    {
        return UnknownExchange(exchangeName);
    }

    return Results.Ok(faultStateService.SetDuplicatesEnabled(parsedExchangeName, false));
});

app.MapPost("/faults/{exchangeName}/pause", (
    string exchangeName,
    HttpRequest request,
    ExchangeFaultStateService faultStateService) =>
{
    if (!ExchangeNameExtensions.TryParse(exchangeName, out var parsedExchangeName))
    {
        return UnknownExchange(exchangeName);
    }

    var durationMs = 5000;
    if (request.Query.TryGetValue("durationMs", out var durationValues)
        && int.TryParse(durationValues.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDurationMs))
    {
        durationMs = parsedDurationMs;
    }

    if (durationMs <= 0)
    {
        return Results.BadRequest(new { error = "durationMs must be a positive integer." });
    }

    return Results.Ok(faultStateService.Pause(parsedExchangeName, TimeSpan.FromMilliseconds(durationMs)));
});

app.Run();

static IResult UnknownExchange(string exchangeName)
{
    return Results.NotFound(new
    {
        error = $"Unknown exchange '{exchangeName}'.",
        supportedExchanges = ExchangeNameExtensions.AllRouteNames
    });
}
