using Indigosoft.TestTask.Aggregator.Parsing;
using Indigosoft.TestTask.Core.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IExchangeMessageParser, ExchangeAMessageParser>();
builder.Services.AddSingleton<IExchangeMessageParser, ExchangeBMessageParser>();
builder.Services.AddSingleton<IExchangeMessageParser, ExchangeCMessageParser>();
builder.Services.AddSingleton<ExchangeMessageParserResolver>();

var app = builder.Build();

app.MapGet("/health", () => "Aggregator is running");

app.Run();
