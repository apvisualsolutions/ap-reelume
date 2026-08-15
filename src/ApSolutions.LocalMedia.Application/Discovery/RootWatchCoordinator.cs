// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Threading.Channels;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed class RootWatchCoordinator
{
    private readonly ConcurrentDictionary<LibraryRootId, SemaphoreSlim> _rootLocks = new();
    private readonly IRootWatcher _rootWatcher;
    private readonly IFallbackScanScheduler _fallbackScanScheduler;
    private readonly IScanCoordinator _scanCoordinator;

    public RootWatchCoordinator(
        IRootWatcher rootWatcher,
        IFallbackScanScheduler fallbackScanScheduler,
        IScanCoordinator scanCoordinator)
    {
        _rootWatcher = rootWatcher ?? throw new ArgumentNullException(nameof(rootWatcher));
        _fallbackScanScheduler = fallbackScanScheduler ??
            throw new ArgumentNullException(nameof(fallbackScanScheduler));
        _scanCoordinator = scanCoordinator ?? throw new ArgumentNullException(nameof(scanCoordinator));
    }

    public async Task StartAsync(
        LibraryRoot root,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);

        // A watcher that died is watched again on the next recovery pass: the fallback schedule is
        // the heartbeat this slice already has, so a lost watcher costs at most one interval of
        // live watching instead of costing it until the application is started again.
        var retries = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = true,
        });

        // The live file watcher is what Continuous means; a root whose owner chose Manual or
        // Startup is not followed behind their back. The fallback scheduler reads the policy on
        // its own, so it always gets the root.
        var watcher = root.ScanPolicy.HasFlag(ScanPolicy.Continuous)
            ? ProcessWatcherAsync(root, retries.Reader, cancellationToken)
            : Task.CompletedTask;
        await Task.WhenAll(
                watcher,
                ProcessFallbackScheduleAsync(root, retries.Writer, cancellationToken))
            .ConfigureAwait(false);
    }

    public Task<ScanSummary> RunFallbackAsync(
        LibraryRoot root,
        ScanTrigger trigger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (trigger is not (ScanTrigger.Startup or ScanTrigger.Manual or ScanTrigger.Recovery))
        {
            throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "The trigger is not a fallback scan.");
        }

        return RunScanAsync(root.Id, trigger, cancellationToken);
    }

    private async Task ProcessWatcherAsync(
        LibraryRoot root,
        ChannelReader<byte> retries,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await foreach (var batch in _rootWatcher
                                   .StartAsync(root, cancellationToken)
                                   .WithCancellation(cancellationToken)
                                   .ConfigureAwait(false))
                {
                    if (batch.RootId != root.Id)
                    {
                        throw new InvalidDataException("The watcher returned a batch for a different root.");
                    }

                    // Lost events are exactly what a full pass over the root recovers, and it covers
                    // whatever else came in the same batch, so it replaces the incremental scan.
                    if (batch.EventsLost)
                    {
                        await RunScanAsync(root.Id, ScanTrigger.Recovery, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else if (batch.Changes.Count > 0)
                    {
                        await RunScanAsync(root.Id, ScanTrigger.Watcher, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                // The stream ran dry on its own: there is nothing left to watch, so nothing to
                // start again either.
                return;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                await RunScanAsync(root.Id, ScanTrigger.Recovery, cancellationToken).ConfigureAwait(false);
            }

            // The watcher died. Waiting for the next fallback pass is what keeps a broken root from
            // being retried in a hot loop; when no pass is coming, there is nothing to wait for.
            if (!await retries.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            while (retries.TryRead(out _))
            {
            }
        }
    }

    private async Task ProcessFallbackScheduleAsync(
        LibraryRoot root,
        ChannelWriter<byte> retries,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var trigger in _fallbackScanScheduler
                               .ScheduleAsync(root, cancellationToken)
                               .WithCancellation(cancellationToken)
                               .ConfigureAwait(false))
            {
                await RunFallbackAsync(root, trigger, cancellationToken).ConfigureAwait(false);
                _ = retries.TryWrite(0);
            }
        }
        finally
        {
            // No more passes are coming, so a watcher waiting for one is released rather than left
            // waiting for a heartbeat that stopped.
            _ = retries.TryComplete();
        }
    }

    private async Task<ScanSummary> RunScanAsync(
        LibraryRootId rootId,
        ScanTrigger trigger,
        CancellationToken cancellationToken)
    {
        var rootLock = _rootLocks.GetOrAdd(rootId, static _ => new SemaphoreSlim(1, 1));
        await rootLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _scanCoordinator
                .StartAsync(new StartScanCommand(rootId, trigger), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            rootLock.Release();
        }
    }
}
