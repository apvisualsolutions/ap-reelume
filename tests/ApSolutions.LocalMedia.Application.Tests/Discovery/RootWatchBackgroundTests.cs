// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Discovery;

/// <summary>
/// The caller the watching slice was missing (LIB-002/003): who starts the watchers with the
/// application, who adds a freshly scanned root, and who stops everything on the way out.
/// </summary>
public sealed class RootWatchBackgroundTests
{
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Start_hands_every_known_root_to_the_coordinator()
    {
        var first = CreateRoot(ScanPolicy.Startup);
        var second = CreateRoot(ScanPolicy.Startup);
        var scans = new SignallingScanCoordinator(expectedScans: 2);
        using var background = CreateBackground([first, second], scans, new IdleWatcher());

        background.Start();

        await scans.WaitAsync(WaitBudget);
        Assert.Equal(
            new[] { first.Id, second.Id }.OrderBy(id => id.Value).ToArray(),
            scans.Commands.Select(command => command.RootId).OrderBy(id => id.Value).ToArray());
        Assert.All(scans.Commands, command => Assert.Equal(ScanTrigger.Startup, command.Trigger));
    }

    [Fact]
    public async Task A_root_is_watched_once_no_matter_how_many_scans_ask()
    {
        var root = CreateRoot(ScanPolicy.Continuous);
        var watcher = new IdleWatcher();
        using var background = CreateBackground([root], new SignallingScanCoordinator(0), watcher);

        background.EnsureWatching(root.Id);
        await watcher.WaitForStartAsync(WaitBudget);
        background.EnsureWatching(root.Id);
        await Task.Delay(250, TestContext.Current.CancellationToken);

        Assert.Equal(1, watcher.Starts);
    }

    [Fact]
    public async Task Stopping_refuses_new_watches_and_cancels_the_running_ones()
    {
        var root = CreateRoot(ScanPolicy.Continuous);
        var watcher = new IdleWatcher();
        using var background = CreateBackground([root], new SignallingScanCoordinator(0), watcher);
        background.EnsureWatching(root.Id);
        await watcher.WaitForStartAsync(WaitBudget);

        background.Stop();

        await watcher.WaitForCancellationAsync(WaitBudget);
        background.EnsureWatching(root.Id);
        await Task.Delay(250, TestContext.Current.CancellationToken);
        Assert.Equal(1, watcher.Starts);
    }

    /// <summary>
    /// Measured on 2026-08-15, and it is why reviving a dead watcher is not this class's job: a
    /// continuous root stays in the watching set for as long as the application runs, because the
    /// fallback schedule it also owns never ends. <c>EnsureWatching</c> after a manual scan cannot
    /// bring the live watcher back.
    /// </summary>
    [Fact]
    public async Task A_continuous_root_is_watched_once_for_the_life_of_the_application()
    {
        var root = CreateRoot(ScanPolicy.Continuous);
        using var watcher = new DyingWatcher();
        var scans = new SignallingScanCoordinator(expectedScans: 1);
        using var background = new RootWatchBackground(
            new FixedRootRepository([root]),
            new RootWatchCoordinator(watcher, new EndlessScheduler(), scans));

        background.Start();
        await watcher.WaitForStartAsync(WaitBudget);
        await scans.WaitAsync(WaitBudget);
        background.EnsureWatching(root.Id);

        await Task.Delay(250, TestContext.Current.CancellationToken);
        Assert.Equal(ScanTrigger.Recovery, Assert.Single(scans.Commands).Trigger);
        Assert.Equal(1, watcher.Starts);
    }

    private static RootWatchBackground CreateBackground(
        IReadOnlyList<LibraryRoot> roots,
        IScanCoordinator scans,
        IRootWatcher watcher) => new(
            new FixedRootRepository(roots),
            new RootWatchCoordinator(watcher, new StartupOnlyScheduler(), scans));

    private static LibraryRoot CreateRoot(ScanPolicy policy) => new(
        new LibraryRootId(Guid.NewGuid()),
        @"C:\Media",
        RootKind.Local,
        RootAvailability.Available,
        policy);

