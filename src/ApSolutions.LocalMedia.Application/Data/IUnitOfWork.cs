namespace ApSolutions.LocalMedia.Application.Data;

public interface IUnitOfWork
{
    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
