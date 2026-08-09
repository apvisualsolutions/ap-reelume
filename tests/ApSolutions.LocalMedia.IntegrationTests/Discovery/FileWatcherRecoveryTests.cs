using System.Reflection;
using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.Infrastructure.Time;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Discovery;

public sealed class FileWatcherRecoveryTests
{
    [Fact]
    public void Watch_slice_owns_debounced_watcher_fallback_clock_and_settings_UI()
    {
        var infrastructure = Assembly.Load("ApSolutions.LocalMedia.Infrastructure");
        var presentation = Assembly.Load("ApSolutions.LocalMedia.Presentation");

        var watcher = infrastructure.GetType(
            "ApSolutions.LocalMedia.Infrastructure.FileSystem.DebouncedFileWatcher",
            throwOnError: false);
        Assert.NotNull(watcher);
        Assert.Equal(
            TimeSpan.FromMilliseconds(750),
            watcher.GetField("DefaultDebounce")?.GetValue(null));
        Assert.NotNull(infrastructure.GetType(
            "ApSolutions.LocalMedia.Infrastructure.FileSystem.FallbackScanScheduler",
            throwOnError: false));
        Assert.NotNull(infrastructure.GetType(
            "ApSolutions.LocalMedia.Infrastructure.Time.SystemClock",
            throwOnError: false));
        Assert.NotNull(presentation.GetType(
            "ApSolutions.LocalMedia.Presentation.Settings.ScanSettingsViewModel",
            throwOnError: false));
        Assert.NotNull(presentation.GetType(
            "ApSolutions.LocalMedia.Presentation.Settings.ScanSettingsView",
            throwOnError: false));
    }

