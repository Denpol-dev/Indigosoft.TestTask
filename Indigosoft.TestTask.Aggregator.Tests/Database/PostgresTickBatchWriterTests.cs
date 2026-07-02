using Indigosoft.TestTask.Core.Models;
using Indigosoft.TestTask.Database.Options;
using Indigosoft.TestTask.Database.Writers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Indigosoft.TestTask.Aggregator.Tests.Database;

public sealed class PostgresTickBatchWriterTests
{
    [Fact]
    public async Task WriteBatchAsync_WithNullTicks_ThrowsArgumentNullException()
    {
        var writer = CreateWriter();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await writer.WriteBatchAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_ReturnsWithoutOpeningConnection()
    {
        var writer = CreateWriter(connectionString: string.Empty);

        await writer.WriteBatchAsync(Array.Empty<NormalizedTick>(), CancellationToken.None);
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyConnectionStringAndNonEmptyBatch_ThrowsInvalidOperationException()
    {
        var writer = CreateWriter(connectionString: string.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await writer.WriteBatchAsync([CreateTick()], CancellationToken.None));
    }

    [Fact]
    public async Task WriteBatchAsync_WithNullTick_ThrowsArgumentException()
    {
        var writer = CreateWriter();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await writer.WriteBatchAsync([null!], CancellationToken.None));
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptySource_ThrowsArgumentException()
    {
        var writer = CreateWriter();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await writer.WriteBatchAsync([CreateTick(source: "")], CancellationToken.None));
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyTicker_ThrowsArgumentException()
    {
        var writer = CreateWriter();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await writer.WriteBatchAsync([CreateTick(ticker: "")], CancellationToken.None));
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyRawJson_ThrowsArgumentException()
    {
        var writer = CreateWriter();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await writer.WriteBatchAsync([CreateTick(rawJson: "")], CancellationToken.None));
    }

    private static PostgresTickBatchWriter CreateWriter(
        string connectionString = "Host=localhost;Database=test;Username=test;Password=test")
    {
        return new PostgresTickBatchWriter(
            Options.Create(new PostgresOptions
            {
                ConnectionString = connectionString
            }),
            NullLogger<PostgresTickBatchWriter>.Instance);
    }

    private static NormalizedTick CreateTick(
        string source = ExchangeSource.ExchangeA,
        string ticker = "BTCUSDT",
        string rawJson = "{}")
    {
        return new NormalizedTick(
            source,
            ticker,
            65000.12m,
            0.42m,
            DateTimeOffset.Parse("2026-06-30T12:00:00.000Z"),
            123,
            rawJson);
    }
}
