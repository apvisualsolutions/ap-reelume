// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
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

        // BUG-012: the buffer the operating system fills defaults to 8 KiB, and a folder receiving
        // a season at once overflows it. This is the ceiling the platform allows.
        Assert.Equal(64 * 1024, watcher.GetField("InternalBufferBytes")?.GetValue(null));
        Assert.NotNull(Assembly.Load("ApSolutions.LocalMedia.Domain").GetType(
            "ApSolutions.LocalMedia.Domain.Discovery.WatchErrorPolicy",
            throwOnError: false));
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

        // BUG-012: a storm this size overflowed the system buffer on a hosted runner, and an
        // overflow used to end the watcher — the batches simply stopped, and the folder was no
        // longer followed. What is asserted is the half the defect ate: a file created after the
        // storm still arrives.
        pendingBatch = batches.MoveNextAsync().AsTask();
        var afterTheStorm = Path.Combine(directory.Path, "after-the-storm.mkv");
        await File.WriteAllBytesAsync(afterTheStorm, [0x41], timeout.Token);
        FileChange? survivor = null;
        while (survivor is null && await pendingBatch)
        {
            survivor = batches.Current.Changes.FirstOrDefault(change =>
                string.Equals(change.Path, afterTheStorm, StringComparison.OrdinalIgnoreCase));
            if (survivor is null)
            {
                pendingBatch = batches.MoveNextAsync().AsTask();
            }
        }

        Assert.NotNull(survivor);
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
    public void Watcher_refuses_a_clock_a_debounce_or_a_buffer_it_cannot_honour()
    {
        Assert.Throws<ArgumentNullException>(() => new DebouncedFileWatcher(null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DebouncedFileWatcher(new SystemClock(), TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DebouncedFileWatcher(
            new SystemClock(),
            internalBufferBytes: DebouncedFileWatcher.MinimumInternalBufferBytes - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DebouncedFileWatcher(
            new SystemClock(),
            internalBufferBytes: DebouncedFileWatcher.InternalBufferBytes + 1));
    }

    [Fact]
    public async Task Overflowing_the_system_buffer_is_reported_as_lost_events_and_the_watching_goes_on()
    {
        using var directory = new DatabaseTestDirectory();
        var stormRoot = Path.Combine(directory.Path, "overflow");
        Directory.CreateDirectory(stormRoot);
        var root = Root(stormRoot, RootKind.Local);

        // BUG-012 handler only runs when Windows drops change records, and at the product ceiling
        // of 64 KiB a storm overflows on some runs and not on others: this file coverage swung
        // between 88.54/73.81 and 93.75/71.43 across two runs of the same binary. So the buffer is
        // a constructor parameter — the pattern the debounce already had — and this test asks for
        // the smallest one the platform honours. Each record costs twelve bytes plus the name in
        // UTF-16, so names this long fit about twenty of them in 4 KiB, and the storm below is
        // thousands.
        var watcher = new DebouncedFileWatcher(
            new SystemClock(),
            TimeSpan.FromMilliseconds(150),
            DebouncedFileWatcher.MinimumInternalBufferBytes);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        await using var batches = watcher.StartAsync(root, timeout.Token).GetAsyncEnumerator(timeout.Token);
        var pendingBatch = batches.MoveNextAsync().AsTask();
        await Task.Delay(100, timeout.Token);

        var longName = new string('o', 96);
        Parallel.For(
            0,
            4_000,
            new ParallelOptions { MaxDegreeOfParallelism = 64, CancellationToken = timeout.Token },
            index =>
            {
                // Concurrent, synchronous and empty on purpose: what overflows the buffer is
                // writing records faster than the thread of the watcher drains them. A sequential
                // loop hands it a third of a millisecond between files, which is all the time it
                // needs to keep up — that is precisely how the earlier storm failed to overflow.
                File.Create(Path.Combine(stormRoot, $"{longName}{index}.mkv")).Dispose();
            });

        var eventsLost = false;
        try
        {
            while (!eventsLost && await pendingBatch)
            {
                eventsLost = batches.Current.EventsLost;
                if (!eventsLost)
                {
                    pendingBatch = batches.MoveNextAsync().AsTask();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Falls through to the assertion, which says what went wrong far better than a
            // cancellation would.
        }

        // The assertion is the whole point. A test that provokes a condition it cannot guarantee
        // has to state that the condition happened, or it goes blind instead of red: the storm
        // that did not overflow passed just the same, without ever running what it protects.
        Assert.True(eventsLost, "The storm never overflowed the buffer, so nothing was proven.");

        // And an overflow is not the end of the watching: a file created afterwards still arrives.
        pendingBatch = batches.MoveNextAsync().AsTask();
        var afterTheStorm = Path.Combine(stormRoot, "after-the-storm.mkv");
        await File.WriteAllBytesAsync(afterTheStorm, [0x41], timeout.Token);
        FileChange? survivor = null;
        while (survivor is null && await pendingBatch)
        {
            survivor = batches.Current.Changes.FirstOrDefault(change =>
                string.Equals(change.Path, afterTheStorm, StringComparison.OrdinalIgnoreCase));
            if (survivor is null)
            {
                pendingBatch = batches.MoveNextAsync().AsTask();
            }
        }

        Assert.NotNull(survivor);
    }

    [Fact]
    public async Task A_change_arriving_exactly_as_the_debounce_ends_joins_the_batch()
    {
        using var directory = new DatabaseTestDirectory();
        var folder = Path.Combine(directory.Path, "racing-debounce");
        Directory.CreateDirectory(folder);
        var first = Path.Combine(folder, "first.mkv");
        var second = Path.Combine(folder, "second.mkv");

        // The last thing in this file whose coverage was left to chance. When a change arrives
        // while the debounce is still waiting, the watcher cancels the debounce and then awaits it
        // to let the cancellation through — and if the debounce had just elapsed, that await
        // returns normally instead. Against a real clock that is a race of microseconds, won on
        // some runs and not on others. This clock ends its wait when the watcher cancels it, so the
        // second half of that pair is exercised every time rather than now and then.
        var watcher = new DebouncedFileWatcher(
            new ClockWhoseWaitEndsWhenItIsCancelled(),
            TimeSpan.FromMilliseconds(300));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await using var batches = watcher
            .StartAsync(Root(folder, RootKind.Local), timeout.Token)
            .GetAsyncEnumerator(timeout.Token);
        var pendingBatch = batches.MoveNextAsync().AsTask();
        await Task.Delay(200, timeout.Token);

        await File.WriteAllBytesAsync(first, [0x41], timeout.Token);
        await Task.Delay(50, timeout.Token);
        await File.WriteAllBytesAsync(second, [0x50], timeout.Token);

        // What the watcher promises either way: a debounce that ends early takes the change that
        // ended it with it, instead of leaving it for a batch that may never come.
        FileChange? late = null;
        while (late is null && await pendingBatch)
        {
            late = batches.Current.Changes.FirstOrDefault(change =>
                string.Equals(change.Path, second, StringComparison.OrdinalIgnoreCase));
            if (late is null)
            {
                pendingBatch = batches.MoveNextAsync().AsTask();
            }
        }

        Assert.NotNull(late);
    }

    [Fact]
    public async Task A_root_that_disappears_ends_the_watching_with_the_reason_rather_than_in_silence()
    {
        using var directory = new DatabaseTestDirectory();
        var vanishing = Path.Combine(directory.Path, "vanishing");
        Directory.CreateDirectory(vanishing);
        var watcher = new DebouncedFileWatcher(new SystemClock(), TimeSpan.FromMilliseconds(150));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await using var batches = watcher
            .StartAsync(Root(vanishing, RootKind.Local), timeout.Token)
            .GetAsyncEnumerator(timeout.Token);
        var pending = batches.MoveNextAsync().AsTask();
        await Task.Delay(200, timeout.Token);

        Directory.Delete(vanishing, recursive: true);

        // The other half of the same decision, and the half that must never be mistaken for an
        // overflow: a root that stopped answering is the end of this watcher, so the reader is
        // told why instead of waiting forever for batches that will not come.
        var error = await Assert.ThrowsAnyAsync<Exception>(async () => await pending);
        Assert.False(
            error is OperationCanceledException,
            "The watching was cancelled rather than ended by the vanished root.");
        Assert.False(WatchErrorPolicy.MeansEventsWereLost(error));
    }

    [Fact]
    public async Task Changes_to_one_path_coalesce_to_the_change_that_survives()
    {
        using var directory = new DatabaseTestDirectory();
        var folder = Path.Combine(directory.Path, "coalescing");
        Directory.CreateDirectory(folder);
        var alreadyThere = Path.Combine(folder, "already-there.mkv");
        await File.WriteAllBytesAsync(alreadyThere, [0x41], TestContext.Current.CancellationToken);
        var renamedExisting = Path.Combine(folder, "renamed-existing.mkv");
        var created = Path.Combine(folder, "created.mkv");
        var renamed = Path.Combine(folder, "renamed.mkv");
        var recreated = Path.Combine(folder, "recreated.mkv");

        // Three seconds of debounce so every step below lands in one batch, and the batch closes
        // three seconds after the last of them. Coalescing is what the batch is for, and it can
        // only be measured when the changes meet: read one change per batch and the pairs this
        // asserts never form. Which pairs formed used to be left to whatever the operating system
        // happened to deliver during a storm, and it delivered differently on each run.
        var watcher = new DebouncedFileWatcher(new SystemClock(), TimeSpan.FromSeconds(3));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        await using var batches = watcher
            .StartAsync(Root(folder, RootKind.Local), timeout.Token)
            .GetAsyncEnumerator(timeout.Token);
        var pendingBatch = batches.MoveNextAsync().AsTask();
        await Task.Delay(200, timeout.Token);

        await File.WriteAllBytesAsync(created, [0x41], timeout.Token);
        await Task.Delay(80, timeout.Token);
        await File.AppendAllTextAsync(created, "x", timeout.Token);
        await Task.Delay(80, timeout.Token);
        File.Move(created, renamed);
        await Task.Delay(80, timeout.Token);
        File.Delete(renamed);
        await Task.Delay(80, timeout.Token);
        await File.WriteAllBytesAsync(recreated, [0x41], timeout.Token);
        await Task.Delay(80, timeout.Token);
        File.Delete(recreated);
        await Task.Delay(80, timeout.Token);
        await File.WriteAllBytesAsync(recreated, [0x41], timeout.Token);
        await Task.Delay(80, timeout.Token);
        File.Move(alreadyThere, renamedExisting);

        Assert.True(await pendingBatch);
        var batch = batches.Current.Changes;

        // Created then changed then renamed then deleted, all on the same file: what survives is
        // one deletion, at the name the file had when it went.
        Assert.Equal(FileChangeKind.Deleted, Single(batch, renamed).Kind);

        // The rename took the old path with it, so nothing is left claiming a file that is gone.
        Assert.DoesNotContain(
            batch,
            change => string.Equals(change.Path, created, StringComparison.OrdinalIgnoreCase));

        // Deleted and created again is a file that changed, not one that appeared.
        Assert.Equal(FileChangeKind.Changed, Single(batch, recreated).Kind);

        // And a rename whose old path was never pending — the file was there before the watching
        // began — is carried through as the rename it is.
        Assert.Equal(FileChangeKind.Renamed, Single(batch, renamedExisting).Kind);
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

    private static FileChange Single(IReadOnlyList<FileChange> changes, string path) =>
        Assert.Single(
            changes,
            change => string.Equals(change.Path, path, StringComparison.OrdinalIgnoreCase));

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

    /// <summary>
    /// A clock whose wait ends — successfully — the moment it is cancelled, instead of throwing.
    /// It models the one ordering a real clock only reaches by coincidence: the debounce elapsing
    /// in the same instant the watcher gives up on it.
    /// </summary>
    private sealed class ClockWhoseWaitEndsWhenItIsCancelled : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => cancelled.TrySetResult());
            await Task.WhenAny(cancelled.Task, Task.Delay(delay, CancellationToken.None));
        }
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
