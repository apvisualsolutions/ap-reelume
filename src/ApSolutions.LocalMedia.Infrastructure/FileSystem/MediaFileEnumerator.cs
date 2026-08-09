using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Infrastructure.FileSystem;

public sealed class MediaFileEnumerator : IMediaFileEnumerator
{
    public async IAsyncEnumerable<IReadOnlyList<EnumeratedFile>> EnumerateBatchesAsync(
        LibraryRoot root,
        string? afterPath,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        await Task.Yield();

        var items = Enumerate(root.Path, cancellationToken)
            .Where(item => afterPath is null ||
                           string.Compare(item.Path, afterPath, StringComparison.OrdinalIgnoreCase) > 0)
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        for (var offset = 0; offset < items.Length; offset += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return items.Skip(offset).Take(batchSize).ToArray();
        }
    }

    private static IEnumerable<EnumeratedFile> Enumerate(
        string rootPath,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);
        while (pending.TryPop(out var directoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] entries = [];
            string? directoryError = null;
            try
            {
                entries = Directory.GetFileSystemEntries(directoryPath);
            }
            catch (UnauthorizedAccessException)
            {
                directoryError = "AccessDenied";
            }
            catch (IOException)
            {
                directoryError = "IoError";
            }

            if (directoryError is not null)
            {
                yield return new EnumeratedFile(directoryPath, 0, DateTimeOffset.UnixEpoch, directoryError);
                continue;
            }

            foreach (var entry in entries.OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileAttributes attributes = default;
                string? entryError = null;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (UnauthorizedAccessException)
                {
                    entryError = "AccessDenied";
                }
                catch (IOException)
                {
                    entryError = "IoError";
                }

                if (entryError is not null)
                {
                    yield return new EnumeratedFile(entry, 0, DateTimeOffset.UnixEpoch, entryError);
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if ((attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Push(entry);
                    }

                    continue;
                }

                if (!MediaFileExtensions.IsApproved(Path.GetExtension(entry)))
                {
                    continue;
                }

                EnumeratedFile? item = null;
                try
                {
                    var file = new FileInfo(entry);
                    item = new EnumeratedFile(
                        file.FullName,
                        file.Length,
                        new DateTimeOffset(file.LastWriteTimeUtc));
                }
                catch (UnauthorizedAccessException)
                {
                    item = new EnumeratedFile(entry, 0, DateTimeOffset.UnixEpoch, "AccessDenied");
                }
                catch (IOException)
                {
                    item = new EnumeratedFile(entry, 0, DateTimeOffset.UnixEpoch, "IoError");
                }

                yield return item;
            }
        }
    }
}
