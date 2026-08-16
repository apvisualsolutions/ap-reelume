// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Infrastructure.Backup;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Backup;

/// <summary>
/// What the exported archive is allowed to carry, and what it must carry. The personal data is checked
/// row by row after the archive has been reopened, because a database file that merely exists proves
/// nothing about what is inside it.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ZipExportTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly TitleId Movie = new(Guid.Parse("aa000000-0000-4000-8000-000000000001"));
    private static readonly SeriesId Series = new(Guid.Parse("aa000000-0000-4000-8000-000000000002"));
    private static readonly MediaFileId File1 = new(Guid.Parse("aa000000-0000-4000-8000-000000000003"));
    private static readonly LibraryRootId Root = new(Guid.Parse("aa000000-0000-4000-8000-000000000004"));

    [Fact]
    public async Task The_archive_holds_exactly_the_allowlisted_payload()
    {
        using var fixture = await ExportFixture.CreateAsync();

        var result = await fixture.ExportAsync();

        using var archive = ZipFile.OpenRead(result.ArchivePath);
        var entries = archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(
            [
                BackupContentPolicy.DatabaseEntryName,
                BackupManifest.FileName,
                "personal-artwork/aa000000-0000-4000-8000-000000000001/poster.jpg",
                BackupContentPolicy.PreferencesEntryName,
            ],
            entries.Order(StringComparer.Ordinal));
        Assert.All(entries, entry => Assert.True(BackupContentPolicy.IsAllowed(entry)));
        Assert.Equal(entries.Order(StringComparer.Ordinal), result.Entries.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task No_video_remote_cache_token_or_diagnostics_reaches_the_archive()
    {
        using var fixture = await ExportFixture.CreateAsync();

        var result = await fixture.ExportAsync();

        using var archive = ZipFile.OpenRead(result.ArchivePath);
        foreach (var entry in archive.Entries)
        {
            Assert.DoesNotContain("cache", entry.FullName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("diagnostic", entry.FullName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", entry.FullName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".db-wal", entry.FullName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".db-shm", entry.FullName, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(
                ".mp4",
                Path.GetExtension(entry.FullName),
                StringComparer.OrdinalIgnoreCase);
        }

        var extracted = fixture.Extract(result.ArchivePath);
        var text = string.Join(
            '\n',
            Directory.EnumerateFiles(extracted, "*.json", SearchOption.AllDirectories)
                .Select(System.IO.File.ReadAllText));
        Assert.DoesNotContain(ExportFixture.CanaryToken, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Personal_marks_progress_markers_and_locked_decisions_survive_the_round_trip()
    {
        using var fixture = await ExportFixture.CreateAsync();
        var result = await fixture.ExportAsync();
        var extracted = fixture.Extract(result.ArchivePath);

        var restoredPath = Path.Combine(extracted, BackupContentPolicy.DatabaseEntryName);
        var restored = new SqliteConnectionFactory(restoredPath);
        var personal = await new PersonalStateRepository(restored).GetAsync(
            ContentKey.ForTitle(Movie),
            TestContext.Current.CancellationToken);
        var progress = await new WatchStateRepository(restored).GetAsync(
            ContentKey.ForTitle(Movie),
            TestContext.Current.CancellationToken);
        var markers = await new IntroMarkerRepository(restored).GetForSeriesAsync(
            Series,
            TestContext.Current.CancellationToken);

        Assert.NotNull(personal);
        Assert.True(personal.IsFavorite);
        Assert.True(personal.IsWatchLater);
        Assert.Equal(9, personal.Rating);
        Assert.NotNull(progress);
        Assert.Equal(TimeSpan.FromMinutes(12), progress.Position);
        Assert.Single(markers);

        await using var connection = new SqliteConnection($"Data Source={restoredPath};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal("ok", await SqliteBootstrapTests.ScalarTextAsync(connection, "PRAGMA integrity_check;"));
        Assert.Equal(
            1L,
            await SqliteBootstrapTests.ScalarInt64Async(
                connection,
                "SELECT COUNT(*) FROM match_candidates WHERE decision_locked = 1;"));
        Assert.Equal(
            1L,
            await SqliteBootstrapTests.ScalarInt64Async(connection, "SELECT COUNT(*) FROM library_roots;"));
    }

    [Fact]
    public async Task Every_manifest_hash_matches_the_file_the_archive_delivered()
    {
        using var fixture = await ExportFixture.CreateAsync();
        var result = await fixture.ExportAsync();
        var extracted = fixture.Extract(result.ArchivePath);

        var manifest = JsonSerializer.Deserialize<BackupManifest>(
            await System.IO.File.ReadAllTextAsync(
                Path.Combine(extracted, BackupManifest.FileName),
                TestContext.Current.CancellationToken),
            BackupSerialization.Options);

        Assert.NotNull(manifest);
        Assert.Equal(BackupManifest.CurrentFormatVersion, manifest.FormatVersion);
        Assert.Equal(
            Sha256(Path.Combine(extracted, BackupContentPolicy.DatabaseEntryName)),
            manifest.DatabaseSha256);
        Assert.Equal(
            Sha256(Path.Combine(extracted, BackupContentPolicy.PreferencesEntryName)),
            manifest.PreferencesSha256);
        var artwork = Assert.Single(manifest.PersonalArtwork);
        Assert.Equal(Sha256(Path.Combine(extracted, artwork.RelativePath)), artwork.Sha256);
        var root = Assert.Single(manifest.Roots);
        Assert.Equal(ExportFixture.RootPath, root.Path);
    }

    [Fact]
    public async Task The_export_reports_progress_and_ends_on_the_archive_stage()
    {
        using var fixture = await ExportFixture.CreateAsync();
        var reported = new List<BackupProgress>();

        // Progress<T> queues its callbacks, so a loaded machine can reach the assertion before the
        // last stages arrive; reporting synchronously keeps the observed walk equal to the real one.
        await fixture.ExportAsync(new ImmediateProgress<BackupProgress>(reported.Add));

        Assert.NotEmpty(reported);
        Assert.Contains(reported, item => item.Stage == BackupStage.Snapshot);
        Assert.Equal(BackupStage.Archive, reported[^1].Stage);
        Assert.Equal(reported[^1].Total, reported[^1].Completed);
    }

    [Fact]
    public async Task A_cancelled_export_leaves_neither_archive_nor_staging()
    {
        using var fixture = await ExportFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var destination = Path.Combine(fixture.WorkingDirectory, "cancelled.zip");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.ExportAsync(progress: null, destination, cancellation.Token));

        Assert.False(System.IO.File.Exists(destination));
        Assert.Empty(fixture.StoredEntries());
    }

    /// <summary>
    /// Writes one real archive where a person can inspect it with their own tools. It only runs when a
    /// destination is handed in, so the suite is unaffected; the export happens while progress rows are
    /// being written, which is the condition the snapshot has to survive.
    /// </summary>
    [Fact]
    public async Task Physical_export_fixture()
    {
        var destination = Environment.GetEnvironmentVariable("AP_LOCALMEDIA_EXPORT_TARGET");
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        using var fixture = await ExportFixture.CreateAsync();
        var repository = new WatchStateRepository(new SqliteConnectionFactory(fixture.Paths.DatabasePath));
        using var writing = new CancellationTokenSource();
        var writer = Task.Run(
            async () =>
            {
                var written = 0;
                while (!writing.IsCancellationRequested)
                {
                    await repository.SaveAsync(
                        new WatchState
                        {
                            Content = ContentKey.ForTitle(new TitleId(Guid.NewGuid())),
                            Position = TimeSpan.FromSeconds(written++),
                            ObservedDuration = TimeSpan.FromMinutes(42),
                            SourceMediaFileId = File1,
                            Status = WatchStatus.InProgress,
                            IsManualOverride = false,
                            StartedUtc = Noon,
                            UpdatedUtc = Noon,
                        },
                        CancellationToken.None);
                }

                return written;
            },
            TestContext.Current.CancellationToken);

        var result = await fixture.ExportAsync(
            progress: null,
            destination,
            TestContext.Current.CancellationToken);
        await writing.CancelAsync();
        var concurrentWrites = await writer;

        Assert.True(concurrentWrites > 0);
        Assert.True(System.IO.File.Exists(result.ArchivePath));
        Assert.NotEmpty(result.Entries);
    }

    [Fact]
    public async Task A_staged_payload_holding_something_it_should_not_is_refused_rather_than_trimmed()
    {
        using var fixture = await ExportFixture.CreateAsync();
        var staging = Path.Combine(fixture.WorkingDirectory, "tampered-staging");
        Directory.CreateDirectory(staging);
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(staging, BackupContentPolicy.DatabaseEntryName),
            "database",
            TestContext.Current.CancellationToken);
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(staging, "leaked.mp4"),
            "video",
            TestContext.Current.CancellationToken);
        var destination = Path.Combine(fixture.WorkingDirectory, "refused.zip");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ZipExportService().WriteAsync(
                destination,
                staging,
                progress: null,
                TestContext.Current.CancellationToken));

        Assert.Contains("leaked.mp4", failure.Message, StringComparison.Ordinal);
        Assert.False(System.IO.File.Exists(destination));
    }

    [Fact]
    public async Task An_archive_interrupted_halfway_leaves_no_temporary_file_behind()
    {
        using var fixture = await ExportFixture.CreateAsync();
        var staging = Path.Combine(fixture.WorkingDirectory, "staging");
        Directory.CreateDirectory(staging);
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(staging, BackupContentPolicy.DatabaseEntryName),
            "database",
            TestContext.Current.CancellationToken);
        await System.IO.File.WriteAllTextAsync(
            Path.Combine(staging, BackupContentPolicy.PreferencesEntryName),
            "{}",
            TestContext.Current.CancellationToken);
        var destination = Path.Combine(fixture.WorkingDirectory, "interrupted.zip");
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ZipExportService().WriteAsync(
                destination,
                staging,
                new CancellingProgress(cancellation),
                cancellation.Token));

        Assert.False(System.IO.File.Exists(destination));
        Assert.Empty(Directory.EnumerateFiles(fixture.WorkingDirectory, "*.tmp"));
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(path))).ToLowerInvariant();

    /// <summary>Cancels as soon as the first entry has been written, which is the interesting moment.</summary>
    private sealed class CancellingProgress(CancellationTokenSource cancellation) : IProgress<BackupProgress>
    {
        public void Report(BackupProgress value)
        {
            _ = value;
            cancellation.Cancel();
        }
    }

    /// <summary>
    /// A data folder shaped like the real one: a migrated database with personal rows, preferences,
    /// personal artwork, downloaded artwork, a token file, diagnostics, and a video. Only four of those
    /// may leave in the archive.
    /// </summary>
    private sealed class ExportFixture : IDisposable
    {
        public const string CanaryToken = "tmdb-access-token-canary-value";
        public const string RootPath = "D:\\media";

        private readonly DatabaseTestDirectory _directory;

        private ExportFixture(DatabaseTestDirectory directory, TestAppDataPaths paths)
        {
            _directory = directory;
            Paths = paths;
        }

        public TestAppDataPaths Paths { get; }

        public string WorkingDirectory => _directory.Path;

        public string BackupsDirectory => Paths.BackupsDirectory;

        /// <summary>
        /// Whatever the backup folder holds, including nothing at all: a run that stops before it
        /// creates the folder is as correct as one that cleans it up.
        /// </summary>
        public string[] StoredEntries() =>
            Directory.Exists(BackupsDirectory) ? Directory.GetFileSystemEntries(BackupsDirectory) : [];

        public static async Task<ExportFixture> CreateAsync()
        {
            var directory = new DatabaseTestDirectory();
            var paths = new TestAppDataPaths(Path.Combine(directory.Path, "data"));
            Directory.CreateDirectory(paths.DataRoot);
            var factory = new SqliteConnectionFactory(paths.DatabasePath);
            await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
            await SeedPersonalDataAsync(factory);

            await System.IO.File.WriteAllTextAsync(
                paths.SettingsPath,
                "{\"Theme\":\"Dark\",\"TrayEnabled\":false}",
                TestContext.Current.CancellationToken);
            var artworkFolder = Path.Combine(paths.PersonalArtworkDirectory, Movie.Value.ToString("D"));
            Directory.CreateDirectory(artworkFolder);
            await System.IO.File.WriteAllTextAsync(
                Path.Combine(artworkFolder, "poster.jpg"),
                "personal poster bytes",
                TestContext.Current.CancellationToken);
            Directory.CreateDirectory(paths.RemoteCacheDirectory);
            await System.IO.File.WriteAllTextAsync(
                Path.Combine(paths.RemoteCacheDirectory, "downloaded.jpg"),
                "downloaded poster",
                TestContext.Current.CancellationToken);
            Directory.CreateDirectory(paths.DiagnosticsDirectory);
            await System.IO.File.WriteAllTextAsync(
                Path.Combine(paths.DiagnosticsDirectory, "report.json"),
                $"{{\"token\":\"{CanaryToken}\"}}",
                TestContext.Current.CancellationToken);
            await System.IO.File.WriteAllTextAsync(
                Path.Combine(paths.DataRoot, "tmdb.token"),
                CanaryToken,
                TestContext.Current.CancellationToken);
            await System.IO.File.WriteAllTextAsync(
                Path.Combine(paths.DataRoot, "sample.mp4"),
                "video bytes",
                TestContext.Current.CancellationToken);

            return new ExportFixture(directory, paths);
        }

        public Task<ExportResult> ExportAsync(IProgress<BackupProgress>? progress = null) =>
            ExportAsync(progress, Path.Combine(WorkingDirectory, "export.zip"), TestContext.Current.CancellationToken);

        public Task<ExportResult> ExportAsync(
            IProgress<BackupProgress>? progress,
            string destination,
            CancellationToken cancellationToken)
        {
            var factory = new SqliteConnectionFactory(Paths.DatabasePath);
            var store = new RotatingBackupStore(Paths.BackupsDirectory);
            var create = new CreateBackup(
                new SqliteBackupService(factory),
                store,
                Paths,
                new LibraryRootRepository(factory),
                new FixedClock(Noon),
                appVersion: "1.0.0");
            return new ExportLibrary(create, store, new ZipExportService())
                .ExecuteAsync(destination, progress, cancellationToken);
        }

        public string Extract(string archivePath)
        {
            var target = Path.Combine(WorkingDirectory, $"extracted-{Guid.NewGuid():N}");
            ZipFile.ExtractToDirectory(archivePath, target);
            return target;
        }

        public void Dispose() => _directory.Dispose();

        private static async Task SeedPersonalDataAsync(SqliteConnectionFactory factory)
        {
            await new PersonalStateRepository(factory).SaveAsync(
                PersonalState.Empty(ContentKey.ForTitle(Movie))
                    .WithFavorite(true)
                    .WithWatchLater(true)
                    .WithRating(9),
                Noon,
                TestContext.Current.CancellationToken);
            await new WatchStateRepository(factory).SaveAsync(
                new WatchState
                {
                    Content = ContentKey.ForTitle(Movie),
                    Position = TimeSpan.FromMinutes(12),
                    ObservedDuration = TimeSpan.FromMinutes(96),
                    SourceMediaFileId = File1,
                    Status = WatchStatus.InProgress,
                    IsManualOverride = false,
                    StartedUtc = Noon,
                    UpdatedUtc = Noon,
                },
                TestContext.Current.CancellationToken);
            await new IntroMarkerRepository(factory).SaveAsync(
                new IntroMarker(
                    Guid.Parse("aa000000-0000-4000-8000-000000000005"),
                    Series,
                    MarkerKind.Intro,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(90),
                    MarkerOrigin.Manual,
                    Confidence: null,
                    UserCorrected: true),
                TestContext.Current.CancellationToken);

            await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO library_roots (id, normalized_path, kind, availability, scan_policy)
                VALUES ($rootId, $rootPath, 0, 0, 1);

                INSERT INTO media_files (
                    id, library_root_id, normalized_path, size_bytes, last_write_utc,
                    duration_ticks, container, video_codecs, audio_codecs, width, height, is_available)
                VALUES ($fileId, $rootId, $filePath, 1024, $stamp, NULL, 'mkv', '["h264"]', '["aac"]', 1920, 1080, 1);

                INSERT INTO match_candidates (
                    candidate_id, media_file_id, stable_key, content_kind, score,
                    scoring_model_version, review_state, signals_json, explanation_codes_json,
                    revision, decision_locked)
                VALUES ($candidateId, $fileId, 'movie:1', 0, 0.95, 1, 2, '{}', '[]', 1, 1);
                """;
            command.Parameters.AddWithValue("$rootId", Root.Value.ToString("D"));
            command.Parameters.AddWithValue("$rootPath", RootPath);
            command.Parameters.AddWithValue("$fileId", File1.Value.ToString("D"));
            command.Parameters.AddWithValue("$filePath", "D:\\media\\movie.mkv");
            command.Parameters.AddWithValue("$stamp", Noon.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue(
                "$candidateId",
                Guid.Parse("aa000000-0000-4000-8000-000000000006").ToString("D"));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private sealed class ImmediateProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    private sealed class TestAppDataPaths(string dataRoot) : IAppDataPaths
    {
        public string DataRoot { get; } = dataRoot;

        public string DatabasePath { get; } = Path.Combine(dataRoot, "library.db");

        public string SettingsPath { get; } = Path.Combine(dataRoot, "settings.json");

        public string BackupsDirectory { get; } = Path.Combine(dataRoot, "backups");

        public string PersonalArtworkDirectory { get; } = Path.Combine(dataRoot, "personal-artwork");

        public string RemoteCacheDirectory { get; } = Path.Combine(dataRoot, "cache", "artwork");

        public string DiagnosticsDirectory { get; } = Path.Combine(dataRoot, "diagnostics");

        // Never the key Windows reads at sign-in: a suite leaves nothing behind there.
        public string StartupRegistrySubKey { get; } =
            @"Software\APSolutions\LocalMedia\Tests\Run";

        // And nothing this suite drives may reach the operating system either, so the handover it
        // would otherwise make lands under its own root.
        public string? SystemHandoffDirectory { get; } = Path.Combine(dataRoot, "handoff");
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
