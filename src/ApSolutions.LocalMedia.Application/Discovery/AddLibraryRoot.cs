using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record AddLibraryRootCommand(string Path, RootKind Kind, ScanPolicy ScanPolicy);

public sealed class AddLibraryRoot
{
    private readonly ILibraryRootRepository _repository;
    private readonly IPathNormalizer _pathNormalizer;

    public AddLibraryRoot(ILibraryRootRepository repository, IPathNormalizer pathNormalizer)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _pathNormalizer = pathNormalizer ?? throw new ArgumentNullException(nameof(pathNormalizer));
    }

    public async Task<LibraryRoot> ExecuteAsync(
        AddLibraryRootCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalizedPath = _pathNormalizer.NormalizeAndValidate(command.Path, command.Kind);
        var roots = await _repository.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var existing in roots)
        {
            if (string.Equals(existing.Path, normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Duplicate library root: {normalizedPath}");
            }

            if (Contains(existing.Path, normalizedPath) || Contains(normalizedPath, existing.Path))
            {
                throw new InvalidOperationException($"Nested library root: {normalizedPath}");
            }
        }

        var root = new LibraryRoot(
            new LibraryRootId(Guid.NewGuid()),
            normalizedPath,
            command.Kind,
            RootAvailability.Available,
            command.ScanPolicy);
        await _repository.AddAsync(root, cancellationToken).ConfigureAwait(false);
        return root;
    }

    private static bool Contains(string parent, string candidate)
    {
        var parentWithSeparator = parent.EndsWith(Path.DirectorySeparatorChar)
            ? parent
            : parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(parentWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
