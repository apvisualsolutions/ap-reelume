using System.Collections.Concurrent;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

/// <summary>
/// Owns the life of the watchers: every root is handed to <see cref="RootWatchCoordinator"/> when
/// the application appears, a freshly scanned root joins without waiting for the next launch, and
/// leaving the application stops everything still running. The deep audit found the whole watching
/// slice registered and never started (LIB-002/003) — this is the caller it was missing.
/// </summary>
public sealed class RootWatchBackground : IDisposable
{
    private readonly ILibraryRootRepository _roots;
    private readonly RootWatchCoordinator _coordinator;
    private readonly ConcurrentDictionary<LibraryRootId, byte> _watching = new();
    private readonly CancellationTokenSource _shutdown = new();

    public RootWatchBackground(ILibraryRootRepository roots, RootWatchCoordinator coordinator)
    {
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    /// <summary>Hands every known root to the coordinator, off the caller's thread.</summary>
    public void Start() => _ = RunQuietlyAsync(async () =>
    {
        foreach (var root in await _roots.ListAsync(_shutdown.Token).ConfigureAwait(false))
        {
            Watch(root);
        }
    });

    /// <summary>
    /// Starts watching one root if nobody is watching it yet. Called after a manual scan, which is
    /// the moment a newly granted folder exists to be followed.
    /// </summary>
    public void EnsureWatching(LibraryRootId rootId) => _ = RunQuietlyAsync(async () =>
    {
        if (await _roots.GetAsync(rootId, _shutdown.Token).ConfigureAwait(false) is { } root)
        {
            Watch(root);
        }
    });

    /// <summary>Stops every watcher and refuses new ones. Leaving the application calls this.</summary>
    public void Stop()
    {
        if (!_shutdown.IsCancellationRequested)
        {
            _shutdown.Cancel();
        }
    }

    public void Dispose()
    {
        Stop();
        _shutdown.Dispose();
    }

    private void Watch(LibraryRoot root)
    {
        if (_shutdown.IsCancellationRequested || !_watching.TryAdd(root.Id, 0))
        {
            return;
        }

        _ = RunQuietlyAsync(async () =>
        {
            try
            {
                await _coordinator.StartAsync(root, _shutdown.Token).ConfigureAwait(false);
            }
            finally
            {
                // A watcher that ended — policy said nothing to do, or its streams ran dry — may be
                // started again by a later scan of the same root.
                _ = _watching.TryRemove(root.Id, out _);
            }
        });
    }

    /// <summary>
    /// Background watching must never take the process down, and cancellation is the one way it is
    /// asked to end. The coordinator already degrades a broken watcher to a recovery scan; whatever
    /// still escapes is observed here on purpose.
    /// </summary>
    private static async Task RunQuietlyAsync(Func<Task> work)
    {
        try
        {
            await Task.Run(work).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Observed on purpose: a dying watcher must cost the watching, never the application.
        }
    }
}
