using System.Collections.Concurrent;
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

        // The live file watcher is what Continuous means; a root whose owner chose Manual or
        // Startup is not followed behind their back. The fallback scheduler reads the policy on
        // its own, so it always gets the root.
        var watcher = root.ScanPolicy.HasFlag(ScanPolicy.Continuous)
            ? ProcessWatcherAsync(root, cancellationToken)
            : Task.CompletedTask;
        await Task.WhenAll(watcher, ProcessFallbackScheduleAsync(root, cancellationToken))
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

    private async Task ProcessWatcherAsync(LibraryRoot root, CancellationToken cancellationToken)
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

                if (batch.Changes.Count > 0)
                {
                    await RunScanAsync(root.Id, ScanTrigger.Watcher, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            await RunScanAsync(root.Id, ScanTrigger.Recovery, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessFallbackScheduleAsync(
        LibraryRoot root,
        CancellationToken cancellationToken)
    {
        await foreach (var trigger in _fallbackScanScheduler
                           .ScheduleAsync(root, cancellationToken)
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            await RunFallbackAsync(root, trigger, cancellationToken).ConfigureAwait(false);
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
