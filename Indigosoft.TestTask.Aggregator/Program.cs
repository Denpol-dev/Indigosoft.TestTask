using Indigosoft.TestTask.Aggregator.Channels;
using Indigosoft.TestTask.Aggregator.Deduplication;
using Indigosoft.TestTask.Aggregator.Parsing;
using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Core.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TickChannelOptions>(
    builder.Configuration.GetSection("Aggregator:Channel"));
builder.Services.Configure<DeduplicationOptions>(
    builder.Configuration.GetSection("Aggregator:Deduplication"));

builder.Services.AddSingleton<IExchangeMessageParser, ExchangeAMessageParser>();
builder.Services.AddSingleton<IExchangeMessageParser, ExchangeBMessageParser>();
builder.Services.AddSingleton<IExchangeMessageParser, ExchangeCMessageParser>();
builder.Services.AddSingleton<ExchangeMessageParserResolver>();
builder.Services.AddSingleton<ITickDeduplicator, InMemoryTickDeduplicator>();
builder.Services.AddSingleton<TickChannel>();

var app = builder.Build();

app.MapGet("/health", () => "Aggregator is running");

app.Run();
