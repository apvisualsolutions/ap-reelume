// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

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

    /// <summary>
    /// Removes a folder and the catalogue that belonged to it: its files, and the titles that are
    /// left without any file to reach. It never touches a video on disk.
    ///
    /// <para>It returns the titles that left, because their covers live on disk and a repository
    /// that deleted files would be deciding and writing at once. The caller hands those ids to the
    /// artwork store after the transaction commits — a deleted file does not roll back.</para>
    ///
    /// <para>There is no flag to keep the catalogue. There was one until 2026-09-06, defaulted to
    /// true, threaded through a command, this interface, the adapter and sixteen test doubles, and
    /// read by nobody. Removing a folder is one thing now, and this is what it is.</para>
    /// </summary>
    Task<IReadOnlyList<TitleId>> RemoveAsync(
        LibraryRootId id,
        CancellationToken cancellationToken = default);
}
