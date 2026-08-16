// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Backup;

/// <summary>
/// The order a copy is built in, what its manifest promises, and the three ways a run can end without
/// producing one: cancellation, no space, and a failing snapshot. None of them may publish.
/// </summary>
public sealed class BackupWorkflowTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("apsolutions-backup-workflow");

    public void Dispose()
    {
        _root.Delete(recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_copy_carries_a_versioned_manifest_that_hashes_everything_it_contains()
    {
        var paths = CreateDataRoot(artworkFiles: 2);
        var store = new FakeBackupStore(Path.Combine(_root.FullName, "copies"));
        var create = CreateBackupUnderTest(paths, store);

        var result = await create.ExecuteAsync(progress: null, TestContext.Current.CancellationToken);

        Assert.Equal(BackupManifest.CurrentFormatVersion, result.Manifest.FormatVersion);
        Assert.Equal("1.2.3", result.Manifest.AppVersion);
        Assert.Equal(Noon, result.Manifest.CreatedUtc);
        Assert.Equal(
            Sha256OfFile(Path.Combine(result.Copy.Path, BackupContentPolicy.DatabaseEntryName)),
            result.Manifest.DatabaseSha256);
        Assert.Equal(
            Sha256OfFile(Path.Combine(result.Copy.Path, BackupContentPolicy.PreferencesEntryName)),
            result.Manifest.PreferencesSha256);
        Assert.Equal(2, result.Manifest.PersonalArtwork.Count);
        foreach (var entry in result.Manifest.PersonalArtwork)
        {
            Assert.Equal(
                Sha256OfFile(Path.Combine(result.Copy.Path, entry.RelativePath)),
                entry.Sha256);
        }

        var root = Assert.Single(result.Manifest.Roots);
        Assert.Equal("D:\\media", root.Path);
        Assert.Equal(nameof(RootKind.Local), root.Kind);
    }

    [Fact]
    public async Task The_manifest_reaches_disk_next_to_the_files_it_describes()
    {
        var paths = CreateDataRoot(artworkFiles: 1);
        var store = new FakeBackupStore(Path.Combine(_root.FullName, "copies"));

        var result = await CreateBackupUnderTest(paths, store)
            .ExecuteAsync(progress: null, TestContext.Current.CancellationToken);

        var manifestPath = Path.Combine(result.Copy.Path, BackupManifest.FileName);
        Assert.True(File.Exists(manifestPath));
        Assert.True(File.Exists(Path.Combine(result.Copy.Path, BackupContentPolicy.DatabaseEntryName)));
        Assert.True(File.Exists(Path.Combine(result.Copy.Path, BackupContentPolicy.PreferencesEntryName)));
        Assert.Contains(
            "\"formatVersion\": 1",
            await File.ReadAllTextAsync(manifestPath, TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Progress_walks_the_stages_in_order_and_finishes_on_publish()
    {
        var paths = CreateDataRoot(artworkFiles: 3);
        var store = new FakeBackupStore(Path.Combine(_root.FullName, "copies"));
        var reported = new List<BackupProgress>();

        // Progress<T> queues its callbacks, so a loaded machine can reach the assertion before the
        // last stages arrive; reporting synchronously keeps the observed walk equal to the real one.
        await CreateBackupUnderTest(paths, store).ExecuteAsync(
            new ImmediateProgress<BackupProgress>(reported.Add),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(reported);
        Assert.Equal(
            [
                BackupStage.Snapshot,
                BackupStage.Preferences,
                BackupStage.PersonalArtwork,
                BackupStage.Manifest,
                BackupStage.Publish,
            ],
            reported.Select(item => item.Stage).Distinct());
        Assert.All(reported, item => Assert.InRange(item.Completed, 0, item.Total));
        Assert.Equal(reported[^1].Total, reported[^1].Completed);
    }

    [Fact]
    public async Task Retention_is_applied_through_the_store_after_the_copy_is_published()
    {
        var paths = CreateDataRoot(artworkFiles: 0);
        var store = new FakeBackupStore(Path.Combine(_root.FullName, "copies"));

        await CreateBackupUnderTest(paths, store).ExecuteAsync(
            progress: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(CreateBackup.DefaultRetention, store.RequestedRetention);
        Assert.Equal(["publish", "prune"], store.Calls.Where(call => call is "publish" or "prune"));
    }

    [Fact]
    public async Task A_cancelled_run_publishes_nothing_and_leaves_no_staging_behind()
    {
        var paths = CreateDataRoot(artworkFiles: 2);
        var store = new FakeBackupStore(Path.Combine(_root.FullName, "copies"));
        using var cancellation = new CancellationTokenSource();
        var create = CreateBackupUnderTest(paths, store, onSnapshot: cancellation.Cancel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => create.ExecuteAsync(progress: null, cancellation.Token));

        Assert.Empty(await store.ListAsync(TestContext.Current.CancellationToken));
        Assert.Contains("discard", store.Calls);
        Assert.Empty(Directory.GetDirectories(store.RootDirectory));
    }

    [Fact]
    public async Task A_volume_that_cannot_hold_the_copy_stops_the_run_before_anything_is_written()
    {
        var paths = CreateDataRoot(artworkFiles: 1);
        var store = new FakeBackupStore(Path.Combine(_root.FullName, "copies"))
        {
            AvailableBytes = 16,
        };
        var create = CreateBackupUnderTest(paths, store);

        var failure = await Assert.ThrowsAsync<InsufficientBackupSpaceException>(
            () => create.ExecuteAsync(progress: null, TestContext.Current.CancellationToken));

        Assert.True(failure.RequiredBytes > failure.AvailableBytes);
        Assert.Empty(await store.ListAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain("staging", store.Calls);
    }

    [Fact]
    public async Task Export_packs_the_same_payload_and_names_every_entry_it_wrote()
    {
        var paths = CreateDataRoot(artworkFiles: 2);
        var store = new FakeBackupStore(Path.Combine(_root.FullName, "copies"));
        var archives = new FakeArchiveWriter();
        var destination = Path.Combine(_root.FullName, "export.zip");

        var result = await new ExportLibrary(CreateBackupUnderTest(paths, store), store, archives)
            .ExecuteAsync(destination, progress: null, TestContext.Current.CancellationToken);

        Assert.Equal(destination, result.ArchivePath);
        Assert.Equal(BackupManifest.CurrentFormatVersion, result.Manifest.FormatVersion);
        Assert.Contains(BackupContentPolicy.DatabaseEntryName, result.Entries);
        Assert.Contains(BackupContentPolicy.PreferencesEntryName, result.Entries);
        Assert.Contains(BackupManifest.FileName, result.Entries);
        Assert.Equal(2, result.Entries.Count(entry => entry.StartsWith(
            BackupContentPolicy.PersonalArtworkDirectoryName,
            StringComparison.Ordinal)));
        Assert.All(result.Entries, entry => Assert.True(BackupContentPolicy.IsAllowed(entry)));
    }

    [Fact]
    public async Task An_export_never_becomes_a_rotating_copy()
    {
        var paths = CreateDataRoot(artworkFiles: 0);
        var store = new FakeBackupStore(Path.Combine(_root.FullName, "copies"));

        await new ExportLibrary(CreateBackupUnderTest(paths, store), store, new FakeArchiveWriter())
            .ExecuteAsync(
                Path.Combine(_root.FullName, "export.zip"),
                progress: null,
                TestContext.Current.CancellationToken);

        Assert.Empty(await store.ListAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain("publish", store.Calls);
        Assert.DoesNotContain("prune", store.Calls);
    }

    [Fact]
    public async Task A_cancelled_export_removes_the_half_written_archive()
    {
        var paths = CreateDataRoot(artworkFiles: 1);
        var store = new FakeBackupStore(Path.Combine(_root.FullName, "copies"));
        var destination = Path.Combine(_root.FullName, "export.zip");
        using var cancellation = new CancellationTokenSource();
        var archives = new FakeArchiveWriter
        {
            OnWrite = path =>
            {
                File.WriteAllText(path, "partial");
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ExportLibrary(CreateBackupUnderTest(paths, store), store, archives)
                .ExecuteAsync(destination, progress: null, cancellation.Token));

        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.GetDirectories(store.RootDirectory));
    }

    [Fact]
    public async Task A_data_folder_without_preferences_still_produces_a_copy_that_says_so()
    {
        var paths = CreateDataRoot(artworkFiles: 0);
        File.Delete(paths.SettingsPath);
        var store = new FakeBackupStore(Path.Combine(_root.FullName, "copies"));

        var result = await CreateBackupUnderTest(paths, store)
            .ExecuteAsync(progress: null, TestContext.Current.CancellationToken);

        Assert.Null(result.Manifest.PreferencesSha256);
        Assert.False(File.Exists(Path.Combine(result.Copy.Path, BackupContentPolicy.PreferencesEntryName)));
        Assert.True(File.Exists(Path.Combine(result.Copy.Path, BackupContentPolicy.DatabaseEntryName)));
    }

    [Theory]
    [InlineData("library.db", true)]
    [InlineData("settings.json", true)]
    [InlineData("manifest.json", true)]
    [InlineData("personal-artwork/0a/poster.jpg", true)]
    [InlineData("personal-artwork\\0a\\poster.png", true)]
    [InlineData("cache/artwork/0a/poster.jpg", false)]
    [InlineData("diagnostics/report.json", false)]
    [InlineData("library.db-wal", false)]
    [InlineData("library.db-shm", false)]
    [InlineData("tmdb.token", false)]
    [InlineData("personal-artwork/../settings.json", false)]
    [InlineData("../escape.json", false)]
    [InlineData("C:/absolute/library.db", false)]
    [InlineData("movie.mp4", false)]
    [InlineData("personal-artwork/clip.mkv", false)]
    [InlineData("   ", false)]
    [InlineData("/library.db", false)]
    [InlineData("personal-artwork/..", false)]
    public void The_allowlist_admits_the_payload_and_refuses_everything_else(string entry, bool allowed) =>
        Assert.Equal(allowed, BackupContentPolicy.IsAllowed(entry));

    private static string Sha256OfFile(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static CreateBackup CreateBackupUnderTest(
        FakeAppDataPaths paths,
        FakeBackupStore store,
        Action? onSnapshot = null) =>
        new(
            new FakeSnapshotWriter(paths.DatabasePath, onSnapshot),
            store,
            paths,
            new FakeRootRepository(),
            new FixedClock(Noon),
            appVersion: "1.2.3");

    private FakeAppDataPaths CreateDataRoot(int artworkFiles)
    {
        var dataRoot = Path.Combine(_root.FullName, "data");
        var paths = new FakeAppDataPaths(dataRoot);
        Directory.CreateDirectory(dataRoot);
        File.WriteAllText(paths.DatabasePath, "database contents");
        File.WriteAllText(paths.SettingsPath, "{\"Theme\":\"Dark\"}");
        File.WriteAllText(paths.DatabasePath + "-wal", "write ahead log");
        Directory.CreateDirectory(paths.RemoteCacheDirectory);
        File.WriteAllText(Path.Combine(paths.RemoteCacheDirectory, "remote.jpg"), "remote artwork");
        Directory.CreateDirectory(paths.DiagnosticsDirectory);
        File.WriteAllText(Path.Combine(paths.DiagnosticsDirectory, "report.json"), "{}");
        for (var index = 0; index < artworkFiles; index++)
        {
            var folder = Path.Combine(paths.PersonalArtworkDirectory, $"title{index}");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "poster.jpg"), $"personal artwork {index}");
        }

        return paths;
    }

    private sealed class FakeAppDataPaths(string dataRoot) : IAppDataPaths
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
    }

    private sealed class FakeSnapshotWriter(string source, Action? onSnapshot) : IBackupSnapshotWriter
    {
        public long EstimateBytes() => new FileInfo(source).Length;

        public async Task<BackupFileEntry> WriteAsync(
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            onSnapshot?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = await File.ReadAllBytesAsync(source, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(destinationPath, bytes, cancellationToken).ConfigureAwait(false);
            return new BackupFileEntry(
                BackupContentPolicy.DatabaseEntryName,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                bytes.Length);
        }
    }

    private sealed class FakeBackupStore(string rootDirectory) : IBackupStore
    {
        private readonly List<BackupCopy> _copies = [];

        public string RootDirectory { get; } = rootDirectory;

        public List<string> Calls { get; } = [];

        public int RequestedRetention { get; private set; }

        public long AvailableBytes { get; set; } = long.MaxValue;

        public Task<IReadOnlyList<BackupCopy>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BackupCopy>>([.. _copies]);

        public Task<string> CreateStagingAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("staging");
            var staging = Path.Combine(RootDirectory, $"staging-{Calls.Count}");
            Directory.CreateDirectory(staging);
            return Task.FromResult(staging);
        }

        public void DiscardStaging(string stagingDirectory)
        {
            Calls.Add("discard");
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }

        public Task<BackupCopy> PublishAsync(
            string stagingDirectory,
            DateTimeOffset createdUtc,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("publish");
            var published = Path.Combine(RootDirectory, $"copy-{_copies.Count + 1}");
            Directory.Move(stagingDirectory, published);
            var copy = new BackupCopy(published, createdUtc, IsValid: true);
            _copies.Add(copy);
            return Task.FromResult(copy);
        }

        public Task<IReadOnlyList<BackupCopy>> PruneAsync(
            int retention,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("prune");
            RequestedRetention = retention;
            return Task.FromResult<IReadOnlyList<BackupCopy>>([]);
        }

        public void EnsureSpace(long requiredBytes)
        {
            Calls.Add("space");
            if (requiredBytes > AvailableBytes)
            {
                throw new InsufficientBackupSpaceException(requiredBytes, AvailableBytes);
            }
        }
    }

    private sealed class FakeArchiveWriter : IBackupArchiveWriter
    {
        public Action<string>? OnWrite { get; set; }

        public Task<IReadOnlyList<string>> WriteAsync(
            string archivePath,
            string sourceDirectory,
            IProgress<BackupProgress>? progress,
            CancellationToken cancellationToken = default)
        {
            OnWrite?.Invoke(archivePath);
            cancellationToken.ThrowIfCancellationRequested();
            var entries = Directory
                .EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToArray();
            File.WriteAllText(archivePath, string.Join('\n', entries));
            return Task.FromResult<IReadOnlyList<string>>(entries);
        }
    }

    private sealed class FakeRootRepository : ILibraryRootRepository
    {
        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibraryRoot>>(
            [
                new LibraryRoot(
                    new LibraryRootId(Guid.Parse("f1000000-0000-4000-8000-000000000001")),
                    "D:\\media",
                    RootKind.Local,
                    RootAvailability.Available,
                    ScanPolicy.Manual),
            ]);

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<LibraryRoot?>(null);

        public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ImmediateProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
