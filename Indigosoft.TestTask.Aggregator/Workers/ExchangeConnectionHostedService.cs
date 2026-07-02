using Indigosoft.TestTask.Aggregator.Channels;
using Indigosoft.TestTask.Core.Options;
using Microsoft.Extensions.Options;

namespace Indigosoft.TestTask.Aggregator.Workers;

public sealed class ExchangeConnectionHostedService : BackgroundService
{
    private readonly IOptions<AggregatorOptions> _options;
    private readonly IExchangeConnectionWorker _exchangeConnectionWorker;
    private readonly TickChannel _tickChannel;
    private readonly ILogger<ExchangeConnectionHostedService> _logger;

    public ExchangeConnectionHostedService(
        IOptions<AggregatorOptions> options,
        IExchangeConnectionWorker exchangeConnectionWorker,
        TickChannel tickChannel,
        ILogger<ExchangeConnectionHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(exchangeConnectionWorker);
        ArgumentNullException.ThrowIfNull(tickChannel);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _exchangeConnectionWorker = exchangeConnectionWorker;
        _tickChannel = tickChannel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var exchanges = _options.Value.Exchanges.ToArray();

        if (exchanges.Length == 0)
        {
            _logger.LogWarning("No exchange connections are configured.");
            _tickChannel.Complete();
            return;
        }

        _logger.LogInformation("Starting {ExchangeCount} exchange connection workers.", exchanges.Length);

        var workerTasks = exchanges
            .Select(connection => RunWorkerSafelyAsync(connection, stoppingToken))
            .ToArray();

        try
        {
            await Task.WhenAll(workerTasks);
        }
        finally
        {
            _tickChannel.Complete();
            _logger.LogInformation("Tick channel completed after exchange workers stopped.");
        }
    }

    private async Task RunWorkerSafelyAsync(
        ExchangeConnectionOptions connection,
        CancellationToken stoppingToken)
    {
        try
        {
            await _exchangeConnectionWorker.RunAsync(connection, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Exchange connection worker for {ExchangeSource} stopped unexpectedly.",
                connection.Source);
        }
    }
}
