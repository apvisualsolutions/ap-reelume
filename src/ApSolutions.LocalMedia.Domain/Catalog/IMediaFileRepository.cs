using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Domain.Catalog;

public interface IMediaFileRepository
{
    Task<MediaFile?> FindByPathAsync(
        LibraryRootId rootId,
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The file behind one identifier, or nothing when the catalogue no longer knows it. Playback
    /// needs this: a surface holds an identifier and the engine needs the path it stands for.
    /// </summary>
    Task<MediaFile?> FindByIdAsync(
        MediaFileId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, MediaFile>> FindByPathsAsync(
        LibraryRootId rootId,
        IReadOnlyCollection<string> paths,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(MediaFile mediaFile, CancellationToken cancellationToken = default);

    Task UpsertBatchAsync(
        IReadOnlyCollection<MediaFile> mediaFiles,
        CancellationToken cancellationToken = default);

    Task<IdentifiedMediaFile?> FindByStableIdentityAsync(
        FileIdentity identity,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IdentifiedMediaFile>> FindByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default);

    Task SaveIdentityAsync(
        MediaFileId mediaFileId,
        FileIdentity identity,
        CancellationToken cancellationToken = default);

    /// <summary>The stored identity of one file, or nothing when no scan has captured it yet.</summary>
    Task<FileIdentity?> GetIdentityAsync(
        MediaFileId mediaFileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets one file entirely: its row, its identity, its candidates, and its catalog
    /// projection. Reconciliation absorbs the scan-created duplicate of a moved file with this;
    /// nothing on disk is touched.
    /// </summary>
    Task RemoveAsync(MediaFileId mediaFileId, CancellationToken cancellationToken = default);

    Task ReassignAsync(
        MediaFileId mediaFileId,
        LibraryRootId libraryRootId,
        string newPath,
        FileIdentity identity,
        CancellationToken cancellationToken = default);

    Task SetRootAvailabilityAsync(
        LibraryRootId libraryRootId,
        bool isAvailable,
        CancellationToken cancellationToken = default);

    Task<string?> GetScanCheckpointAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default);

    Task SaveScanCheckpointAsync(
        LibraryRootId rootId,
        string resumeAfterPath,
        CancellationToken cancellationToken = default);

    Task ClearScanCheckpointAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default);
}

public sealed record IdentifiedMediaFile(MediaFile MediaFile, FileIdentity Identity);
