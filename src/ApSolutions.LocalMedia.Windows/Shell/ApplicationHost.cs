// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Storage;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace ApSolutions.LocalMedia.Windows.Shell;

/// <summary>
/// One running application: the services it was built from, the live playback session's teardown,
/// and the release of both.
/// </summary>
/// <remarks>
/// ARQ-001. The composition root kept its provider — and with it LibVLC, SQLite, the tray icon and
/// the hardware key registrations — in a static field that nothing ever released. Two consequences
/// followed. The process leant on Windows to reclaim what it had taken, which is not a teardown but
/// a bet that the operating system will not mind; and two applications could not exist at once in
/// one process, because the second overwrote the first's services. The second consequence is why
/// the assembled suites had to run one after another, and removing that is the proof this class
/// works.
/// <para>
/// What disposal reaches is what the container built: the media player and its factory, the audio
/// adapter, the tray icon, the hotkey registrations and the clients the updater and the metadata
/// provider hold. The one thing it deliberately does not reach is the native LibVLC instance —
/// <see cref="Infrastructure.Playback.LibVlcFactory"/> keeps exactly one per option set for the life
/// of the process, because repeatedly creating and tearing down LibVLC is a native failure mode this
/// repository has already met. It dies with the process, and saying so is better than implying a
/// completeness that is not there.
/// </para>
/// </remarks>
public sealed class ApplicationHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly IAppDataPaths _paths;

    // The live playback session's teardown: the save loop's cancellation and the engine handlers'
    // detachment. One session at a time is the product's own rule.
    private PlaybackSessionHooks? _sessionHooks;

    private bool _disposed;

    private ApplicationHost(ServiceProvider services, IAppDataPaths paths)
    {
        _services = services;
        _paths = paths;
    }

    /// <summary>
    /// The loose file this launch was asked to open, if any. It is a path and nothing more; no entity
    /// is created for it, in the catalogue or anywhere else.
    /// </summary>
    public string? PendingActivationPath { get; set; }

    /// <summary>What the application was built from. Resolving after disposal throws, as it should.</summary>
    public IServiceProvider Services => _services;

    /// <summary>The countdown in flight, so the overlay's buttons can reach it.</summary>
    internal NextEpisodeOffer? NextEpisode { get; set; }

    /// <summary>
    /// Builds one application against one set of paths. Everything the product declares is registered
    /// here: a component nobody registers is a screen nobody can open.
    /// </summary>
    public static ApplicationHost Create(IAppDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var shellHost = new CompositionRoot.ShellHost();
        var services = BuildProvider(new ServiceCollection().AddLocalMedia(paths, shellHost));
        var host = new ApplicationHost(services, paths);
        services.GetRequiredService<Accessor>().Host = host;
        return host;
    }

    /// <summary>
    /// Turns a set of registrations into the container this application runs on, under the two checks
    /// the product wants: no singleton may capture a scoped service, and no registration may name a
    /// dependency nobody registered.
    /// </summary>
    /// <remarks>
    /// ARQ-010. Both checks answer the same question — not whether a defect exists, but when it is
    /// heard. Without them a broken registration waits for whichever resolution first happens to touch
    /// it, and in a desktop application that means a screen, in front of somebody, in a corner no test
    /// opened. Validating at build moves the whole class of defect to startup, which every test in this
    /// repository already pays for and can therefore report.
    /// <para>
    /// It is a separate method so a test can build a deliberately broken collection through the very
    /// path the product uses. Asserting on a copy of the options would only prove the copy.
    /// </para>
    /// </remarks>
    public static ServiceProvider BuildProvider(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    /// <summary>
    /// The control the main window shows: the shell, or the recovery screen when the database cannot
    /// be opened.
    /// </summary>
    public Control CreateShell() => CompositionRoot.FinishShell(_services, _paths);

    /// <summary>
    /// Attaches the lifecycle behaviour to the main window: the close button asks the policy what it
    /// means, and the tray only appears once someone has turned it on.
    /// </summary>
    public void ConfigureWindow(Window window) =>
        CompositionRoot.ConfigureWindow(this, _services, window);

    /// <summary>
    /// Begins a playback session's teardown record. Whatever the previous session left running ends
    /// first: the engine is a singleton, and a subscription per open with no unsubscribe stacks every
    /// dead session's work onto the live one.
    /// </summary>
    internal void BeginPlaybackSession(
        CancellationTokenSource loopCancellation,
        Task loopTask,
        Action detachHandlers)
    {
        EndPlaybackSession();
        _sessionHooks = new PlaybackSessionHooks(loopCancellation, loopTask, detachHandlers);
    }

    /// <summary>Ends the live session's save loop and detaches its engine handlers, if any.</summary>
    internal void EndPlaybackSession()
    {
        _sessionHooks?.End();
        _sessionHooks = null;
    }

    /// <summary>
    /// Releases what this application took. Idempotent, because shutdown can arrive from the window,
    /// from the tray, and from a test that owns the host, and none of them can know about the others.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // The session's loop and handlers go before the services they were feeding: a tick landing
        // on a disposed tracker would be an exception raised by the teardown itself.
        EndPlaybackSession();

        // And the session itself goes with them, which is not the same thing. Ending the hooks
        // detaches the handlers and stops the save loop; the media is still open, so the container's
        // own teardown reaches the coordinator, which stops a player the engine's disposal has
        // already taken away. Measured on 2026-08-17, twice, from a scene that ended while a video
        // was playing: "ObjectDisposedException: LibVlcMediaPlayerEngine" out of
        // PlaybackSessionCoordinator.DisposeAsync — the engine is registered three times, and the
        // last of them is resolved when a video starts drawing, so it enters the disposal list after
        // the coordinator and leaves it before.
        try
        {
            await _services.GetRequiredService<IPlaybackSessionCoordinator>()
                .StopAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Something already took the engine away, which is the very thing this call exists to
            // get ahead of. There is nothing left to stop and nothing worth saying about it.
        }

        await _services.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Holds the host once it exists, so the surfaces the container builds can reach the application
    /// they belong to.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="CompositionRoot.ShellHost"/> and for the same reason: the host
    /// owns the provider, so it cannot be one of the provider's own registrations. What it replaces
    /// is a static field, and the difference is the whole point — there is one of these per
    /// container, so two applications in one process reach their own.
    /// </remarks>
    public sealed class Accessor
    {
        public ApplicationHost? Host { get; set; }

        /// <summary>The application these surfaces belong to, or a failure that says what is missing.</summary>
        public ApplicationHost Required =>
            Host ?? throw new InvalidOperationException(
                "The surfaces were built before the application host was published, so nothing owns "
                    + "this session's teardown.");
    }

    /// <summary>The countdown offered at the end of an episode, and what the buttons decided.</summary>
    internal sealed class NextEpisodeOffer(StartNextEpisodeCountdown countdown)
    {
        public StartNextEpisodeCountdown Countdown { get; } = countdown;

        public bool PlayNowRequested { get; set; }
    }

    private sealed class PlaybackSessionHooks(
        CancellationTokenSource loopCancellation,
        Task loopTask,
        Action detachHandlers)
    {
        public void End()
        {
            loopCancellation.Cancel();
            loopCancellation.Dispose();
            _ = loopTask;
            detachHandlers();
        }
    }
}