    [Fact]
    public async Task Local_file_appears_in_a_debounced_batch_within_five_seconds()
    {
        using var directory = new DatabaseTestDirectory();
        var root = Root(directory.Path, RootKind.Local);
        var watcher = new DebouncedFileWatcher(new SystemClock());
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await using var batches = watcher.StartAsync(root, timeout.Token).GetAsyncEnumerator(timeout.Token);
        var pendingBatch = batches.MoveNextAsync().AsTask();
        await Task.Delay(100, timeout.Token);
        var mediaPath = Path.Combine(directory.Path, "new.mkv");

        await File.WriteAllBytesAsync(mediaPath, [0x41, 0x50], timeout.Token);
        var started = DateTimeOffset.UtcNow;
        Assert.True(await pendingBatch);

        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(5));
        Assert.Contains(
            batches.Current.Changes,
            change => string.Equals(change.Path, mediaPath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_change_rename_delete_storm_is_coalesced_by_final_path()
    {
        using var directory = new DatabaseTestDirectory();
        var root = Root(directory.Path, RootKind.Local);
        var watcher = new DebouncedFileWatcher(new SystemClock(), TimeSpan.FromMilliseconds(150));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await using var batches = watcher.StartAsync(root, timeout.Token).GetAsyncEnumerator(timeout.Token);
        var pendingBatch = batches.MoveNextAsync().AsTask();
        await Task.Delay(100, timeout.Token);
        var originalPath = Path.Combine(directory.Path, "storm.mkv");
        var renamedPath = Path.Combine(directory.Path, "renamed.mkv");

        await File.WriteAllBytesAsync(originalPath, [0x41], timeout.Token);
        for (var index = 0; index < 1_000; index++)
        {
            await File.AppendAllTextAsync(originalPath, "x", timeout.Token);
        }

        File.Move(originalPath, renamedPath);
        File.Delete(renamedPath);

        // A thousand appends take an unpredictable amount of time, so the rename and the delete can
        // land after the first debounce window has already closed. What the watcher promises is that
        // the storm coalesces by final path — never that the whole storm arrives in one batch — so
        // batches are read until the deletion appears. Reading only the first one made this pass or
        // fail depending on how busy the machine was.
        FileChange? deletion = null;
        while (deletion is null && await pendingBatch)
        {
            var changes = batches.Current.Changes;
            Assert.InRange(changes.Count, 1, 2);
            deletion = changes.FirstOrDefault(change =>
                change.Kind == FileChangeKind.Deleted &&
                string.Equals(change.Path, renamedPath, StringComparison.OrdinalIgnoreCase));
            if (deletion is null)
            {
                pendingBatch = batches.MoveNextAsync().AsTask();
            }
        }

        Assert.NotNull(deletion);
    }

    [Fact]
    public async Task Fallback_scheduler_runs_at_startup_then_stays_idle_until_the_interval()
    {
        using var directory = new DatabaseTestDirectory();
        var root = Root(directory.Path, RootKind.Usb);
        var scheduler = new FallbackScanScheduler(new SystemClock(), TimeSpan.FromMilliseconds(100));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await using var triggers = scheduler.ScheduleAsync(root, timeout.Token).GetAsyncEnumerator(timeout.Token);

        Assert.True(await triggers.MoveNextAsync());
        Assert.Equal(ScanTrigger.Startup, triggers.Current);
        var started = DateTimeOffset.UtcNow;
        Assert.True(await triggers.MoveNextAsync());

        Assert.Equal(ScanTrigger.Recovery, triggers.Current);
        Assert.True(DateTimeOffset.UtcNow - started >= TimeSpan.FromMilliseconds(75));
    }

    [Fact]
    public async Task Watcher_wait_is_cancelable_while_idle()
    {
        using var directory = new DatabaseTestDirectory();
        var watcher = new DebouncedFileWatcher(new SystemClock());
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var batches = watcher
            .StartAsync(Root(directory.Path, RootKind.Local), cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);
        var pending = batches.MoveNextAsync().AsTask();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task Unreliable_UNC_watcher_recovers_a_lost_event_and_skips_unchanged_probes()
    {
        using var directory = new DatabaseTestDirectory();
        var mediaPath = Path.Combine(directory.Path, "simulated-unc");
        Directory.CreateDirectory(mediaPath);
        var firstPath = Path.Combine(mediaPath, "first.mkv");
        var secondPath = Path.Combine(mediaPath, "missed.mkv");
        await File.WriteAllBytesAsync(firstPath, [0x41], TestContext.Current.CancellationToken);
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var root = Root(mediaPath, RootKind.Unc);
        var rootRepository = new LibraryRootRepository(factory);
        await rootRepository.AddAsync(root, TestContext.Current.CancellationToken);
        var probe = new CountingProbe();
        var scan = new RecordingScanCoordinator(new ScanCoordinator(
            rootRepository,
            new MediaFileRepository(factory),
            new MediaFileEnumerator(),
            probe,
            new NullPublisher()));
        await scan.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Initial),
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(secondPath, [0x50], TestContext.Current.CancellationToken);
        var inventoryBeforeRecovery = await ReadInventoryAsync(mediaPath);
        var watch = new RootWatchCoordinator(
            new ThrowingRootWatcher(),
            new EmptyFallbackScheduler(),
            scan);
        await watch.StartAsync(root, TestContext.Current.CancellationToken);
        await watch.RunFallbackAsync(root, ScanTrigger.Recovery, TestContext.Current.CancellationToken);

        Assert.Equal(2, await CountMediaAsync(factory));
        Assert.Equal(2, probe.Count);
        var recoveryScans = scan.Results
            .Where(result => result.Command.Trigger == ScanTrigger.Recovery)
            .ToArray();
        Assert.Equal(2, recoveryScans.Length);
        Assert.Equal(1, recoveryScans[0].Summary.ProbeCount);
        Assert.Equal(0, recoveryScans[1].Summary.ProbeCount);
        Assert.Equal(inventoryBeforeRecovery, await ReadInventoryAsync(mediaPath));
    }

    [Fact]
    public async Task Root_disconnect_and_reconnect_updates_availability_without_deleting_or_reprobing()
    {
        using var directory = new DatabaseTestDirectory();
        var mediaPath = Path.Combine(directory.Path, "disconnectable-root");
        Directory.CreateDirectory(mediaPath);
        var filePath = Path.Combine(mediaPath, "episode.mkv");
        await File.WriteAllBytesAsync(filePath, [0x41], TestContext.Current.CancellationToken);
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var root = Root(mediaPath, RootKind.Unc);
        var rootRepository = new LibraryRootRepository(factory);
        var mediaRepository = new MediaFileRepository(factory);
        await rootRepository.AddAsync(root, TestContext.Current.CancellationToken);
        var probe = new CountingProbe();
        var connected = new ScanCoordinator(
            rootRepository,
            mediaRepository,
            new MediaFileEnumerator(),
            probe,
            new NullPublisher());
        await connected.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Initial),
            TestContext.Current.CancellationToken);

        var disconnected = new ScanCoordinator(
            rootRepository,
            mediaRepository,
            new UnavailableRootEnumerator(),
            probe,
            new NullPublisher());
        await disconnected.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Recovery),
            TestContext.Current.CancellationToken);
        var unavailable = await mediaRepository.FindByPathAsync(
            root.Id,
            filePath,
            TestContext.Current.CancellationToken);

        Assert.False(unavailable?.IsAvailable);
        var recovered = await connected.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Recovery),
            TestContext.Current.CancellationToken);
        Assert.True((await mediaRepository.FindByPathAsync(
            root.Id,
            filePath,
            TestContext.Current.CancellationToken))?.IsAvailable);
        Assert.Equal(0, recovered.ProbeCount);
        Assert.Equal(1, probe.Count);
        Assert.Equal(1, await CountMediaAsync(factory));
    }

    private static LibraryRoot Root(string path, RootKind kind) => new(
        new LibraryRootId(Guid.NewGuid()),
        path,
        kind,
        RootAvailability.Available,
        ScanPolicy.Startup | ScanPolicy.Continuous);

    private static async Task<long> CountMediaAsync(SqliteConnectionFactory factory)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM media_files;";
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0L);
    }

    private static async Task<string[]> ReadInventoryAsync(string root)
    {
        var inventory = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root).Order(StringComparer.OrdinalIgnoreCase))
        {
            inventory.Add(await HashAsync(path));
        }

        return inventory.Order(StringComparer.Ordinal).ToArray();
    }

    private static async Task<string> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(
            stream,
            TestContext.Current.CancellationToken);
        return $"{Path.GetFileName(path)}|{Convert.ToHexString(hash)}";
    }

    private sealed class ThrowingRootWatcher : IRootWatcher
    {
        public async IAsyncEnumerable<FileChangeBatch> StartAsync(
            LibraryRoot root,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = root;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            throw new IOException("Simulated unreliable UNC watcher.");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class EmptyFallbackScheduler : IFallbackScanScheduler
    {
        public async IAsyncEnumerable<ScanTrigger> ScheduleAsync(
            LibraryRoot root,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = root;
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class UnavailableRootEnumerator : IMediaFileEnumerator
    {
        public async IAsyncEnumerable<IReadOnlyList<EnumeratedFile>> EnumerateBatchesAsync(
            LibraryRoot root,
            string? afterPath,
            int batchSize,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = afterPath;
            _ = batchSize;
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return
            [
                new EnumeratedFile(
                    root.Path,
                    0,
                    DateTimeOffset.UnixEpoch,
                    "IoError"),
            ];
        }
    }

    private sealed class RecordingScanCoordinator(IScanCoordinator inner) : IScanCoordinator
    {
        public List<RecordedScan> Results { get; } = [];

        public async Task<ScanSummary> StartAsync(
            StartScanCommand command,
            CancellationToken cancellationToken = default)
        {
            var summary = await inner.StartAsync(command, cancellationToken);
            Results.Add(new RecordedScan(command, summary));
            return summary;
        }
    }

    private sealed record RecordedScan(StartScanCommand Command, ScanSummary Summary);

    private sealed class CountingProbe : IMediaProbe
    {
        public int Count { get; private set; }

        public Task<TechnicalMetadata> ProbeAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            _ = path;
            cancellationToken.ThrowIfCancellationRequested();
            Count++;
            return Task.FromResult(new TechnicalMetadata(
                TimeSpan.FromMinutes(1),
                "fake",
                ["h264"],
                ["aac"],
                1920,
                1080));
        }
    }

    private sealed class NullPublisher : IApplicationEventPublisher
    {
        public Task PublishAsync<TEvent>(
            TEvent applicationEvent,
            CancellationToken cancellationToken = default)
            where TEvent : notnull
        {
            _ = applicationEvent;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
