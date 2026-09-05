// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Discovery;

public interface ILibraryRootRepository
{
    Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default);

    Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default);

    Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes what a scan just learned about a root: available, gone, or refused by the operating
    /// system. It takes the three-state value rather than a boolean because the two failures are not
    /// the same sentence to a person, and because <c>IMediaFileRepository.SetRootAvailabilityAsync</c>
    /// — which sounds like this one — writes the files under a root and never the root itself.
    /// </summary>
    Task SetAvailabilityAsync(
        LibraryRootId id,
        RootAvailability availability,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        LibraryRootId id,
        bool preserveCatalog = true,
        CancellationToken cancellationToken = default);
}