    private sealed class FixedRootRepository(IReadOnlyList<LibraryRoot> roots) : ILibraryRootRepository
    {
        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(roots);

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(roots.FirstOrDefault(root => root.Id == id));

        public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetAvailabilityAsync(
            LibraryRootId id,
            RootAvailability availability,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>A watcher that starts, says so, and idles until it is cancelled.</summary>
    private sealed class IdleWatcher : IRootWatcher, IDisposable
    {
        private readonly SemaphoreSlim _started = new(0);
        private readonly SemaphoreSlim _cancelled = new(0);
        private int _starts;

        public int Starts => Volatile.Read(ref _starts);

        public async IAsyncEnumerable<FileChangeBatch> StartAsync(
            LibraryRoot root,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = Interlocked.Increment(ref _starts);
            _started.Release();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _cancelled.Release();
                throw;
            }

            yield break;
        }

        public async Task WaitForStartAsync(TimeSpan timeout) =>
            Assert.True(await _started.WaitAsync(timeout), "The watcher was never started.");

        public async Task WaitForCancellationAsync(TimeSpan timeout) =>
            Assert.True(await _cancelled.WaitAsync(timeout), "The watcher was never cancelled.");

        public void Dispose()
        {
            _started.Dispose();
            _cancelled.Dispose();
        }
    }

    /// <summary>A watcher whose first life ends the way an unreliable root ends it.</summary>
    private sealed class DyingWatcher : IRootWatcher, IDisposable
    {
        private readonly SemaphoreSlim _started = new(0);
        private int _starts;

        public int Starts => Volatile.Read(ref _starts);

        public async IAsyncEnumerable<FileChangeBatch> StartAsync(
            LibraryRoot root,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = root;
            var start = Interlocked.Increment(ref _starts);
            _started.Release();
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if (start == 1)
            {
                throw new IOException("The root stopped answering.");
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            yield break;
        }

        public async Task WaitForStartAsync(TimeSpan timeout) =>
            Assert.True(await _started.WaitAsync(timeout), "The watcher was never started.");

        public void Dispose() => _started.Dispose();
    }

    /// <summary>
    /// The fallback scheduler of a continuous root between recovery passes: alive, and quiet for the
    /// next fifteen minutes. What the assembled application looks like right after startup.
    /// </summary>
    private sealed class EndlessScheduler : IFallbackScanScheduler
    {
        public async IAsyncEnumerable<ScanTrigger> ScheduleAsync(
            LibraryRoot root,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = root;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            yield break;
        }
    }

    private sealed class StartupOnlyScheduler : IFallbackScanScheduler
    {
        public async IAsyncEnumerable<ScanTrigger> ScheduleAsync(
            LibraryRoot root,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            if (root.ScanPolicy.HasFlag(ScanPolicy.Startup))
            {
                yield return ScanTrigger.Startup;
            }
        }
    }

    private sealed class SignallingScanCoordinator(int expectedScans) : IScanCoordinator
    {
        private readonly TaskCompletionSource _done =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<StartScanCommand> _commands = [];

        public IReadOnlyList<StartScanCommand> Commands
        {
            get
            {
                lock (_commands)
                {
                    return [.. _commands];
                }
            }
        }

        public Task<ScanSummary> StartAsync(
            StartScanCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count;
            lock (_commands)
            {
                _commands.Add(command);
                count = _commands.Count;
            }

            if (count >= expectedScans)
            {
                _done.TrySetResult();
            }

            return Task.FromResult(new ScanSummary(
                command.RootId,
                0,
                0,
                0,
                0,
                0,
                IsCancelled: false,
                ResumeAfterPath: null,
                Results: [],
                TimeSpan.Zero));
        }

        public async Task WaitAsync(TimeSpan timeout)
        {
            var completed = await Task.WhenAny(_done.Task, Task.Delay(timeout));
            Assert.True(completed == _done.Task, "The expected scans never arrived.");
        }
    }
}
