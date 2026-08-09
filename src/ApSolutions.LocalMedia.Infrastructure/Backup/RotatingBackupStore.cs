using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ApSolutions.LocalMedia.Application.Backup;

namespace ApSolutions.LocalMedia.Infrastructure.Backup;

/// <summary>
/// The folder of rotating copies. A copy only becomes visible once it is complete, and rotation is the
/// only thing that ever deletes: it keeps the five most recent, and on top of that it always keeps the
/// most recent copy that still hashes to its manifest. Five damaged copies must never be able to push
/// out the one that would actually restore.
/// </summary>
public sealed class RotatingBackupStore : IBackupStore
{
    private const string StagingPrefix = ".staging-";
    private readonly string _rootDirectory;
    private readonly Func<string, long> _availableBytes;

    public RotatingBackupStore(string rootDirectory)
        : this(rootDirectory, DefaultAvailableBytes)
    {
    }

    public RotatingBackupStore(string rootDirectory, Func<string, long> availableBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(availableBytes);
        _rootDirectory = Path.GetFullPath(rootDirectory);
        _availableBytes = availableBytes;
    }

    public Task<IReadOnlyList<BackupCopy>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(_rootDirectory))
        {
            return Task.FromResult<IReadOnlyList<BackupCopy>>([]);
        }

        var copies = Directory
            .EnumerateDirectories(_rootDirectory)
            .Where(directory => !Path.GetFileName(directory).StartsWith(StagingPrefix, StringComparison.Ordinal))
            .Select(Describe)
            .OrderByDescending(copy => copy.CreatedUtc)
            .ThenBy(copy => copy.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult<IReadOnlyList<BackupCopy>>(copies);
    }

    public Task<string> CreateStagingAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_rootDirectory);
        var staging = Path.Combine(_rootDirectory, $"{StagingPrefix}{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        return Task.FromResult(staging);
    }

    public void DiscardStaging(string stagingDirectory)
    {
        if (string.IsNullOrWhiteSpace(stagingDirectory))
        {
            return;
        }

        var resolved = Path.GetFullPath(stagingDirectory);
        EnsureInsideRoot(resolved);
        if (Directory.Exists(resolved))
        {
            Directory.Delete(resolved, recursive: true);
        }
    }

    public Task<BackupCopy> PublishAsync(
        string stagingDirectory,
        DateTimeOffset createdUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        var resolved = Path.GetFullPath(stagingDirectory);
        EnsureInsideRoot(resolved);
        if (!Directory.Exists(resolved))
        {
            throw new DirectoryNotFoundException("The staged copy no longer exists.");
        }

        var published = Path.Combine(_rootDirectory, NextName(createdUtc));
        Directory.Move(resolved, published);
        return Task.FromResult(Describe(published));
    }

    public async Task<IReadOnlyList<BackupCopy>> PruneAsync(
        int retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retention);
        var copies = await ListAsync(cancellationToken).ConfigureAwait(false);
        if (copies.Count <= retention)
        {
            return [];
        }

        var kept = new HashSet<string>(
            copies.Take(retention).Select(copy => copy.Path),
            StringComparer.OrdinalIgnoreCase);

        // The newest restorable copy is never a candidate, whatever its position in the order.
        if (copies.FirstOrDefault(copy => copy.IsValid) is { } newestValid)
        {
            kept.Add(newestValid.Path);
        }

        var removed = copies.Where(copy => !kept.Contains(copy.Path)).ToArray();
        foreach (var copy in removed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Delete(copy.Path, recursive: true);
        }

        return removed;
    }

    public void EnsureSpace(long requiredBytes)
    {
        var available = _availableBytes(_rootDirectory);
        if (requiredBytes > available)
        {
            throw new InsufficientBackupSpaceException(requiredBytes, available);
        }
    }

    private static long DefaultAvailableBytes(string directory)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(directory));
        return string.IsNullOrWhiteSpace(root) ? long.MaxValue : new DriveInfo(root).AvailableFreeSpace;
    }

    private static BackupCopy Describe(string directory)
    {
        var manifestPath = Path.Combine(directory, BackupManifest.FileName);
        if (!File.Exists(manifestPath))
        {
            return new BackupCopy(directory, Directory.GetCreationTimeUtc(directory), IsValid: false);
        }

        BackupManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BackupManifest>(
                File.ReadAllText(manifestPath),
                BackupSerialization.Options);
        }
        catch (JsonException)
        {
            return new BackupCopy(directory, Directory.GetCreationTimeUtc(directory), IsValid: false);
        }

        return manifest is null
            ? new BackupCopy(directory, Directory.GetCreationTimeUtc(directory), IsValid: false)
            : new BackupCopy(directory, manifest.CreatedUtc, Verify(directory, manifest));
    }

    /// <summary>
    /// A copy is restorable when every file it promised is still there and still hashes to what the
    /// manifest recorded. Nothing here opens the database: a byte that changed is enough to disqualify
    /// the copy, and that answer must not depend on a database engine.
    /// </summary>
    private static bool Verify(string directory, BackupManifest manifest)
    {
        if (manifest.FormatVersion != BackupManifest.CurrentFormatVersion)
        {
            return false;
        }

        if (!Matches(Path.Combine(directory, BackupContentPolicy.DatabaseEntryName), manifest.DatabaseSha256))
        {
            return false;
        }

        if (manifest.PreferencesSha256 is { } preferences
            && !Matches(Path.Combine(directory, BackupContentPolicy.PreferencesEntryName), preferences))
        {
            return false;
        }

        return manifest.PersonalArtwork.All(entry => Matches(
            Path.Combine(directory, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)),
            entry.Sha256));
    }

    private static bool Matches(string path, string expectedSha256) =>
        File.Exists(path)
        && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
            .Equals(expectedSha256, StringComparison.OrdinalIgnoreCase);

    private void EnsureInsideRoot(string path)
    {
        if (!path.StartsWith(_rootDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The staged copy is outside the backup folder.");
        }
    }

    private string NextName(DateTimeOffset createdUtc)
    {
        var stamp = createdUtc.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var candidate = Path.Combine(_rootDirectory, stamp);
        for (var suffix = 2; Directory.Exists(candidate); suffix++)
        {
            candidate = Path.Combine(
                _rootDirectory,
                $"{stamp}-{suffix.ToString(CultureInfo.InvariantCulture)}");
        }

        return Path.GetFileName(candidate);
    }
}
