namespace ApSolutions.LocalMedia.Application.Data;

public sealed record DatabaseIntegrityResult(bool IsValid, string Detail);

public interface IDatabaseIntegrityChecker
{
    Task<DatabaseIntegrityResult> CheckAsync(CancellationToken cancellationToken = default);
}
