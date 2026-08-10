// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Discovery;

public sealed class WatchCoordinatorTests
{
    [Fact]
    public void Watch_slice_owns_clock_batches_watcher_scheduler_and_coordinator_contracts()
    {
        var domain = Assembly.Load("ApSolutions.LocalMedia.Domain");
        var application = Assembly.Load("ApSolutions.LocalMedia.Application");

        Assert.NotNull(domain.GetType(
            "ApSolutions.LocalMedia.Domain.Common.IClock",
            throwOnError: false));
        Assert.NotNull(application.GetType(
            "ApSolutions.LocalMedia.Application.Discovery.FileChangeBatch",
            throwOnError: false));
        Assert.NotNull(application.GetType(
            "ApSolutions.LocalMedia.Application.Discovery.IRootWatcher",
            throwOnError: false));
        Assert.NotNull(application.GetType(
            "ApSolutions.LocalMedia.Application.Discovery.IFallbackScanScheduler",
            throwOnError: false));
        Assert.NotNull(application.GetType(
            "ApSolutions.LocalMedia.Application.Discovery.RootWatchCoordinator",
            throwOnError: false));
    }

    [Fact]
    public async Task Created_changed_renamed_and_deleted_batch_becomes_one_watcher_scan()
    {
        var root = CreateRoot(RootKind.Local);
        var batch = new FileChangeBatch(
            root.Id,
            [
                new FileChange(FileChangeKind.Created, @"C:\Media\one.mkv"),
                new FileChange(FileChangeKind.Changed, @"C:\Media\one.mkv"),
                new FileChange(FileChangeKind.Renamed, @"C:\Media\two.mkv", @"C:\Media\one.mkv"),
                new FileChange(FileChangeKind.Deleted, @"C:\Media\two.mkv"),
            ],
            DateTimeOffset.UnixEpoch);
        var scanner = new RecordingScanCoordinator();
        var coordinator = new RootWatchCoordinator(
            new FakeRootWatcher([batch]),
            new FakeFallbackScanScheduler([]),
            scanner);

        await coordinator.StartAsync(root, TestContext.Current.CancellationToken);

        var command = Assert.Single(scanner.Commands);
        Assert.Equal(root.Id, command.RootId);
        Assert.Equal(ScanTrigger.Watcher, command.Trigger);
    }

    [Fact]
    public async Task Storm_of_one_thousand_events_is_one_incremental_scan()
    {
        var root = CreateRoot(RootKind.Local);
        var changes = Enumerable.Range(0, 1_000)
            .Select(index => new FileChange(FileChangeKind.Changed, $@"C:\Media\item-{index:D4}.mkv"))
            .ToArray();
        var scanner = new RecordingScanCoordinator();
        var coordinator = new RootWatchCoordinator(
            new FakeRootWatcher([new FileChangeBatch(root.Id, changes, DateTimeOffset.UnixEpoch)]),
            new FakeFallbackScanScheduler([]),
            scanner);

        await coordinator.StartAsync(root, TestContext.Current.CancellationToken);

        Assert.Equal(ScanTrigger.Watcher, Assert.Single(scanner.Commands).Trigger);
    }

    [Fact]
    public async Task Startup_manual_and_recovery_fallbacks_use_the_scan_coordinator()
    {
        var root = CreateRoot(RootKind.Usb);
        var scanner = new RecordingScanCoordinator();
        var coordinator = new RootWatchCoordinator(
            new FakeRootWatcher([]),
            new FakeFallbackScanScheduler([ScanTrigger.Startup, ScanTrigger.Recovery]),
            scanner);

        await coordinator.StartAsync(root, TestContext.Current.CancellationToken);
        await coordinator.RunFallbackAsync(root, ScanTrigger.Manual, TestContext.Current.CancellationToken);

        Assert.Equal(
            [ScanTrigger.Startup, ScanTrigger.Manual, ScanTrigger.Recovery],
            scanner.Commands.Select(command => command.Trigger).Order().ToArray());
    }

