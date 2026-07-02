using Indigosoft.TestTask.Aggregator.HostedServices;
using Indigosoft.TestTask.Database.Initialization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Database;

public sealed class DatabaseInitializationHostedServiceTests
{
    [Fact]
    public async Task StartAsync_CallsDatabaseInitializer()
    {
        var initializer = new FakeDatabaseInitializer();
        var service = CreateService(initializer);

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(1, initializer.CallCount);
    }

    [Fact]
    public async Task StartAsync_WhenInitializerThrows_PropagatesException()
    {
        var expectedException = new InvalidOperationException("Database unavailable.");
        var service = CreateService(new FakeDatabaseInitializer(expectedException));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.StartAsync(CancellationToken.None));

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public async Task StopAsync_DoesNotThrow()
    {
        var service = CreateService(new FakeDatabaseInitializer());

        await service.StopAsync(CancellationToken.None);
    }

    private static DatabaseInitializationHostedService CreateService(
        FakeDatabaseInitializer initializer)
    {
        return new DatabaseInitializationHostedService(
            initializer,
            NullLogger<DatabaseInitializationHostedService>.Instance);
    }

    private sealed class FakeDatabaseInitializer(Exception? exception = null) : IDatabaseInitializer
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);

            if (exception is not null)
            {
                throw exception;
            }

            return Task.CompletedTask;
        }
    }
}
