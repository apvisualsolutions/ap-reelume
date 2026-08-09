using System.IO.Compression;
using ApSolutions.LocalMedia.Application.Backup;

namespace ApSolutions.LocalMedia.Infrastructure.Backup;

/// <summary>
/// Packs a staged payload into one archive. The allowlist is checked here as well as when the payload
/// is built: this is the file that leaves the machine, so it is the last place that can still refuse to
/// carry something, and it refuses by failing rather than by silently dropping the entry.
/// </summary>
public sealed class ZipExportService : IBackupArchiveWriter
{
    public async Task<IReadOnlyList<string>> WriteAsync(
        string archivePath,
        string sourceDirectory,
        IProgress<BackupProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var files = Directory
            .EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .Select(file => (Source: file, Entry: Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/')))
            .OrderBy(file => file.Entry, StringComparer.Ordinal)
            .ToArray();
        if (files.FirstOrDefault(file => !BackupContentPolicy.IsAllowed(file.Entry)) is { Entry: not null } refused)
        {
            throw new InvalidOperationException(
                $"The staged payload holds an entry a backup may not carry: {refused.Entry}");
        }

        var fullArchivePath = Path.GetFullPath(archivePath);
        var directory = Path.GetDirectoryName(fullArchivePath)
            ?? throw new InvalidOperationException("The archive destination has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $"{Path.GetFileName(fullArchivePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
                var written = 0;
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = archive.CreateEntry(file.Entry, CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await using var source = new FileStream(
                        file.Source,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read);
                    await source.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
                    progress?.Report(new BackupProgress(BackupStage.Archive, ++written, files.Length));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullArchivePath, overwrite: true);
            progress?.Report(new BackupProgress(BackupStage.Archive, files.Length, files.Length));
            return [.. files.Select(file => file.Entry)];
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
