namespace Indigosoft.TestTask.Database.Options;

public sealed class PostgresOptions
{
    public string ConnectionString { get; init; } = string.Empty;

    public string MaintenanceDatabase { get; init; } = "postgres";
}
