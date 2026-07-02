using Indigosoft.TestTask.Database.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Indigosoft.TestTask.Database.Initialization;

public sealed class PostgresDatabaseInitializer(
    IOptions<PostgresOptions> options,
    ILogger<PostgresDatabaseInitializer> logger) : IDatabaseInitializer
{
    private const string CreateRawTicksTableSql = """
        CREATE TABLE IF NOT EXISTS raw_ticks (
            id BIGSERIAL PRIMARY KEY,
            source TEXT NOT NULL,
            ticker TEXT NOT NULL,
            price NUMERIC(18, 8) NOT NULL,
            volume NUMERIC(18, 8) NOT NULL,
            tick_timestamp TIMESTAMPTZ NOT NULL,
            sequence BIGINT NULL,
            raw_json JSONB NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        """;

    private const string CreateSourceTimestampIndexSql = """
        CREATE INDEX IF NOT EXISTS ix_raw_ticks_source_timestamp
        ON raw_ticks (source, tick_timestamp);
        """;

    private const string CreateTickerTimestampIndexSql = """
        CREATE INDEX IF NOT EXISTS ix_raw_ticks_ticker_timestamp
        ON raw_ticks (ticker, tick_timestamp);
        """;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var connectionString = options.Value.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Postgres connection string is not configured.");
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var targetDatabaseName = connectionStringBuilder.Database;
        if (string.IsNullOrWhiteSpace(targetDatabaseName))
        {
            throw new InvalidOperationException("Postgres connection string must include a target database name.");
        }

        await EnsureDatabaseExistsAsync(connectionStringBuilder, targetDatabaseName, cancellationToken);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, CreateRawTicksTableSql, cancellationToken);
        await ExecuteNonQueryAsync(connection, CreateSourceTimestampIndexSql, cancellationToken);
        await ExecuteNonQueryAsync(connection, CreateTickerTimestampIndexSql, cancellationToken);

        logger.LogInformation("PostgreSQL database schema initialized.");
    }

    private async Task EnsureDatabaseExistsAsync(
        NpgsqlConnectionStringBuilder targetConnectionStringBuilder,
        string targetDatabaseName,
        CancellationToken cancellationToken)
    {
        var maintenanceDatabase = string.IsNullOrWhiteSpace(options.Value.MaintenanceDatabase)
            ? "postgres"
            : options.Value.MaintenanceDatabase;

        var maintenanceConnectionStringBuilder = new NpgsqlConnectionStringBuilder(targetConnectionStringBuilder.ConnectionString)
        {
            Database = maintenanceDatabase
        };

        logger.LogInformation(
            "Checking whether PostgreSQL database {DatabaseName} exists.",
            targetDatabaseName);

        await using var connection = new NpgsqlConnection(maintenanceConnectionStringBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(
                         """
                         SELECT 1
                         FROM pg_database
                         WHERE datname = @databaseName;
                         """,
                         connection))
        {
            command.Parameters.AddWithValue("databaseName", targetDatabaseName);
            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (result is not null)
            {
                logger.LogInformation(
                    "PostgreSQL database {DatabaseName} already exists.",
                    targetDatabaseName);
                return;
            }
        }

        logger.LogInformation(
            "Creating PostgreSQL database {DatabaseName}.",
            targetDatabaseName);

        var quotedDatabaseName = QuoteIdentifier(targetDatabaseName);
        await ExecuteNonQueryAsync(connection, $"CREATE DATABASE {quotedDatabaseName};", cancellationToken);

        logger.LogInformation(
            "PostgreSQL database {DatabaseName} created.",
            targetDatabaseName);
    }

    private static async Task ExecuteNonQueryAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }
}
