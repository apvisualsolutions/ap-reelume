using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record EnumeratedFile(
    string Path,
    long SizeBytes,
    DateTimeOffset LastWriteUtc,
    string? ErrorCode = null);

public interface IMediaFileEnumerator
{
    IAsyncEnumerable<IReadOnlyList<EnumeratedFile>> EnumerateBatchesAsync(
        LibraryRoot root,
        string? afterPath,
        int batchSize,
        CancellationToken cancellationToken = default);
}
