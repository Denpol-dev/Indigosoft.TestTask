using Indigosoft.TestTask.Database.Initialization;

namespace Indigosoft.TestTask.Aggregator.HostedServices;

public sealed class DatabaseInitializationHostedService : IHostedService
{
    private readonly IDatabaseInitializer _databaseInitializer;
    private readonly ILogger<DatabaseInitializationHostedService> _logger;

    public DatabaseInitializationHostedService(
        IDatabaseInitializer databaseInitializer,
        ILogger<DatabaseInitializationHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(databaseInitializer);
        ArgumentNullException.ThrowIfNull(logger);

        _databaseInitializer = databaseInitializer;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting PostgreSQL database initialization.");
        await _databaseInitializer.InitializeAsync(cancellationToken);
        _logger.LogInformation("PostgreSQL database initialization completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
