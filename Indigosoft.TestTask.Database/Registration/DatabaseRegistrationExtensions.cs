using Indigosoft.TestTask.Core.Interfaces;
using Indigosoft.TestTask.Database.Initialization;
using Indigosoft.TestTask.Database.Options;
using Indigosoft.TestTask.Database.Writers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Indigosoft.TestTask.Database.Registration;

public static class DatabaseRegistrationExtensions
{
    public static IServiceCollection AddPostgresDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<PostgresOptions>(
            configuration.GetSection("Postgres"));

        services.AddSingleton<IDatabaseInitializer, PostgresDatabaseInitializer>();
        services.AddSingleton<ITickBatchWriter, PostgresTickBatchWriter>();

        return services;
    }
}
