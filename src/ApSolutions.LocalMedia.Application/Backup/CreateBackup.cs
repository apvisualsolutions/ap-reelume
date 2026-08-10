// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Backup;

/// <summary>
/// Builds one copy of everything a person would lose: the database, their preferences, and the artwork
/// they chose. Nothing is published until all of it is on disk, and rotation runs only after that, so a
/// failed run can never be the reason an older copy disappeared.
/// </summary>
public sealed class CreateBackup
{
    public const int DefaultRetention = 5;

    private readonly IBackupSnapshotWriter _snapshots;
    private readonly IBackupStore _store;
    private readonly IAppDataPaths _paths;
    private readonly ILibraryRootRepository _roots;
    private readonly IClock _clock;
    private readonly string _appVersion;
    private readonly int _retention;

    public CreateBackup(
        IBackupSnapshotWriter snapshots,
        IBackupStore store,
        IAppDataPaths paths,
        ILibraryRootRepository roots,
        IClock clock,
        string appVersion,
        int retention = DefaultRetention)
    {
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);
        _appVersion = appVersion;
        _retention = retention > 0
            ? retention
            : throw new ArgumentOutOfRangeException(nameof(retention));
    }

    public async Task<BackupResult> ExecuteAsync(
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _store.EnsureSpace(EstimateRequiredBytes());
        var staging = await _store.CreateStagingAsync(cancellationToken).ConfigureAwait(false);
        BackupManifest manifest;
        BackupCopy copy;
        try
        {
            var steps = await BuildPayloadAsync(
                staging,
                progress,
                trailingSteps: 1,
                cancellationToken).ConfigureAwait(false);
            manifest = steps.Manifest;
            copy = await _store.PublishAsync(
                staging,
                manifest.CreatedUtc,
                cancellationToken).ConfigureAwait(false);
            progress?.Report(new BackupProgress(BackupStage.Publish, steps.Total, steps.Total));
        }
        catch
        {
            _store.DiscardStaging(staging);
            throw;
        }

        var pruned = await _store.PruneAsync(_retention, cancellationToken).ConfigureAwait(false);
        return new BackupResult(copy, manifest, pruned);
    }

    /// <summary>
    /// Fills a staging folder with the allowlisted payload and writes the manifest that describes it.
    /// The caller decides what happens to the folder afterwards: a rotating copy publishes it, an
    /// export packs it. <paramref name="trailingSteps"/> is what the caller still has to do, so the
    /// progress total is the whole operation rather than this part of it.
    /// </summary>
    public async Task<BackupPayload> BuildPayloadAsync(
        string stagingDirectory,
        IProgress<BackupProgress>? progress = null,
        int trailingSteps = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        var artwork = EnumeratePersonalArtwork();
        var total = 3 + artwork.Count + trailingSteps;
        var completed = 0;

        var database = await _snapshots.WriteAsync(
            Path.Combine(stagingDirectory, BackupContentPolicy.DatabaseEntryName),
            cancellationToken).ConfigureAwait(false);
        progress?.Report(new BackupProgress(BackupStage.Snapshot, ++completed, total));

        var preferences = await CopyPreferencesAsync(stagingDirectory, cancellationToken).ConfigureAwait(false);
        progress?.Report(new BackupProgress(BackupStage.Preferences, ++completed, total));

        var copiedArtwork = new List<BackupFileEntry>(artwork.Count);
        foreach (var source in artwork)
        {
            cancellationToken.ThrowIfCancellationRequested();
            copiedArtwork.Add(await CopyArtworkAsync(source, stagingDirectory, cancellationToken)
                .ConfigureAwait(false));
            progress?.Report(new BackupProgress(BackupStage.PersonalArtwork, ++completed, total));
        }

        if (artwork.Count == 0)
        {
            progress?.Report(new BackupProgress(BackupStage.PersonalArtwork, completed, total));
        }

        var manifest = new BackupManifest(
            BackupManifest.CurrentFormatVersion,
            _appVersion,
            _clock.UtcNow,
            database.Sha256,
            preferences?.Sha256,
            copiedArtwork,
            await ReadRootsAsync(cancellationToken).ConfigureAwait(false));
        await File.WriteAllTextAsync(
            Path.Combine(stagingDirectory, BackupManifest.FileName),
            JsonSerializer.Serialize(manifest, BackupSerialization.Options),
            cancellationToken).ConfigureAwait(false);
        progress?.Report(new BackupProgress(BackupStage.Manifest, ++completed, total));

        return new BackupPayload(manifest, completed, total);
    }

    private long EstimateRequiredBytes()
    {
        var required = _snapshots.EstimateBytes();
        if (File.Exists(_paths.SettingsPath))
        {
            required += new FileInfo(_paths.SettingsPath).Length;
        }

        return EnumeratePersonalArtwork().Aggregate(required, (total, file) => total + file.Length);
    }

    private IReadOnlyList<FileInfo> EnumeratePersonalArtwork() =>
        Directory.Exists(_paths.PersonalArtworkDirectory)
            ? [.. new DirectoryInfo(_paths.PersonalArtworkDirectory)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(file => BackupContentPolicy.IsAllowed(ArtworkEntryName(file.FullName)))
                .OrderBy(file => file.FullName, StringComparer.Ordinal)]
            : [];

    private async Task<BackupFileEntry?> CopyPreferencesAsync(
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.SettingsPath))
        {
            return null;
        }

        var destination = Path.Combine(stagingDirectory, BackupContentPolicy.PreferencesEntryName);
        var bytes = await File.ReadAllBytesAsync(_paths.SettingsPath, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(destination, bytes, cancellationToken).ConfigureAwait(false);
        return new BackupFileEntry(BackupContentPolicy.PreferencesEntryName, Sha256(bytes), bytes.Length);
    }

    private async Task<BackupFileEntry> CopyArtworkAsync(
        FileInfo source,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        var entryName = ArtworkEntryName(source.FullName);
        var destination = Path.Combine(stagingDirectory, entryName.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var bytes = await File.ReadAllBytesAsync(source.FullName, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(destination, bytes, cancellationToken).ConfigureAwait(false);
        return new BackupFileEntry(entryName, Sha256(bytes), bytes.Length);
    }

    private string ArtworkEntryName(string fullPath) =>
        $"{BackupContentPolicy.PersonalArtworkDirectoryName}/" +
        Path.GetRelativePath(_paths.PersonalArtworkDirectory, fullPath).Replace('\\', '/');

    private async Task<IReadOnlyList<BackupRootEntry>> ReadRootsAsync(CancellationToken cancellationToken)
    {
        var roots = await _roots.ListAsync(cancellationToken).ConfigureAwait(false);
        return [.. roots.Select(root => new BackupRootEntry(
            root.Id.Value.ToString("D", CultureInfo.InvariantCulture),
            root.Path,
            root.Kind.ToString(),
            root.ScanPolicy.ToString()))];
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

/// <summary>A staged payload: what it promises, and how far the progress counter got.</summary>
public sealed record BackupPayload(BackupManifest Manifest, int Completed, int Total);
