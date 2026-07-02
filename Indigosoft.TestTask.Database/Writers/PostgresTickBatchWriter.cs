using System.Text;
using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Core.Models;
using Indigosoft.TestTask.Database.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Indigosoft.TestTask.Database.Writers;

public sealed class PostgresTickBatchWriter(
    IOptions<PostgresOptions> options,
    ILogger<PostgresTickBatchWriter> logger) : ITickBatchWriter
{
    private readonly IOptions<PostgresOptions> _options =
    options ?? throw new ArgumentNullException(nameof(options));

    private readonly ILogger<PostgresTickBatchWriter> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task WriteBatchAsync(
        IReadOnlyCollection<NormalizedTick> ticks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticks);

        if (ticks.Count == 0)
        {
            return;
        }

        var connectionString = _options.Value.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Postgres connection string is not configured.");
        }

        ValidateTicks(ticks);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var committed = false;

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = BuildInsertCommand(command, ticks);

            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;

            _logger.LogDebug("Inserted {TickCount} raw ticks.", ticks.Count);
        }
        catch
        {
            if (!committed)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
    }

    private static void ValidateTicks(IReadOnlyCollection<NormalizedTick> ticks)
    {
        foreach (var tick in ticks)
        {
            if (tick is null)
            {
                throw new ArgumentException("Batch contains a null tick.", nameof(ticks));
            }

            if (string.IsNullOrWhiteSpace(tick.Source))
            {
                throw new ArgumentException("Tick source must not be empty.", nameof(ticks));
            }

            if (string.IsNullOrWhiteSpace(tick.Ticker))
            {
                throw new ArgumentException("Tick ticker must not be empty.", nameof(ticks));
            }

            if (string.IsNullOrWhiteSpace(tick.RawJson))
            {
                throw new ArgumentException("Tick raw JSON must not be empty.", nameof(ticks));
            }
        }
    }

    private static string BuildInsertCommand(
        NpgsqlCommand command,
        IReadOnlyCollection<NormalizedTick> ticks)
    {
        var sql = new StringBuilder("""
            INSERT INTO raw_ticks (
                source,
                ticker,
                price,
                volume,
                tick_timestamp,
                sequence,
                raw_json
            )
            VALUES
            """);

        var index = 0;
        foreach (var tick in ticks)
        {
            if (index > 0)
            {
                sql.AppendLine(",");
            }

            sql.Append("    (");
            sql.Append($"@source{index}, @ticker{index}, @price{index}, @volume{index}, @timestamp{index}, @sequence{index}, @rawJson{index}");
            sql.Append(')');

            command.Parameters.AddWithValue($"source{index}", tick.Source);
            command.Parameters.AddWithValue($"ticker{index}", tick.Ticker);
            command.Parameters.AddWithValue($"price{index}", tick.Price);
            command.Parameters.AddWithValue($"volume{index}", tick.Volume);
            command.Parameters.AddWithValue($"timestamp{index}", tick.Timestamp);
            command.Parameters.AddWithValue(
                $"sequence{index}",
                NpgsqlDbType.Bigint,
                tick.Sequence.HasValue ? (object)tick.Sequence.Value : DBNull.Value);
            command.Parameters.AddWithValue($"rawJson{index}", NpgsqlDbType.Jsonb, tick.RawJson);

            index++;
        }

        sql.Append(';');
        return sql.ToString();
    }
}
