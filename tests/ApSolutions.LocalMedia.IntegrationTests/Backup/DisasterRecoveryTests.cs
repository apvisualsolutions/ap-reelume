// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Backup;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Infrastructure.Backup;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Backup;

/// <summary>
/// Restoring a library onto a machine that never had it. The invariant that governs every case here is
/// the same: whatever goes wrong, the database that was already there is still there, byte for byte.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DisasterRecoveryTests
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    [Fact]
    public async Task A_restore_brings_back_every_personal_thing_the_backup_carried()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        await fixture.EmptyTheActiveDatabaseAsync();

        var result = await fixture.RestoreAsync(fixture.ArchivePath, []);

        Assert.True(result.Restored);
        var factory = new SqliteConnectionFactory(fixture.Paths.DatabasePath);
        var personal = await new PersonalStateRepository(factory).GetAsync(
            ContentKey.ForTitle(RestoreFixture.Movie),
            TestContext.Current.CancellationToken);
        var progress = await new WatchStateRepository(factory).GetAsync(
            ContentKey.ForTitle(RestoreFixture.Movie),
            TestContext.Current.CancellationToken);
        var markers = await new IntroMarkerRepository(factory).GetForSeriesAsync(
            RestoreFixture.Series,
            TestContext.Current.CancellationToken);

        Assert.NotNull(personal);
        Assert.True(personal.IsFavorite);
        Assert.Equal(4, personal.Rating);
        Assert.NotNull(progress);
        Assert.Equal(TimeSpan.FromMinutes(21), progress.Position);
        Assert.Single(markers);
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            1L,
            await SqliteBootstrapTests.ScalarInt64Async(
                connection,
                "SELECT COUNT(*) FROM match_candidates WHERE decision_locked = 1;"));
        Assert.Equal(
            "{\"Theme\":\"Dark\"}",
            await File.ReadAllTextAsync(fixture.Paths.SettingsPath, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(Path.Combine(
            fixture.Paths.PersonalArtworkDirectory,
            RestoreFixture.Movie.Value.ToString("D"),
            "poster.jpg")));
    }

    [Fact]
    public async Task The_database_that_was_replaced_is_kept_where_a_person_can_find_it()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var replaced = await File.ReadAllBytesAsync(
            fixture.Paths.DatabasePath,
            TestContext.Current.CancellationToken);

        var result = await fixture.RestoreAsync(fixture.ArchivePath, []);

        Assert.True(result.Restored);
        Assert.NotNull(result.PreservedDatabasePath);
        Assert.True(File.Exists(result.PreservedDatabasePath));
        Assert.Equal(
            replaced,
            await File.ReadAllBytesAsync(result.PreservedDatabasePath!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_restore_with_a_remap_moves_every_stored_path_under_the_new_root()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var destination = Path.Combine(fixture.WorkingDirectory, "restored-root");

        var result = await fixture.RestoreAsync(
            fixture.ArchivePath,
            [new RootRemap(fixture.RootPath, destination)]);

        Assert.True(result.Restored);
        Assert.Equal(1, result.Preview.PathChangeCount);
        var stored = await fixture.ReadStoredPathsAsync();
        Assert.Equal([destination], stored.Roots);
        Assert.All(stored.MediaFiles, path => Assert.StartsWith(
            destination + Path.DirectorySeparatorChar,
            path,
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_restore_without_a_remap_keeps_every_stored_path_exactly_as_it_was()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var before = await fixture.ReadStoredPathsAsync();

        var result = await fixture.RestoreAsync(fixture.ArchivePath, []);

        Assert.True(result.Restored);
        Assert.Equal(0, result.Preview.PathChangeCount);
        var after = await fixture.ReadStoredPathsAsync();
        Assert.Equal(before.Roots, after.Roots);
        Assert.Equal(before.MediaFiles, after.MediaFiles);
    }

    [Fact]
    public async Task A_restored_library_rescanned_in_its_new_place_finds_no_duplicates()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var destination = Path.Combine(fixture.WorkingDirectory, "restored-root");
        fixture.CopySourceTree(destination);
        await fixture.EmptyTheActiveDatabaseAsync();

        await fixture.RestoreAsync(fixture.ArchivePath, [new RootRemap(fixture.RootPath, destination)]);
        var summary = await fixture.RescanAsync();

        Assert.Equal(1, summary.EnumeratedCount);
        Assert.Equal(0, summary.ProbeCount);
        Assert.Equal(1, summary.UnchangedCount);
        var stored = await fixture.ReadStoredPathsAsync();
        Assert.Single(stored.MediaFiles);
        Assert.Single(stored.Roots);
    }

    [Theory]
    [InlineData(RestoreFailure.UnreadableArchive)]
    [InlineData(RestoreFailure.TamperedDatabase)]
    [InlineData(RestoreFailure.ZipSlip)]
    [InlineData(RestoreFailure.ForbiddenEntry)]
    [InlineData(RestoreFailure.CorruptDatabase)]
    [InlineData(RestoreFailure.MissingManifest)]
    [InlineData(RestoreFailure.RootConflict)]
    [InlineData(RestoreFailure.NotEnoughSpace)]
    [InlineData(RestoreFailure.CancelledBeforeSwap)]
    [InlineData(RestoreFailure.FailedDuringSwap)]
    public async Task Every_failure_leaves_the_active_database_untouched(RestoreFailure failure)
    {
        using var fixture = await RestoreFixture.CreateAsync(secondRoot: "E:\\archive");
        var before = await File.ReadAllBytesAsync(
            fixture.Paths.DatabasePath,
            TestContext.Current.CancellationToken);
        var settingsBefore = await File.ReadAllTextAsync(
            fixture.Paths.SettingsPath,
            TestContext.Current.CancellationToken);

        var outcome = await fixture.AttemptFailedRestoreAsync(failure);

        Assert.False(outcome.Restored);
        Assert.Equal(
            before,
            await File.ReadAllBytesAsync(fixture.Paths.DatabasePath, TestContext.Current.CancellationToken));
        Assert.Equal(
            settingsBefore,
            await File.ReadAllTextAsync(fixture.Paths.SettingsPath, TestContext.Current.CancellationToken));
        await using var connection = new SqliteConnection(
            $"Data Source={fixture.Paths.DatabasePath};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal("ok", await SqliteBootstrapTests.ScalarTextAsync(connection, "PRAGMA integrity_check;"));
        Assert.Empty(fixture.StagingLeftovers());
    }

    [Fact]
    public async Task A_swap_that_fails_halfway_puts_the_original_database_back()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var before = await File.ReadAllBytesAsync(
            fixture.Paths.DatabasePath,
            TestContext.Current.CancellationToken);

        var outcome = await fixture.AttemptFailedRestoreAsync(RestoreFailure.FailedDuringSwap);

        Assert.False(outcome.Restored);
        Assert.NotNull(outcome.Failure);
        Assert.True(File.Exists(fixture.Paths.DatabasePath));
        Assert.Equal(
            before,
            await File.ReadAllBytesAsync(fixture.Paths.DatabasePath, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Restores a real library into a real folder somewhere else on this machine and writes down what
    /// happened. It only runs when handed a destination, so the suite is unaffected. This is the fixture
    /// the physical verification drives once per kind of destination: a new folder, a substituted drive,
    /// and a local UNC path.
    /// </summary>
    [Fact]
    public async Task Physical_restore_fixture()
    {
        var destination = Environment.GetEnvironmentVariable("AP_LOCALMEDIA_RESTORE_TARGET");
        var report = Environment.GetEnvironmentVariable("AP_LOCALMEDIA_RESTORE_REPORT");
        if (string.IsNullOrWhiteSpace(destination) || string.IsNullOrWhiteSpace(report))
        {
            return;
        }

        using var fixture = await RestoreFixture.CreateAsync();
        fixture.CopySourceTree(destination);
        fixture.RemoveSourceTree();
        await fixture.EmptyTheActiveDatabaseAsync();

        var preview = await fixture.PreviewAsync(
            fixture.ArchivePath,
            [new RootRemap(fixture.RootPath, destination)]);
        var result = await fixture.RestoreAsync(
            fixture.ArchivePath,
            [new RootRemap(fixture.RootPath, destination)]);
        var stored = await fixture.ReadStoredPathsAsync();
        var rescan = await fixture.RescanAsync();
        var afterRescan = await fixture.ReadStoredPathsAsync();
        var factory = new SqliteConnectionFactory(fixture.Paths.DatabasePath);
        var personal = await new PersonalStateRepository(factory).GetAsync(
            ContentKey.ForTitle(RestoreFixture.Movie),
            TestContext.Current.CancellationToken);
        var progress = await new WatchStateRepository(factory).GetAsync(
            ContentKey.ForTitle(RestoreFixture.Movie),
            TestContext.Current.CancellationToken);
        var markers = await new IntroMarkerRepository(factory).GetForSeriesAsync(
            RestoreFixture.Series,
            TestContext.Current.CancellationToken);

        await File.WriteAllTextAsync(
            report,
            JsonSerializer.Serialize(
                new
                {
                    restored = result.Restored,
                    previewRootStatus = preview.Roots.Select(root => root.Status.ToString()).ToArray(),
                    pathChanges = preview.PathChangeCount,
                    storedRootsUnderDestination = stored.Roots.Count(path =>
                        path.StartsWith(destination, StringComparison.OrdinalIgnoreCase)),
                    storedFilesUnderDestination = stored.MediaFiles.Count(path =>
                        path.StartsWith(destination, StringComparison.OrdinalIgnoreCase)),
                    preservedDatabaseKept = result.PreservedDatabasePath is { } kept && File.Exists(kept),
                    favorite = personal?.IsFavorite ?? false,
                    rating = personal?.Rating,
                    positionSeconds = progress?.Position.TotalSeconds ?? -1,
                    markers = markers.Count,
                    preferencesRestored = File.Exists(fixture.Paths.SettingsPath),
                    artworkRestored = Directory.Exists(fixture.Paths.PersonalArtworkDirectory)
                        && Directory.EnumerateFiles(
                            fixture.Paths.PersonalArtworkDirectory,
                            "*",
                            SearchOption.AllDirectories).Any(),
                    rescanEnumerated = rescan.EnumeratedCount,
                    rescanProbed = rescan.ProbeCount,
                    rescanUnchanged = rescan.UnchangedCount,
                    duplicatesAfterRescan = afterRescan.MediaFiles.Count - 1,
                    stagingLeftovers = fixture.StagingLeftovers().Length,
                },
                ReportOptions),
            TestContext.Current.CancellationToken);

        Assert.True(result.Restored);
    }

    [Fact]
    public async Task A_restore_whose_staging_fails_outright_reports_it_and_changes_nothing()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var before = await File.ReadAllBytesAsync(
            fixture.Paths.DatabasePath,
            TestContext.Current.CancellationToken);

        var outcome = await fixture.RestoreWithBrokenStagingAsync();

        Assert.False(outcome.Restored);
        Assert.NotNull(outcome.Failure);
        Assert.Contains(
            outcome.Preview.Findings,
            finding => finding.Kind == RestoreFindingKind.UnreadableArchive);
        Assert.Equal(
            before,
            await File.ReadAllBytesAsync(fixture.Paths.DatabasePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_restore_that_cannot_run_says_why_and_never_stages_anything_permanent()
    {
        using var fixture = await RestoreFixture.CreateAsync(secondRoot: "E:\\archive");

        var outcome = await fixture.RestoreAsync(
            fixture.ArchivePath,
            [
                new RootRemap(fixture.RootPath, "F:\\one"),
                new RootRemap("E:\\archive", "F:\\one"),
            ]);

        Assert.False(outcome.Restored);
        Assert.Contains(
            outcome.Preview.Findings,
            finding => finding.Kind == RestoreFindingKind.RootConflict);
        Assert.Null(outcome.PreservedDatabasePath);
        Assert.Empty(fixture.StagingLeftovers());
    }
}

/// <summary>The phase a restore is asked to fail in, so the invariant can be checked one phase at a time.</summary>
public enum RestoreFailure
{
    UnreadableArchive,
    TamperedDatabase,
    ZipSlip,
    ForbiddenEntry,
    CorruptDatabase,
    MissingManifest,
    RootConflict,
    NotEnoughSpace,
    CancelledBeforeSwap,
    FailedDuringSwap,
}

/// <summary>
/// A data folder with a real library in it, an archive exported from that library, and the ability to
/// hand the restore a broken version of that archive. Both restore suites build on this so a defect one
/// finds is reproducible from the other.
/// </summary>
internal sealed class RestoreFixture : IDisposable
{
    public static readonly TitleId Movie = new(Guid.Parse("bb000000-0000-4000-8000-000000000001"));
    public static readonly SeriesId Series = new(Guid.Parse("bb000000-0000-4000-8000-000000000002"));

    private static readonly MediaFileId MediaFile = new(Guid.Parse("bb000000-0000-4000-8000-000000000003"));
    private static readonly LibraryRootId RootId = new(Guid.Parse("bb000000-0000-4000-8000-000000000004"));
    private static readonly LibraryRootId SecondRootId = new(Guid.Parse("bb000000-0000-4000-8000-000000000005"));
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly DatabaseTestDirectory _directory;

    private RestoreFixture(
        DatabaseTestDirectory directory,
        TestPaths paths,
        string rootPath,
        string archivePath,
        string? secondRootPath)
    {
        _directory = directory;
        Paths = paths;
        RootPath = rootPath;
        ArchivePath = archivePath;
        SecondRootPath = secondRootPath ?? rootPath;
    }

    public TestPaths Paths { get; }

    public string RootPath { get; }

    /// <summary>The second root when the fixture was asked for one, and the first when it was not.</summary>
    public string SecondRootPath { get; }

    public string ArchivePath { get; }

    public string WorkingDirectory => _directory.Path;

    public static async Task<RestoreFixture> CreateAsync(string? secondRoot = null)
    {
        var directory = new DatabaseTestDirectory();
        var paths = new TestPaths(Path.Combine(directory.Path, "data"));
        Directory.CreateDirectory(paths.DataRoot);
        var rootPath = Path.Combine(directory.Path, "source");
        Directory.CreateDirectory(rootPath);
        await File.WriteAllBytesAsync(
            Path.Combine(rootPath, "film.mkv"),
            [0x41, 0x50, 0x53],
            TestContext.Current.CancellationToken);

        var factory = await MigratedSchemaTemplate.CreateFactoryAsync(paths.DatabasePath, TestContext.Current.CancellationToken);
        await SeedAsync(factory, rootPath, secondRoot);
        await File.WriteAllTextAsync(
            paths.SettingsPath,
            "{\"Theme\":\"Dark\"}",
            TestContext.Current.CancellationToken);
        var artwork = Path.Combine(paths.PersonalArtworkDirectory, Movie.Value.ToString("D"));
        Directory.CreateDirectory(artwork);
        await File.WriteAllTextAsync(
            Path.Combine(artwork, "poster.jpg"),
            "personal poster",
            TestContext.Current.CancellationToken);

        var archivePath = Path.Combine(directory.Path, "backup.zip");
        var store = new RotatingBackupStore(paths.BackupsDirectory);
        await new ExportLibrary(
            new CreateBackup(
                new SqliteBackupService(factory),
                store,
                paths,
                new LibraryRootRepository(factory),
                new FixedClock(Noon),
                appVersion: "1.0.0"),
            store,
            new ZipExportService())
            .ExecuteAsync(archivePath, progress: null, TestContext.Current.CancellationToken);

        return new RestoreFixture(directory, paths, rootPath, archivePath, secondRoot);
    }

    public Task<RestorePreview> PreviewAsync(
        string archivePath,
        IReadOnlyList<RootRemap> remaps,
        long availableBytes = long.MaxValue) =>
        CreatePreview(availableBytes).ExecuteAsync(archivePath, remaps, TestContext.Current.CancellationToken);

    public Task<RestoreResult> RestoreAsync(
        string archivePath,
        IReadOnlyList<RootRemap> remaps,
        long availableBytes = long.MaxValue) =>
        CreateRestore(availableBytes).ExecuteAsync(
            archivePath,
            remaps,
            progress: null,
            TestContext.Current.CancellationToken);

    /// <summary>Runs one restore that must not succeed, breaking exactly the phase it is asked to break.</summary>
    public async Task<RestoreResult> AttemptFailedRestoreAsync(RestoreFailure failure)
    {
        switch (failure)
        {
            case RestoreFailure.UnreadableArchive:
                var garbage = Path.Combine(WorkingDirectory, "garbage.zip");
                await File.WriteAllTextAsync(
                    garbage,
                    "not an archive",
                    TestContext.Current.CancellationToken);
                return await RestoreAsync(garbage, []);
            case RestoreFailure.TamperedDatabase:
                return await RestoreAsync(
                    Rebuild(entries => entries.Select(entry => entry.Key == BackupContentPolicy.DatabaseEntryName
                        ? new KeyValuePair<string, byte[]>(entry.Key, Encoding.UTF8.GetBytes("tampered"))
                        : entry)),
                    []);
            case RestoreFailure.ZipSlip:
                return await RestoreAsync(
                    Rebuild(entries => entries.Append(new KeyValuePair<string, byte[]>(
                        "../escaped.json",
                        Encoding.UTF8.GetBytes("owned")))),
                    []);
            case RestoreFailure.ForbiddenEntry:
                return await RestoreAsync(
                    Rebuild(entries => entries.Append(new KeyValuePair<string, byte[]>(
                        "payload.mp4",
                        Encoding.UTF8.GetBytes("video")))),
                    []);
            case RestoreFailure.CorruptDatabase:
                return await RestoreAsync(
                    RebuildWithMatchingHashes(entries => entries.Select(entry =>
                        entry.Key == BackupContentPolicy.DatabaseEntryName
                            ? new KeyValuePair<string, byte[]>(
                                entry.Key,
                                Encoding.UTF8.GetBytes("SQLite format 3\0 but truncated"))
                            : entry)),
                    []);
            case RestoreFailure.MissingManifest:
                return await RestoreAsync(
                    Rebuild(entries => entries.Where(entry => entry.Key != BackupManifest.FileName)),
                    []);
            case RestoreFailure.RootConflict:
                return await RestoreAsync(
                    ArchivePath,
                    [new RootRemap(RootPath, "F:\\one"), new RootRemap(SecondRootPath, "F:\\one")]);
            case RestoreFailure.NotEnoughSpace:
                return await RestoreAsync(ArchivePath, [], availableBytes: 8);
            case RestoreFailure.CancelledBeforeSwap:
                using (var cancellation = new CancellationTokenSource())
                {
                    return await CreateRestore(long.MaxValue, onBeforeSwap: cancellation.Cancel)
                        .ExecuteAsync(ArchivePath, [], progress: null, cancellation.Token);
                }

            default:
                return await CreateRestore(
                        long.MaxValue,
                        onBeforeSwap: () => throw new IOException("the swap was interrupted"))
                    .ExecuteAsync(ArchivePath, [], progress: null, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Runs a restore whose staging cannot even begin. It is the one failure that happens before there is
    /// anything to preview, so it has to be reported rather than thrown at whoever pressed the button.
    /// </summary>
    public Task<RestoreResult> RestoreWithBrokenStagingAsync()
    {
        var staging = new BrokenStagingService(CreateStagedRestore(long.MaxValue, onBeforeSwap: null));
        return new RestoreBackup(new PreviewRestore(new BackupValidator(), staging), staging)
            .ExecuteAsync(ArchivePath, [], progress: null, TestContext.Current.CancellationToken);
    }

    /// <summary>The real staging service, with the real free-space reading behind it.</summary>
    public StagedRestoreService CreateStagedRestoreService() => new(Paths);

    /// <summary>
    /// A dry run whose staged database blows up while being inspected. The staged copy must still be
    /// gone afterwards: a failure that leaves rubbish behind fills a disk one attempt at a time.
    /// </summary>
    public Task PreviewWithBrokenInspectionAsync()
    {
        var staging = new BrokenStagingService(
            CreateStagedRestore(long.MaxValue, onBeforeSwap: null),
            failOnInspect: true);
        return new PreviewRestore(new BackupValidator(), staging)
            .ExecuteAsync(ArchivePath, [], TestContext.Current.CancellationToken);
    }

    /// <summary>Replaces the archive's manifest, leaving every other entry alone.</summary>
    public async Task<string> RewriteManifestAsync(Func<BackupManifest, BackupManifest> change)
    {
        var entries = ReadEntries();
        var manifest = JsonSerializer.Deserialize<BackupManifest>(
            Encoding.UTF8.GetString(entries[BackupManifest.FileName]),
            BackupSerialization.Options)!;
        entries[BackupManifest.FileName] = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(change(manifest), BackupSerialization.Options));
        var path = Write(entries);
        await Task.CompletedTask;
        return path;
    }

    public string Rebuild(
        Func<IEnumerable<KeyValuePair<string, byte[]>>, IEnumerable<KeyValuePair<string, byte[]>>> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return Write(transform(ReadEntries()).ToDictionary(entry => entry.Key, entry => entry.Value));
    }

    /// <summary>
    /// Rebuilds and recomputes the manifest so every hash still matches. It is how a corrupt database
    /// gets past the hashes and has to be caught by actually opening it.
    /// </summary>
    public string RebuildWithMatchingHashes(
        Func<IEnumerable<KeyValuePair<string, byte[]>>, IEnumerable<KeyValuePair<string, byte[]>>> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        var entries = transform(ReadEntries()).ToDictionary(entry => entry.Key, entry => entry.Value);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(
            Encoding.UTF8.GetString(entries[BackupManifest.FileName]),
            BackupSerialization.Options)!;
        var updated = manifest with
        {
            DatabaseSha256 = Sha256(entries[BackupContentPolicy.DatabaseEntryName]),
            PreferencesSha256 = entries.TryGetValue(BackupContentPolicy.PreferencesEntryName, out var preferences)
                ? Sha256(preferences)
                : null,
            PersonalArtwork = [.. manifest.PersonalArtwork.Select(artwork => artwork with
            {
                Sha256 = Sha256(entries[artwork.RelativePath]),
            })],
        };
        entries[BackupManifest.FileName] = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(updated, BackupSerialization.Options));
        return Write(entries);
    }

    public async Task EmptyTheActiveDatabaseAsync()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(Paths.DatabasePath);
        await MigratedSchemaTemplate.CopyToAsync(
            Paths.DatabasePath,
            TestContext.Current.CancellationToken);
        File.Delete(Paths.SettingsPath);
        if (Directory.Exists(Paths.PersonalArtworkDirectory))
        {
            Directory.Delete(Paths.PersonalArtworkDirectory, recursive: true);
        }
    }

    /// <summary>Takes the original library folder away, which is what restoring elsewhere looks like.</summary>
    public void RemoveSourceTree()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

    public void CopySourceTree(string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(RootPath, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    public async Task<StoredPaths> ReadStoredPathsAsync()
    {
        await using var connection = new SqliteConnection(
            $"Data Source={Paths.DatabasePath};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return new StoredPaths(
            await ReadColumnAsync(connection, "SELECT normalized_path FROM library_roots ORDER BY normalized_path;"),
            await ReadColumnAsync(connection, "SELECT normalized_path FROM media_files ORDER BY normalized_path;"));
    }

    /// <summary>Scans the restored library where it now lives, which is where duplicates would appear.</summary>
    public async Task<ScanSummary> RescanAsync()
    {
        var factory = new SqliteConnectionFactory(Paths.DatabasePath);
        var roots = new LibraryRootRepository(factory);
        var root = (await roots.ListAsync(TestContext.Current.CancellationToken))[0];
        var coordinator = new ScanCoordinator(
            roots,
            new MediaFileRepository(factory),
            new MediaFileEnumerator(),
            new SilentProbe(),
            new SilentPublisher());
        return await coordinator.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Manual),
            TestContext.Current.CancellationToken);
    }

    public string[] StagingLeftovers() =>
        Directory.Exists(Paths.RestoreStagingDirectory)
            ? Directory.GetFileSystemEntries(Paths.RestoreStagingDirectory)
            : [];

    public void Dispose() => _directory.Dispose();

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static async Task<string[]> ReadColumnAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var values = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }

    private static async Task SeedAsync(SqliteConnectionFactory factory, string rootPath, string? secondRoot)
    {
        await new PersonalStateRepository(factory).SaveAsync(
            PersonalState.Empty(ContentKey.ForTitle(Movie)).WithFavorite(true).WithRating(4),
            Noon,
            TestContext.Current.CancellationToken);
        await new WatchStateRepository(factory).SaveAsync(
            new WatchState
            {
                Content = ContentKey.ForTitle(Movie),
                Position = TimeSpan.FromMinutes(21),
                ObservedDuration = TimeSpan.FromMinutes(102),
                SourceMediaFileId = MediaFile,
                Status = WatchStatus.InProgress,
                IsManualOverride = false,
                StartedUtc = Noon,
                UpdatedUtc = Noon,
            },
            TestContext.Current.CancellationToken);
        await new IntroMarkerRepository(factory).SaveAsync(
            new IntroMarker(
                Guid.Parse("bb000000-0000-4000-8000-000000000006"),
                Series,
                MarkerKind.Intro,
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(75),
                MarkerOrigin.Manual,
                Confidence: null,
                UserCorrected: true),
            TestContext.Current.CancellationToken);

        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO library_roots (id, normalized_path, kind, availability, scan_policy)
                VALUES ($rootId, $rootPath, 0, 0, 1);

                INSERT INTO media_files (
                    id, library_root_id, normalized_path, size_bytes, last_write_utc,
                    duration_ticks, container, video_codecs, audio_codecs, width, height, is_available)
                VALUES ($fileId, $rootId, $filePath, 3, $stamp, NULL, 'mkv', '["h264"]', '["aac"]', 1920, 1080, 1);

                INSERT INTO match_candidates (
                    candidate_id, media_file_id, stable_key, content_kind, score,
                    scoring_model_version, review_state, signals_json, explanation_codes_json,
                    revision, decision_locked)
                VALUES ($candidateId, $fileId, 'movie:7', 0, 0.91, 1, 2, '{}', '[]', 1, 1);
                """;
            command.Parameters.AddWithValue("$rootId", RootId.Value.ToString("D"));
            command.Parameters.AddWithValue("$rootPath", rootPath);
            command.Parameters.AddWithValue("$fileId", MediaFile.Value.ToString("D"));
            command.Parameters.AddWithValue("$filePath", Path.Combine(rootPath, "film.mkv"));
            command.Parameters.AddWithValue(
                "$stamp",
                new FileInfo(Path.Combine(rootPath, "film.mkv")).LastWriteTimeUtc.ToString(
                    "O",
                    CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue(
                "$candidateId",
                Guid.Parse("bb000000-0000-4000-8000-000000000007").ToString("D"));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        if (secondRoot is null)
        {
            return;
        }

        await using var second = connection.CreateCommand();
        second.CommandText = """
            INSERT INTO library_roots (id, normalized_path, kind, availability, scan_policy)
            VALUES ($id, $path, 0, 1, 1);
            """;
        second.Parameters.AddWithValue("$id", SecondRootId.Value.ToString("D"));
        second.Parameters.AddWithValue("$path", secondRoot);
        await second.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private Dictionary<string, byte[]> ReadEntries()
    {
        using var archive = ZipFile.OpenRead(ArchivePath);
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            entries[entry.FullName] = buffer.ToArray();
        }

        return entries;
    }

    private string Write(Dictionary<string, byte[]> entries)
    {
        var path = Path.Combine(WorkingDirectory, $"rebuilt-{Guid.NewGuid():N}.zip");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var entry in entries.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var created = archive.CreateEntry(entry.Key, CompressionLevel.Optimal);
            using var target = created.Open();
            target.Write(entry.Value);
        }

        return path;
    }

    private PreviewRestore CreatePreview(long availableBytes) => new(
        new BackupValidator(),
        CreateStagedRestore(availableBytes, onBeforeSwap: null));

    private RestoreBackup CreateRestore(long availableBytes, Action? onBeforeSwap = null) => new(
        new PreviewRestore(new BackupValidator(), CreateStagedRestore(availableBytes, onBeforeSwap)),
        CreateStagedRestore(availableBytes, onBeforeSwap));

    private StagedRestoreService CreateStagedRestore(long availableBytes, Action? onBeforeSwap) =>
        new(Paths, _ => availableBytes, onBeforeSwap);

    public sealed record StoredPaths(IReadOnlyList<string> Roots, IReadOnlyList<string> MediaFiles);

    internal sealed class TestPaths(string dataRoot) : IAppDataPaths
    {
        public string DataRoot { get; } = dataRoot;

        public string DatabasePath { get; } = Path.Combine(dataRoot, "library.db");

        public string SettingsPath { get; } = Path.Combine(dataRoot, "settings.json");

        public string BackupsDirectory { get; } = Path.Combine(dataRoot, "backups");

        public string PersonalArtworkDirectory { get; } = Path.Combine(dataRoot, "personal-artwork");

        public string RemoteCacheDirectory { get; } = Path.Combine(dataRoot, "cache", "artwork");

        public string CourseThumbnailDirectory { get; } = Path.Combine(dataRoot, "cache", "course-thumbnails");

        public string DiagnosticsDirectory { get; } = Path.Combine(dataRoot, "diagnostics");

        // Never the key Windows reads at sign-in: a suite leaves nothing behind there.
        public string StartupRegistrySubKey { get; } =
            @"Software\APSolutions\LocalMedia\Tests\Run";

        // And nothing this suite drives may reach the operating system either, so the handover it
        // would otherwise make lands under its own root.
        public string? SystemHandoffDirectory { get; } = Path.Combine(dataRoot, "handoff");

        public string RestoreStagingDirectory => Path.Combine(BackupsDirectory, ".restore");
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    /// <summary>A staging service whose unpacking always fails, and that answers everything else honestly.</summary>
    private sealed class BrokenStagingService(IStagedRestoreService inner, bool failOnInspect = false)
        : IStagedRestoreService
    {
        public long GetAvailableBytes() => inner.GetAvailableBytes();

        public Task<string> ExtractAsync(string archivePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return failOnInspect
                ? inner.ExtractAsync(archivePath, cancellationToken)
                : Task.FromException<string>(new IOException("the staging folder is unavailable"));
        }

        public Task<RestoreDatabaseFacts> InspectDatabaseAsync(
            string stagingDirectory,
            CancellationToken cancellationToken = default) =>
            failOnInspect
                ? Task.FromException<RestoreDatabaseFacts>(
                    new InvalidDataException("the staged database could not be inspected"))
                : inner.InspectDatabaseAsync(stagingDirectory, cancellationToken);

        public Task<int> ApplyRemapAsync(
            string stagingDirectory,
            IReadOnlyList<RootRemapDecision> decisions,
            CancellationToken cancellationToken = default) =>
            inner.ApplyRemapAsync(stagingDirectory, decisions, cancellationToken);

        public Task<string> SwapAsync(string stagingDirectory, CancellationToken cancellationToken = default) =>
            inner.SwapAsync(stagingDirectory, cancellationToken);

        public void Discard(string stagingDirectory) => inner.Discard(stagingDirectory);
    }

    private sealed class SilentProbe : IMediaProbe
    {
        public Task<TechnicalMetadata> ProbeAsync(string path, CancellationToken cancellationToken = default)
        {
            _ = path;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new TechnicalMetadata(
                TimeSpan.FromMinutes(1),
                "mkv",
                ["h264"],
                ["aac"],
                1920,
                1080));
        }
    }

    private sealed class SilentPublisher : IApplicationEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
            where TEvent : notnull
        {
            _ = applicationEvent;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
