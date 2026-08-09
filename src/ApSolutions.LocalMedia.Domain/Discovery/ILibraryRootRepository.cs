using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Discovery;

public interface ILibraryRootRepository
{
    Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default);

    Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default);

    Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default);

    Task RemoveAsync(
        LibraryRootId id,
        bool preserveCatalog = true,
        CancellationToken cancellationToken = default);
}
