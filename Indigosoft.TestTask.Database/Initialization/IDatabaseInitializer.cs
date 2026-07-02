namespace Indigosoft.TestTask.Database.Initialization;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
