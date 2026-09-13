// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record RemoveLibraryRootCommand(LibraryRootId LibraryRootId);

public sealed class RemoveLibraryRoot
{
    private readonly ILibraryRootRepository _repository;
    private readonly IArtworkStore? _artwork;

    /// <summary>
    /// The artwork store is optional because the covers are the one thing here that lives outside the
    /// database: without it the catalogue still leaves, and the folders are orphaned rather than the
    /// removal being refused.
    /// </summary>
    public RemoveLibraryRoot(ILibraryRootRepository repository, IArtworkStore? artwork = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _artwork = artwork;
    }

    public async Task ExecuteAsync(
        RemoveLibraryRootCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var leaving = await _repository
            .RemoveAsync(command.LibraryRootId, cancellationToken)
            .ConfigureAwait(false);

        if (_artwork is null)
        {
            return;
        }

        // After the transaction, never inside it: a deleted file does not roll back.
        foreach (var title in leaving)
        {
            await _artwork.RemoveTitleAsync(title, cancellationToken).ConfigureAwait(false);
        }
    }
}
