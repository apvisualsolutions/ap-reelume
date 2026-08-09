using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record RemoveLibraryRootCommand(LibraryRootId LibraryRootId, bool PreserveCatalog = true);

public sealed class RemoveLibraryRoot
{
    private readonly ILibraryRootRepository _repository;

    public RemoveLibraryRoot(ILibraryRootRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task ExecuteAsync(
        RemoveLibraryRootCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _repository.RemoveAsync(
            command.LibraryRootId,
            command.PreserveCatalog,
            cancellationToken);
    }
}
