using Indigosoft.TestTask.Database.Initialization;
using Indigosoft.TestTask.Database.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Database;

public sealed class PostgresDatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_WithEmptyConnectionString_ThrowsInvalidOperationException()
    {
        var initializer = new PostgresDatabaseInitializer(
            Options.Create(new PostgresOptions()),
            NullLogger<PostgresDatabaseInitializer>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await initializer.InitializeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InitializeAsync_WithConnectionStringWithoutDatabase_ThrowsInvalidOperationException()
    {
        var initializer = new PostgresDatabaseInitializer(
            Options.Create(new PostgresOptions
            {
                ConnectionString = "Host=localhost;Username=postgres;Password=postgres"
            }),
            NullLogger<PostgresDatabaseInitializer>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await initializer.InitializeAsync(CancellationToken.None));
    }
}
