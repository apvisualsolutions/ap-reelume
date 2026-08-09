namespace ApSolutions.LocalMedia.Application.Data;

public interface IMigrationRunner
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