    [Fact]
    public async Task Watch_and_fallback_work_never_exceed_one_active_scan_per_root()
    {
        var root = CreateRoot(RootKind.Local);
        var scanner = new RecordingScanCoordinator(TimeSpan.FromMilliseconds(50));
        var coordinator = new RootWatchCoordinator(
            new FakeRootWatcher([
                new FileChangeBatch(
                    root.Id,
                    [new FileChange(FileChangeKind.Created, @"C:\Media\new.mkv")],
                    DateTimeOffset.UnixEpoch),
            ]),
            new FakeFallbackScanScheduler([ScanTrigger.Startup]),
            scanner);

        await coordinator.StartAsync(root, TestContext.Current.CancellationToken);

        Assert.Equal(2, scanner.Commands.Count);
        Assert.Equal(1, scanner.MaximumConcurrency);
    }

    [Fact]
    public async Task Unreliable_UNC_watcher_degrades_to_a_recovery_scan()
    {
        var root = CreateRoot(RootKind.Unc);
        var scanner = new RecordingScanCoordinator();
        var coordinator = new RootWatchCoordinator(
            new FakeRootWatcher([], new IOException("UNC watcher unavailable")),
            new FakeFallbackScanScheduler([]),
            scanner);

        await coordinator.StartAsync(root, TestContext.Current.CancellationToken);

        Assert.Equal(ScanTrigger.Recovery, Assert.Single(scanner.Commands).Trigger);
    }

    [Fact]
    public async Task A_manual_root_is_not_watched_behind_its_owners_back()
    {
        var root = CreateRoot(RootKind.Local) with { ScanPolicy = ScanPolicy.Manual };
        var watcher = new CountingRootWatcher();
        var scanner = new RecordingScanCoordinator();
        var coordinator = new RootWatchCoordinator(
            watcher,
            new FakeFallbackScanScheduler([]),
            scanner);

        await coordinator.StartAsync(root, TestContext.Current.CancellationToken);

        // Continuous is a choice, and the live watcher is what it means: a root whose owner chose
        // Manual gets no watcher and no scan they did not ask for.
        Assert.Equal(0, watcher.Starts);
        Assert.Empty(scanner.Commands);
    }

    private sealed class CountingRootWatcher : IRootWatcher
    {
        public int Starts { get; private set; }

        public async IAsyncEnumerable<FileChangeBatch> StartAsync(
            LibraryRoot root,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = root;
            cancellationToken.ThrowIfCancellationRequested();
            Starts++;
            await Task.Yield();
            yield break;
        }
    }

    private static LibraryRoot CreateRoot(RootKind kind) => new(
        new LibraryRootId(Guid.NewGuid()),
        kind == RootKind.Unc ? @"\\nas\Media" : @"C:\Media",
        kind,
        RootAvailability.Available,
        ScanPolicy.Startup | ScanPolicy.Continuous);

    private sealed class FakeRootWatcher(
        IReadOnlyList<FileChangeBatch> batches,
        Exception? completionError = null) : IRootWatcher
    {
        public async IAsyncEnumerable<FileChangeBatch> StartAsync(
            LibraryRoot root,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = root;
            foreach (var batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return batch;
                await Task.Yield();
            }

            if (completionError is not null)
            {
                throw completionError;
            }
        }
    }

    private sealed class FakeFallbackScanScheduler(IReadOnlyList<ScanTrigger> triggers)
        : IFallbackScanScheduler
    {
        public async IAsyncEnumerable<ScanTrigger> ScheduleAsync(
            LibraryRoot root,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = root;
            foreach (var trigger in triggers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return trigger;
                await Task.Yield();
            }
        }
    }

    private sealed class RecordingScanCoordinator(TimeSpan? duration = null) : IScanCoordinator
    {
        private int _active;
        private int _maximumConcurrency;

        public List<StartScanCommand> Commands { get; } = [];

        public int MaximumConcurrency => _maximumConcurrency;

        public async Task<ScanSummary> StartAsync(
            StartScanCommand command,
            CancellationToken cancellationToken = default)
        {
            lock (Commands)
            {
                Commands.Add(command);
            }

            var active = Interlocked.Increment(ref _active);
            InterlockedExtensions.Max(ref _maximumConcurrency, active);
            try
            {
                if (duration is { } delay)
                {
                    await Task.Delay(delay, cancellationToken);
                }

                return new ScanSummary(
                    command.RootId,
                    0,
                    0,
                    0,
                    0,
                    0,
                    IsCancelled: false,
                    ResumeAfterPath: null,
                    Results: [],
                    MaxEventDispatchDuration: TimeSpan.Zero);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref target);
                if (current >= value)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
    }
}
