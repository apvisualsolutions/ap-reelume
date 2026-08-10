// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApSolutions.LocalMedia.Application.Backup;

/// <summary>One file inside a copy, named by the path it takes there and pinned by its hash.</summary>
public sealed record BackupFileEntry(string RelativePath, string Sha256, long Length);

/// <summary>
/// A library root as it stood when the copy was taken. The restore wizard needs the old path to offer
/// a remap, so it is recorded even though the catalogue inside the database already holds it.
/// </summary>
public sealed record BackupRootEntry(string Id, string Path, string Kind, string ScanPolicy);

/// <summary>
/// What a copy claims about itself. The format version is checked before anything else is read, so a
/// copy written by a later release is refused instead of half-understood.
/// </summary>
public sealed record BackupManifest(
    int FormatVersion,
    string AppVersion,
    DateTimeOffset CreatedUtc,
    string DatabaseSha256,
    string? PreferencesSha256,
    IReadOnlyList<BackupFileEntry> PersonalArtwork,
    IReadOnlyList<BackupRootEntry> Roots)
{
    public const int CurrentFormatVersion = 1;

    public const string FileName = "manifest.json";
}

/// <summary>The steps a copy walks, in the order it walks them.</summary>
public enum BackupStage
{
    Snapshot,
    Preferences,
    PersonalArtwork,
    Manifest,
    Publish,
    Archive,
}

public sealed record BackupProgress(BackupStage Stage, int Completed, int Total);

/// <summary>
/// A published copy. <paramref name="IsValid"/> means the files still hash to what the manifest says,
/// which is the only thing rotation is allowed to trust when it decides what may be deleted.
/// </summary>
public sealed record BackupCopy(string Path, DateTimeOffset CreatedUtc, bool IsValid);

public sealed record BackupResult(
    BackupCopy Copy,
    BackupManifest Manifest,
    IReadOnlyList<BackupCopy> PrunedCopies);

public sealed record ExportResult(
    string ArchivePath,
    BackupManifest Manifest,
    IReadOnlyList<string> Entries);

/// <summary>
/// Raised before anything is written. A copy that runs out of room halfway would leave the worst
/// possible artefact: a file that looks like a backup and restores nothing.
/// </summary>
public sealed class InsufficientBackupSpaceException(long requiredBytes, long availableBytes)
    : IOException($"A backup needs {requiredBytes} bytes and the volume offers {availableBytes}.")
{
    public long RequiredBytes { get; } = requiredBytes;

    public long AvailableBytes { get; } = availableBytes;
}

/// <summary>The one place that decides what a copy may contain.</summary>
public static class BackupContentPolicy
{
    public const string DatabaseEntryName = "library.db";

    public const string PreferencesEntryName = "settings.json";

    public const string PersonalArtworkDirectoryName = "personal-artwork";

    /// <summary>
    /// True for the four things a copy carries: the consistent database, the preferences, the artwork
    /// a person chose, and the manifest that describes them. Everything else is refused, including the
    /// write-ahead log, downloaded artwork, diagnostics, tokens, and media of any kind.
    /// </summary>
    public static bool IsAllowed(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalized = relativePath.Replace('\\', '/').Trim();
        if (normalized.StartsWith('/')
            || normalized.Contains("../", StringComparison.Ordinal)
            || normalized.EndsWith("/..", StringComparison.Ordinal)
            || normalized == ".."
            || Path.IsPathRooted(normalized))
        {
            return false;
        }

        if (normalized.Equals(DatabaseEntryName, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(PreferencesEntryName, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(BackupManifest.FileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalized.StartsWith(PersonalArtworkDirectoryName + "/", StringComparison.OrdinalIgnoreCase)
            && IsArtworkExtension(Path.GetExtension(normalized));
    }

    private static bool IsArtworkExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".png" or ".webp" => true,
        _ => false,
    };
}

/// <summary>
/// How a manifest is written and read. The options are shared so a copy produced by the application is
/// byte-identical to one produced by a test, and so the reader never guesses.
/// </summary>
public static class BackupSerialization
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}

/// <summary>Writes a database file that can be opened on its own, taken while the application writes.</summary>
public interface IBackupSnapshotWriter
{
    long EstimateBytes();

    Task<BackupFileEntry> WriteAsync(string destinationPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// The folder of rotating copies. Staging is separate from publication so a half-written copy is never
/// something the store would list, and rotation only ever deletes through <see cref="PruneAsync"/>.
/// </summary>
public interface IBackupStore
{
    Task<IReadOnlyList<BackupCopy>> ListAsync(CancellationToken cancellationToken = default);

    Task<string> CreateStagingAsync(CancellationToken cancellationToken = default);

    void DiscardStaging(string stagingDirectory);

    Task<BackupCopy> PublishAsync(
        string stagingDirectory,
        DateTimeOffset createdUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupCopy>> PruneAsync(int retention, CancellationToken cancellationToken = default);

    void EnsureSpace(long requiredBytes);
}

/// <summary>Packs a prepared payload into one archive, refusing anything the policy does not allow.</summary>
public interface IBackupArchiveWriter
{
    Task<IReadOnlyList<string>> WriteAsync(
        string archivePath,
        string sourceDirectory,
        IProgress<BackupProgress>? progress,
        CancellationToken cancellationToken = default);
}
