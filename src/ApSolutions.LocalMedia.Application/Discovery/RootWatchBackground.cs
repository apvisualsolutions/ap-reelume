// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

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
    private readonly IScanWatchSettings _settings;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Lock _generationLock = new();
    private CancellationTokenSource _generation;

    /// <summary>
    /// Who is already being watched, and it is replaced wholesale by a restart rather than cleared.
    /// <b>Measured, not feared:</b> clearing one shared dictionary leaves the old generation's
    /// <c>finally</c> free to remove the entry the new generation had just added, and the root ends
    /// up watched twice — or not at all. A generation that owns its own book cannot reach into the
    /// next one's.
    /// </summary>
    private ConcurrentDictionary<LibraryRootId, byte> _watching = new();

    public RootWatchBackground(
        ILibraryRootRepository roots,
        RootWatchCoordinator coordinator,
        IScanWatchSettings settings)
    {
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _generation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        _settings.Changed += OnSettingsChanged;
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
    /// Tears the watching down and builds it again, which is how a change in Settings is felt now
    /// rather than next launch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a restart and not a finer switch.</b> Every root goes back through
    /// <c>RootWatchCoordinator.StartAsync</c>, which reads the setting exactly as it does at
    /// startup — so there is <i>one</i> place that decides who is watched, instead of a second copy
    /// of the rule living in whatever toggles it. That is what keeps the promise
    /// <c>DeclareCourseFolder</c> makes in writing true on this path as well: a Manual root is left
    /// alone here for the same reason it is left alone at launch, not because somebody remembered to
    /// check again.
    /// </para>
    /// <para>
    /// <b>Its declared cost:</b> touching the box emits the startup pass again for each root. That
    /// is honest — switching watching back on should catch up with whatever was missed while it was
    /// off — and the per-root lock in the coordinator already serialises it.
    /// </para>
    /// <para>
    /// <b>The finer alternative was rejected by reading the code rather than by measuring it</b>, so
    /// take it as reasoning and not as a reading: switching inside the coordinator's watcher loop,
    /// waiting on the retry channel and the event together. Two traps are visible there.
    /// <c>ProcessWatcherAsync</c> returns when <c>WaitToReadAsync</c> resolves <c>false</c>, which a
    /// completed channel does forever; and a local root with the setting off gets no sweep, so
    /// <c>ProcessFallbackScheduleAsync</c> completes that channel at once, the watcher loop returns,
    /// and turning the box back on would wake nobody — which is exactly the case this exists for.
    /// Anybody who wants that design should measure it before building it.
    /// </para>
    /// <para>
    /// <b>And there is no «have we already left?» check here, which IS measured.</b> One was
    /// written, and removing it left every test green: each generation is linked to the shutdown
    /// token, so once the application has gone the new one is born already cancelled and
    /// <see cref="Watch"/> turns away on its own. A branch nothing can take is a branch nothing
    /// tests, so it is the check that goes and not the assertion.
    /// </para>
    /// </remarks>
    private void Restart()
    {
        CancellationTokenSource previous;
        lock (_generationLock)
        {
            previous = _generation;
            _generation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
            _watching = new ConcurrentDictionary<LibraryRootId, byte>();
        }

        previous.Cancel();
        previous.Dispose();
        Start();
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => Restart();

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
        // Letting go of the setting first: a screen can outlive one host, and a stopped host still
        // subscribed would keep rebuilding watchers nobody can reach.
        _settings.Changed -= OnSettingsChanged;
        Stop();
        lock (_generationLock)
        {
            _generation.Dispose();
        }

        _shutdown.Dispose();
    }

    private void Watch(LibraryRoot root)
    {
        CancellationToken generation;
        ConcurrentDictionary<LibraryRootId, byte> watching;
        lock (_generationLock)
        {
            generation = _generation.Token;
            watching = _watching;
        }

        if (generation.IsCancellationRequested || !watching.TryAdd(root.Id, 0))
        {
            return;
        }

        _ = RunQuietlyAsync(async () =>
        {
            try
            {
                await _coordinator.StartAsync(root, generation).ConfigureAwait(false);
            }
            finally
            {
                // A root counts as watched until everything the coordinator runs for it has ended —
                // which for a swept root means until the application leaves or the setting changes,
                // because the fallback schedule never runs dry. A live watcher that dies inside that
                // is the coordinator's own business to start again; a later scan cannot do it here.
                //
                // Its own book, not the current one: a restart may already have installed the next
                // generation's, and removing from that would evict a watcher that has just started.
                _ = watching.TryRemove(root.Id, out _);
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
