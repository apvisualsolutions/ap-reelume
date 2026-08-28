// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Data;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Application.Lifecycle;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Privacy;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Backup;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.Infrastructure.Privacy;
using ApSolutions.LocalMedia.Infrastructure.Settings;
using ApSolutions.LocalMedia.Infrastructure.Time;
using ApSolutions.LocalMedia.Infrastructure.Updates;
using ApSolutions.LocalMedia.Presentation.Backup;
using ApSolutions.LocalMedia.Presentation.Catalog;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Language;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Metadata;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Recovery;
using ApSolutions.LocalMedia.Presentation.Review;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Presentation.Show;
using ApSolutions.LocalMedia.Presentation.Theme;
using ApSolutions.LocalMedia.Presentation.Updates;
using ApSolutions.LocalMedia.Windows.Accessibility;
using ApSolutions.LocalMedia.Windows.MediaKeys;
using ApSolutions.LocalMedia.Windows.Playback;
using ApSolutions.LocalMedia.Windows.Shell;
using ApSolutions.LocalMedia.Windows.Startup;
using ApSolutions.LocalMedia.Windows.Tray;
using ApSolutions.LocalMedia.Windows.Updates;
using ApSolutions.LocalMedia.Windows.Windowing;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ApSolutions.LocalMedia.Windows;

public static partial class CompositionRoot
{
    /// <summary>
    /// Where the independent updater looks. The Store has its own update channel, and this one is
    /// not duplicated into it: two updaters racing to replace the same installation is not twice the
    /// safety.
    /// </summary>
    /// <remarks>
    /// These two strings are the whole address, so a wrong one has no symptom: GitHub answers 404,
    /// the absence of a release is a settled answer, and the application tells everybody they are up
    /// to date forever. `UpdateSourceTests` compares them against the release address the changelog
    /// publishes, which is the record a person actually reads.
    /// </remarks>
    private const string UpdateRepositoryOwner = "apvisualsolutions";

    private const string UpdateRepositoryName = "ap-reelume";

    /// <summary>
    /// Attaches the lifecycle behaviour to the main window: where it reappears, what closing it
    /// means, and what stops when it goes. With every default in place this changes nothing visible —
    /// closing closes.
    /// </summary>
    /// <remarks>
    /// ARQ-006 left this here on purpose, and ARQ-001 took it out and put it back. The extraction
    /// compiled and the assembled walks stayed green, and then the coverage gate measured it:
    /// 70,89 % of lines and 28,57 % of branches. It resolves ten services from the container, so
    /// reaching the tray path, both closing branches and the loose-activation catch means handing it
    /// a purpose-built provider full of fakes, and several of those services are sealed classes with
    /// dependencies of their own. That is the same verdict `WindowsFilePickers` got, and the same
    /// rule applies: a class comes out when its tests can follow it. This one cannot yet, so it stays
    /// where the walks already reach it, and what it costs is written down rather than hidden.
    /// </remarks>
    internal static void ConfigureWindow(ApplicationHost host, IServiceProvider services, Window window)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(window);

        // Where the window was is part of what the application remembers (WIN-003): the stored
        // placement is applied before anything shows, followed while the window moves, and written
        // once when it closes — whatever path closes it.
        services.GetRequiredService<MainWindowPlacement>().Attach(window);

        var settings = services.GetRequiredService<ILifecycleSettings>();
        var tray = services.GetRequiredService<ITrayService>();
        var coordinator = services.GetRequiredService<IPlaybackSessionCoordinator>();
        var tracker = services.GetRequiredService<PlaybackProgressTracker>();
        var isExiting = false;

        var close = new CloseApplication(
            settings,
            tray,
            persistProgress: () => tracker.FlushAsync(PersistenceTrigger.Close),
            stopPlayback: () => coordinator.StopAsync(CancellationToken.None),
            hideWindow: window.Hide,
            exitApplication: () =>
            {
                isExiting = true;

                // Whatever is still working for this window stops with it: a detection decoding in
                // the background, the root watchers, and the live session's save loop must not
                // outlive the application.
                services.GetRequiredService<SegmentDetectionScheduler>().Stop();
                services.GetRequiredService<RootWatchBackground>().Stop();
                host.EndPlaybackSession();
                ShutdownDesktop();
            });

        tray.OpenRequested += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            window.Show();
            window.Activate();
        });
        // Leaving through the tray takes the same path as closing the window, so the position is
        // written before anything else there too.
        tray.ExitRequested += (_, _) => PostSafely(() => close
            .ExitAsync(coordinator.ActiveSession is not null, CancellationToken.None));

        window.Closing += async (_, args) =>
        {
            if (isExiting)
            {
                return;
            }

            // The close is always cancelled first: the position has to reach storage before the
            // window goes anywhere, and that cannot happen inside a synchronous close handler.
            args.Cancel = true;
            var decision = await close
                .ExecuteAsync(coordinator.ActiveSession is not null, CancellationToken.None)
                .ConfigureAwait(true);
            if (!decision.HideToTray && !isExiting)
            {
                isExiting = true;
                window.Close();
            }
        };

        if (settings.Current.TrayEnabled)
        {
            tray.Show();
        }

        // The watchers start with the window: continuous roots are followed from the first minute,
        // startup-policy roots get the scan their owner asked for, and manual roots are left alone.
        services.GetRequiredService<RootWatchBackground>().Start();

        // The automatic check, if somebody has turned it on. Without this the preference would be a
        // switch on a screen that nothing reads — the whole application would only ever check when a
        // button was pressed, and the setting would be a promise it never kept. With it off, which is
        // the default, this opens no connection at all.
        PostSafely(() => services.GetRequiredService<UpdateViewModel>()
            .CheckAutomaticallyAsync(CancellationToken.None));

        // LIB-016. One pass over the oldest entries, if somebody turned it on. It is posted like the
        // update check — after the window exists — and it reads the switch itself, so with it off,
        // which is the default, this opens no connection and does not even read the catalogue.
        PostSafely(() => services.GetRequiredService<RefreshStaleMetadata>()
            .ExecuteAsync(CancellationToken.None));

        // The subtitle style somebody chose comes back. It is loaded onto the surface the shell was
        // handed rather than onto a freshly resolved one, because this view model is transient: a
        // fresh instance would be filled correctly and shown to nobody. Without this the choice was
        // stored and never read, which is the same defect from the other end.
        if (services.GetRequiredService<ShellHost>().Shell?.SubtitleStyle is { } subtitleStyle)
        {
            PostSafely(() => subtitleStyle.LoadAsync(CancellationToken.None));
        }

        if (host.PendingActivationPath is { Length: > 0 } activation)
        {
            var banner = services.GetRequiredService<LooseFileViewModel>();
            PostSafely(async () =>
            {
                try
                {
                    // Through the shell, so the session gets a screen. Applying the banner and
                    // nothing else is what this used to do, and it played a video nobody could see:
                    // the banner hangs off the player's surfaces, so without them it is a view model
                    // filled correctly and shown to no one.
                    if (services.GetRequiredService<ShellHost>().Shell is { } shell)
                    {
                        await shell.OpenLoosePlayerAsync(activation, CancellationToken.None)
                            .ConfigureAwait(true);
                    }
                }
                catch (PlaybackFailureException)
                {
                    // Refused before anything opened — an unapproved container, or a file that is not
                    // where the activation said. There is no session to describe.
                    banner.Clear();
                }
            });
        }
    }

    /// <summary>
    /// Runs work on the interface thread without becoming an async void: a lambda handed straight to
    /// Post rethrows its exception on the dispatcher and takes the application down for whatever the
    /// work happened to hit — a network answer, a missing file, a refused launch.
    /// </summary>
    internal static void PostSafely(Func<Task> work) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = RunQuietlyAsync(work));

    private static async Task RunQuietlyAsync(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Observed on purpose: the surfaces these jobs feed already land on their own error
            // states, and an entry-point job must never be the reason the process dies.
        }
    }

    /// <summary>
    /// Everything the product declares, as an inspectable collection (ARQ-006, step 1 — started
    /// when ART-A01 retired the artwork registration and the textual reachability assertion had to
    /// stop trusting the file's text). Registering runs nothing: tests assert against these
    /// descriptors instead of against source code as a string.
    /// </summary>
    public static IServiceCollection AddLocalMedia(
        this IServiceCollection services,
        IAppDataPaths paths,
        ShellHost shellHost)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(shellHost);
        return services
            .AddData(paths, shellHost)
            .AddPlayback()
            .AddPersonalisation()
            .AddLibrary()
            .AddSettingsAndBackup()
            .AddUpdates()
            .AddAppearanceAndLifecycle()
            .AddIdentification()
            .AddCatalogEditing()

            .AddTransient(CreateShellSurfaces)
            .AddTransient(provider =>
            {
                var shell = new ShellViewModel(
                    provider.GetRequiredService<INavigationService>(),
                    provider.GetRequiredService<ShellSurfaces>());

                // The library's header button opens the shell's dialog: the shell owns the
                // overlay, the library owns the header, and the wiring happens here the way
                // DetailsLoader's already does.
                if (shell.Library is { } library)
                {
                    library.AddMediaCommand = shell.AddMediaCommand;
                }

                provider.GetRequiredService<ShellHost>().Shell = shell;
                return shell;
            })
            .AddTransient(provider => new ShellView
            {
                DataContext = provider.GetRequiredService<ShellViewModel>(),
            });
    }

    /// <summary>
    /// Continues <see cref="ApplicationHost.CreateShell"/> after the registrations are built.
    /// </summary>
    /// <remarks>
    /// The host is not a parameter here and does not need to be: the surfaces that have to reach it
    /// take it from the accessor its own container publishes, which is what replaced the static field
    /// ARQ-001 removed.
    /// </remarks>
    internal static Control FinishShell(IServiceProvider services, IAppDataPaths paths)
    {
        _ = services.GetRequiredService<IThemeService>();

        // And the nine appearance preferences, in that order and before any surface: the theme
        // decides which page the accent is derived against, and the cover size, the gutter and the
        // two radii are read by every card the library is about to build.
        _ = services.GetRequiredService<IAppearanceService>();

        // The stored language is applied before any surface is built, so the first window already
        // speaks it and the thread culture the updater and the metadata read agrees with it.
        ((StoredLanguageService)services.GetRequiredService<ILanguageService>()).ApplyCurrent();
        var migrationRunner = services.GetRequiredService<MigrationRunner>();
        var integrityChecker = services.GetRequiredService<IDatabaseIntegrityChecker>();

        // What the window is handed now, so it can be shown now. The decision between the shell and
        // the recovery screen is the same one as before; only when it is taken has changed.
        var host = new ContentControl { Content = new StartupView() };
        GuardedEvent.Run(
            async () =>
            {
                var refusal = await PrepareDatabaseAsync(migrationRunner, integrityChecker).ConfigureAwait(true);
                host.Content = refusal is null
                    ? services.GetRequiredService<ShellView>()
                    : CreateRecoveryView(services, paths.DatabasePath, migrationRunner.LastBackupPath, refusal);
                if (refusal is null)
                {
                    // The destination the application opens on arrives the same way every other one
                    // does. NavigationService starts on Home and raises nothing, so the shell's
                    // arrival handler — the only thing that reads Home — never ran until somebody
                    // left Home and came back: the owner opened the application on a library of 102
                    // scanned files and found it blank. Announcing the route that is already current
                    // makes the first destination an arrival rather than an initial value.
                    var navigation = services.GetRequiredService<INavigationService>();
                    navigation.Navigate(navigation.CurrentRoute);
                    await StartReviewBadgeAsync(services).ConfigureAwait(true);
                }
            },
            // Nothing else can report a failure here: the window is already on screen with a startup
            // view in it, and leaving that view up for good would be the one outcome that says
            // nothing at all.
            exception => host.Content =
                CreateRecoveryView(services, paths.DatabasePath, migrationRunner.LastBackupPath, exception.Message));

        return host;
    }

    /// <summary>
    /// Counts what is waiting in the review inbox, and keeps counting it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rail's badge is the prototype's, and this is the wire behind it. It is also the first
    /// subscriber the application event bus has ever had: <c>ReviewInboxChanged</c> is published by
    /// <c>ResolveMatch</c> and <c>RejectMatch</c>, and <c>InProcessApplicationEventPublisher.Published</c>
    /// had no handler anywhere in <c>src/</c> — a bus with a publisher and no listener, which is the
    /// house defect wearing its widest hat.
    /// </para>
    /// <para>
    /// The count is taken once at startup and again on every decision, because those are the two
    /// moments it can change: nothing else adds to the inbox while the application is open except a
    /// scan, and a scan that identifies a file publishes the same event.
    /// </para>
    /// </remarks>
    private static async Task StartReviewBadgeAsync(IServiceProvider services)
    {
        var shell = services.GetRequiredService<ShellHost>().Shell;
        if (shell is null)
        {
            return;
        }

        var inbox = services.GetRequiredService<GetReviewInbox>();
        var events = services.GetRequiredService<InProcessApplicationEventPublisher>();
        events.Published += published =>
        {
            if (published is ReviewInboxChanged)
            {
                GuardedEvent.Run(() => CountAsync(inbox, shell));
            }
        };

        await CountAsync(inbox, shell).ConfigureAwait(true);
    }

    /// <summary>One count, on the interface thread, with the page size the use case allows.</summary>
    private static async Task CountAsync(GetReviewInbox inbox, ShellViewModel shell)
    {
        var page = await inbox
            .ExecuteAsync(new GetReviewInboxQuery(PageSize: 100), CancellationToken.None)
            .ConfigureAwait(true);

        // A hundred is the ceiling the use case takes, and NextOffset is how it says there are more.
        var pending = page.NextOffset is null ? page.Items.Count : 101;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => shell.ApplyReviewPendingCount(pending));
    }

    /// <summary>
    /// Makes the database ready off the interface thread, and says why not when it will not be.
    /// </summary>
    /// <remarks>
    /// ARQ-005. <see cref="MigrationRunner.MigrateAsync"/> is written with real awaits and yields at
    /// none of them — <c>Microsoft.Data.Sqlite</c> implements its <c>Async</c> surface synchronously
    /// because SQLite has no asynchronous I/O — so awaiting it here would leave the interface thread
    /// exactly as blocked as the <c>GetAwaiter().GetResult()</c> it replaced, while looking fixed.
    /// <c>MigrationYieldTests</c> measures that rather than assuming it. Hence a thread of its own:
    /// <see cref="SqliteConnectionFactory"/> opens a connection per call, so the work travels.
    /// <para>
    /// The four exceptions caught are the ones that mean the file cannot be used, and they become
    /// the recovery screen. Anything else is a defect rather than a refusal, and is left to the
    /// caller's guard so it is not dressed up as a database problem.
    /// </para>
    /// </remarks>
    private static Task<string?> PrepareDatabaseAsync(
        MigrationRunner migrationRunner,
        IDatabaseIntegrityChecker integrityChecker) =>
        Task.Run<string?>(async () =>
        {
            try
            {
                await migrationRunner.MigrateAsync(CancellationToken.None).ConfigureAwait(false);

                // The runner already proved integrity before touching anything, on this very start.
                // Asking SQLite the same question twice doubles the slowest part of opening a large
                // library; the second check only runs when a migration actually rewrote the file
                // (BUG-012).
                if (migrationRunner.AppliedMigrationCount > 0)
                {
                    var integrity = await integrityChecker.CheckAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                    if (!integrity.IsValid)
                    {
                        return integrity.Detail;
                    }
                }

                return null;
            }
            catch (Exception exception) when (
                exception is Microsoft.Data.Sqlite.SqliteException
                    or InvalidDataException
                    or IOException
                    or UnauthorizedAccessException)
            {
                return exception.Message;
            }
        });

    /// <summary>
    /// Wires the library to the two detail surfaces and to the reads they need. The library itself
    /// still knows only the catalogue; watch states, episodes, and versions arrive through this hook.
    /// </summary>
    /// <summary>
    /// The trailer sitting next to the film, if there is one. The listing happens here because the
    /// policy that decides is pure and touches no disk; a folder that cannot be read is not a
    /// failure worth reporting — it is simply a film with no trailer offered.
    /// </summary>
    /// <remarks>
    /// It takes the film's own path, and that is the correction of a measured defect. It used to take
    /// the version <b>group</b> and read the path off its first available member — but
    /// <c>GroupMediaVersions</c> refuses fewer than two versions, so a film with no duplicate has no
    /// group at all, and the trailer was offered only to films that happened to be duplicated.
    /// Measured on 2026-08-18 with a film and its `-trailer` sibling on disk: the card found nothing.
    /// The group still decides <em>which</em> copy is the film when there is one, because the trailer
    /// belongs beside the copy that would play.
    /// </remarks>
    private static string? FindTrailer(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(path));
            if (folder is null || !Directory.Exists(folder))
            {
                return null;
            }

            var trailers = Path.Combine(folder, TrailerDiscoveryPolicy.FolderName);
            string[] candidates =
            [
                .. Directory.EnumerateFiles(folder),
                .. Directory.Exists(trailers) ? Directory.EnumerateFiles(trailers) : [],
            ];
            return TrailerDiscoveryPolicy.Select(path, candidates);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static LibraryViewModel CreateLibraryViewModel(IServiceProvider provider)
    {
        var watchStates = provider.GetRequiredService<IWatchStateRepository>();
        var episodes = provider.GetRequiredService<IEpisodeSequenceRepository>();
        var personalFilters = provider.GetRequiredService<GetPersonalFilters>();
        var setPersonalState = provider.GetRequiredService<SetPersonalState>();
        var versionGroups = provider.GetRequiredService<IMediaVersionGroupRepository>();

        // The synopsis was stored, merged and editable long before any card could show it (LIB-013).
        // It is read here, next to the watch state and the versions, because a details view model in
        // this application never queries: it shows what somebody already read for it.
        var catalogMetadata = provider.GetRequiredService<ICatalogMetadataRepository>();
        var host = provider.GetRequiredService<ShellHost>();
        MovieDetailsViewModel? movieDetails = null;
        movieDetails = new MovieDetailsViewModel(
            // The card is where playback starts. Until this hook existed, the player and the use case
            // behind it were registered and never invoked by anything.
            onPlay: request => host.Shell is { } shell
                ? shell.OpenPlayerAsync(
                    request with { MediaFileId = request.MediaFileId ?? new MediaFileId(movieDetails!.TitleId.Value) },
                    CancellationToken.None)
                : Task.CompletedTask,
            // The toggle was built with a null handler (CNT-A01): every mark a person made on the
            // card went nowhere. A chosen status is recorded as a manual override; choosing none
            // hands the state back to the automatic rules under the threshold in force. The control
            // then shows what the repository holds, never what the click hoped.
            onWatchStatusChanged: async status =>
            {
                var content = ContentKey.ForTitle(movieDetails!.TitleId);
                var setWatchStatus = provider.GetRequiredService<SetWatchStatus>();
                var state = status is { } chosen
                    ? await setWatchStatus.MarkAsync(
                            content,
                            movieDetails.Versions.FirstOrDefault(row => row.IsEffective)?.MediaFileId
                                ?? new MediaFileId(movieDetails.TitleId.Value),
                            chosen,
                            CancellationToken.None)
                        .ConfigureAwait(true)
                    : await setWatchStatus.ClearOverrideAsync(
                            content,
                            provider.GetRequiredService<ConfigureWatchedThreshold>().Current,
                            CancellationToken.None)
                        .ConfigureAwait(true);
                movieDetails.WatchStatus.Apply(
                    state?.Status ?? WatchStatus.NotStarted,
                    state?.IsManualOverride ?? false);
            },
            onPersonalActionChanged: async request =>
            {
                var content = ContentKey.ForTitle(movieDetails!.TitleId);
                await ApplyPersonalActionAsync(setPersonalState, content, request).ConfigureAwait(true);
                movieDetails.PersonalActions.Apply(
                    await personalFilters.GetAsync(content, CancellationToken.None).ConfigureAwait(true));
            },

            // A trailer is a file the person already has, so it opens the way a file dropped on the
            // application opens: as a loose session. That path refuses an extension outside the
            // approved list and writes no catalogue row, which is exactly what a trailer must not
            // become (LIB-014). Nothing is downloaded and nothing is streamed. It goes through the
            // shell for the reason the activation does: a session with no surfaces is a video nobody
            // can see or stop.
            onPlayTrailer: async path =>
            {
                if (provider.GetRequiredService<ShellHost>().Shell is { } shell)
                {
                    await shell.OpenLoosePlayerAsync(path, CancellationToken.None).ConfigureAwait(true);
                }
            },

            // The provider's trailer goes the other way (LIB-015): out of the application and into
            // whatever browser the person uses. The address arrives already composed by
            // TrailerLinkPolicy from a key that passed its alphabet and its length, and the launcher
            // refuses anything that is not https on its own host. Nothing here connects to YouTube.
            onOpenTrailerLink: link => provider
                .GetRequiredService<IExternalLinkLauncher>()
                .TryLaunchAsync(link, CancellationToken.None));
        ShowDetailsViewModel? showDetails = null;
        showDetails = new ShowDetailsViewModel(
            onPlay: request => host.Shell is { } shell
                ? shell.OpenPlayerAsync(request, CancellationToken.None)
                : Task.CompletedTask,
            onPersonalActionChanged: async request =>
            {
                var content = ContentKey.ForTitle(showDetails!.TitleId);
                await ApplyPersonalActionAsync(setPersonalState, content, request).ConfigureAwait(true);
                showDetails.PersonalActions.Apply(
                    await personalFilters.GetAsync(content, CancellationToken.None).ConfigureAwait(true));
            },
            onOpenTrailerLink: link => provider
                .GetRequiredService<IExternalLinkLauncher>()
                .TryLaunchAsync(link, CancellationToken.None));
        var library = new LibraryViewModel(
            provider.GetRequiredService<ICatalogQueryService>(),
            movieDetails,
            showDetails,
            provider.GetRequiredService<ScanProgressViewModel>());
        library.DetailsLoader = async item =>
        {
            var stored = await catalogMetadata
                .GetAsync(item.Item.Id, CancellationToken.None)
                .ConfigureAwait(true);
            var overview = stored?.Metadata.Overview;
            var trailerKey = stored?.Metadata.TrailerKey;
            if (item.Item.Kind == CatalogTitleKind.Show)
            {
                var sequence = await episodes
                    .GetSeriesAsync(item.Item.Id, CancellationToken.None)
                    .ConfigureAwait(true);
                var states = new Dictionary<ContentKey, WatchState>();
                foreach (var entry in sequence)
                {
                    var key = ContentKey.ForEpisode(entry.ShowId, entry.Id);
                    if (await watchStates.GetAsync(key, CancellationToken.None).ConfigureAwait(true) is { } state)
                    {
                        states[key] = state;
                    }
                }

                var showKey = ContentKey.ForTitle(item.Item.Id);
                var showPersonal = await personalFilters
                    .GetAsync(showKey, CancellationToken.None)
                    .ConfigureAwait(true);
                var showRename = await WouldRenameChangeAnythingAsync(
                    provider,
                    item.Item.Id,
                    CancellationToken.None).ConfigureAwait(true);
                showDetails.Apply(
                    item.Item,
                    sequence,
                    states,
                    showPersonal,
                    overview: overview,
                    trailerKey: trailerKey,
                    renameWouldChangeTheName: showRename);
            }
            else
            {
                var key = ContentKey.ForTitle(item.Item.Id);
                var state = await watchStates.GetAsync(key, CancellationToken.None).ConfigureAwait(true);
                var personal = await personalFilters.GetAsync(key, CancellationToken.None).ConfigureAwait(true);
                var versions = await versionGroups
                        .FindByContentKeyAsync(key.Value, CancellationToken.None)
                        .ConfigureAwait(true)
                    ?? await versionGroups
                        .FindByMemberAsync(new MediaFileId(item.Item.Id.Value), CancellationToken.None)
                        .ConfigureAwait(true);
                // The copy that would play: the group's first available member when the film has one,
                // and otherwise the film's own row — which is the ordinary case, because a film with
                // no duplicate is never grouped.
                var file = await provider.GetRequiredService<IMediaFileRepository>()
                    .FindByIdAsync(new MediaFileId(item.Item.Id.Value), CancellationToken.None)
                    .ConfigureAwait(true);
                var filmPath = versions?.Versions.FirstOrDefault(version => version.IsAvailable)?.Path
                    ?? file?.Path;
                var movieRename = await WouldRenameChangeAnythingAsync(
                    provider,
                    item.Item.Id,
                    CancellationToken.None).ConfigureAwait(true);
                movieDetails.Apply(
                    item.Item,
                    state,
                    versions,
                    personal,
                    overview: overview,
                    trailerPath: FindTrailer(filmPath),
                    trailerKey: trailerKey,
                    file: file,
                    renameWouldChangeTheName: movieRename,
                    posterFile: FindCachedPoster(provider, item.Item.Id, stored?.Metadata.PosterPath));
            }
        };
        return library;
    }

    /// <summary>
    /// The poster this title already has on the disk, or nothing.
    /// </summary>
    /// <remarks>
    /// Opening a card must not open a connection, so this asks the store where the file is and never
    /// asks it to fetch one. What fetches is <c>ApplyIdentification</c>, at the moment somebody has
    /// consented to talk to the provider — which means a library identified before 2026-08-28 gets
    /// its posters at its next identification or refresh, the same way a scanned title got its name.
    /// </remarks>
    private static string? FindCachedPoster(IServiceProvider provider, TitleId titleId, string? posterPath) =>
        PosterAddressPolicy.TryBuildPosterAddress(posterPath) is { } address
            ? provider.GetRequiredService<IArtworkStore>().Find(titleId, new Uri(address, UriKind.Absolute))
            : null;

    /// <summary>
    /// Everything the shell is handed. The long-lived surfaces arrive built; the ones that describe
    /// one title, one plan, or one playing session arrive as the request that builds them.
    /// </summary>
    private static ShellSurfaces CreateShellSurfaces(IServiceProvider provider)
    {
        var host = provider.GetRequiredService<ApplicationHost.Accessor>().Required;
        return new ShellSurfaces
        {
            AppearanceSettings = provider.GetRequiredService<AppearanceSettingsViewModel>(),
            Library = provider.GetRequiredService<LibraryViewModel>(),
            Home = provider.GetRequiredService<HomeViewModel>(),
            RecommendationSettings = provider.GetRequiredService<RecommendationSettingsViewModel>(),
            LifecycleSettings = provider.GetRequiredService<LifecycleSettingsViewModel>(),
            Backups = provider.GetRequiredService<BackupViewModel>(),
            Restore = provider.GetRequiredService<RestoreWizardViewModel>(),
            PrivacySettings = provider.GetRequiredService<PrivacySettingsViewModel>(),
            Updates = provider.GetRequiredService<UpdateViewModel>(),
            Onboarding = provider.GetRequiredService<RootOnboardingViewModel>(),
            DuplicatesOverview = CreateDuplicatesOverview(provider),
            StartScan = (rootId, cancellationToken) => ScanRootAsync(provider, rootId, cancellationToken),
            ReviewInbox = provider.GetRequiredService<ReviewInboxViewModel>(),
            ScanSettings = provider.GetRequiredService<ScanSettingsViewModel>(),
            Shortcuts = provider.GetRequiredService<ShortcutSettingsViewModel>(),
            SubtitleStyle = provider.GetRequiredService<SubtitleStyleViewModel>(),
            MiniPlayerPlacement = provider.GetRequiredService<IMiniPlayerPlacementStore>(),
            SegmentDetection = new SegmentDetectionSettingsViewModel(
            () => provider.GetRequiredService<DetectSeriesSegments>().IsEnabled,
            enabled => provider.GetRequiredService<DetectSeriesSegments>().SetEnabled(enabled)),
            OpenMetadataEditor = (titleId, cancellationToken) =>
                OpenMetadataEditorAsync(provider, titleId, cancellationToken),
            OpenRename = (titleId, cancellationToken) => OpenRenameAsync(provider, titleId, cancellationToken),
            OpenDuplicates = (titleId, cancellationToken) => OpenDuplicatesAsync(provider, titleId, cancellationToken),
            OpenPlayer = (request, cancellationToken) => OpenPlayerAsync(host, provider, request, cancellationToken),
            OpenLoosePlayer = (path, cancellationToken) =>
                OpenLoosePlayerAsync(provider, path, cancellationToken),
            // Closing the player writes the position before it stops the media: a session that ends
            // without persisting is a resume offer that never appears.
            ClosePlayer = async cancellationToken =>
            {
                // The loop and the handlers go first, so no tick races the final write and no dead
                // session keeps feeding the singleton engine's events.
                host.EndPlaybackSession();

                // The write is not cancellable on purpose: a cancelled close would leave the position
                // half written, and losing it is exactly what this call exists to prevent.
                await provider.GetRequiredService<PlaybackProgressTracker>()
                    .FlushAsync(PersistenceTrigger.Close, CancellationToken.None)
                    .ConfigureAwait(true);
                await provider.GetRequiredService<IPlaybackSessionCoordinator>()
                    .StopAsync(cancellationToken)
                    .ConfigureAwait(true);
            },
            ChangePlaybackMode = async (mode, cancellationToken) =>
                (await provider.GetRequiredService<ChangePlaybackMode>()
                    .ExecuteAsync(mode, cancellationToken)
                    .ConfigureAwait(false)).To,
        };
    }

    /// <summary>
    /// The duplicates destination's list, wired to the same door the film card's action opens: a
    /// row leads to the very review a person reaches from the card, so the two can never disagree.
    /// </summary>
    private static DuplicatesOverviewViewModel CreateDuplicatesOverview(IServiceProvider provider)
    {
        // The opener is the shell's to wire - its constructor points every row at the same door
        // the film card uses - so the composition only builds the list half.
        return new DuplicatesOverviewViewModel(
            provider.GetRequiredService<ApSolutions.LocalMedia.Application.Catalog.GetDuplicateOverview>(),
            provider.GetRequiredService<ApSolutions.LocalMedia.Application.Catalog.SetPreferredVersion>());
    }

    /// <summary>
    /// Scans one root and announces the progress on the library screen. The scan is cancellable from
    /// that same announcement, because it is the one job that runs for minutes.
    /// </summary>
    private static async Task ScanRootAsync(
        IServiceProvider provider,
        LibraryRootId rootId,
        CancellationToken cancellationToken)
    {
        var progress = provider.GetRequiredService<ScanProgressViewModel>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        progress.Begin(cancellation);
        try
        {
            // The shared coordinator already hands every summary to identification, so the inbox
            // fills from here and from watcher scans alike.
            _ = await provider.GetRequiredService<IScanCoordinator>()
                .StartAsync(new StartScanCommand(rootId, ScanTrigger.Initial), cancellation.Token)
                .ConfigureAwait(true);

            // A root scanned by hand is followed from then on: onboarding runs its first scan the
            // moment a folder is granted, and this is what makes watching begin without a relaunch.
            provider.GetRequiredService<RootWatchBackground>().EnsureWatching(rootId);
        }
        catch (OperationCanceledException)
        {
            // Cancelling a scan is a decision, not a failure: what was already found stays catalogued.
        }
    }

    /// <summary>
    /// The metadata of one title, created on first edit. A title nobody has edited has no row yet, so
    /// the editor starts from what the catalogue displays rather than refusing to open.
    /// </summary>
    private static async Task<MetadataEditorViewModel?> OpenMetadataEditorAsync(
        IServiceProvider provider,
        TitleId titleId,
        CancellationToken cancellationToken)
    {
        var repository = provider.GetRequiredService<ICatalogMetadataRepository>();
        var stored = await repository.GetAsync(titleId, cancellationToken).ConfigureAwait(true);
        var catalog = stored ?? new CatalogMetadata(
            titleId,
            new EditableMetadata(
                provider.GetRequiredService<ShellHost>().Shell?.Library?.SelectedItem?.Title ?? string.Empty,
                OriginalTitle: null,
                Overview: null,
                ReleaseYear: null,
                Genres: [],
                PosterPath: null,
                BackdropPath: null,
                TrailerKey: null,
                LockedFields: new HashSet<MetadataField>()),
            Revision: 0);
        return new MetadataEditorViewModel(
            catalog,
            provider.GetRequiredService<UpdateMetadata>(),
            provider.GetRequiredService<RefreshMetadata>(),
            provider.GetRequiredService<ArtworkPickerViewModel>());
    }

    /// <summary>
    /// A rename preview for one title. Producing the preview reads the file and touches nothing: the
    /// plan describes what would happen, and only an explicit confirmation executes it.
    /// </summary>
    /// <remarks>
    /// LIB-012: what this asked for used to be the name the file already had, which
    /// <see cref="RenamePolicy"/> correctly discards, so the plan was always empty and neither
    /// button could ever run. The name now comes from the entry — what a provider identified when
    /// somebody identified it, and what the parser reads off the file name when nobody has, since a
    /// catalogue with no metadata row still knows a season and an episode when the name carries one.
    /// An entry with nothing better to propose produces no request at all rather than a request that
    /// asks for the current name back.
    /// </remarks>
    /// <summary>
    /// Whether renaming this title's file would change its name at all.
    /// </summary>
    /// <remarks>
    /// Asked when a card opens, so the card can leave the button out when the answer is no: a
    /// request whose destination equals its source becomes a <c>NoChange</c> conflict rather than an
    /// operation, and a preview of no operations is a button promising work there is none of. It
    /// builds the same plan the preview does rather than a second rule about names, because two
    /// rules drift apart on the file nobody tested.
    /// </remarks>
    private static async Task<bool> WouldRenameChangeAnythingAsync(
        IServiceProvider provider,
        TitleId titleId,
        CancellationToken cancellationToken) =>
        await BuildRenamePlanAsync(provider, titleId, cancellationToken).ConfigureAwait(true)
            is { Operations.Count: > 0 };

    private static async Task<RenamePreviewViewModel?> OpenRenameAsync(
        IServiceProvider provider,
        TitleId titleId,
        CancellationToken cancellationToken)
    {
        var plan = await BuildRenamePlanAsync(provider, titleId, cancellationToken).ConfigureAwait(true);
        return plan is null
            ? null
            : new RenamePreviewViewModel(
                plan,
                provider.GetRequiredService<ExecuteRename>(),
                provider.GetRequiredService<UndoRename>());
    }

    /// <summary>The plan itself, which both the preview and the card's question are built on.</summary>
    private static async Task<RenamePlan?> BuildRenamePlanAsync(
        IServiceProvider provider,
        TitleId titleId,
        CancellationToken cancellationToken)
    {
        var mediaFiles = provider.GetRequiredService<IMediaFileRepository>();
        var file = await mediaFiles
            .FindByIdAsync(new MediaFileId(titleId.Value), cancellationToken)
            .ConfigureAwait(true);
        if (file is null)
        {
            return null;
        }

        var roots = await provider.GetRequiredService<ILibraryRootRepository>()
            .ListAsync(cancellationToken)
            .ConfigureAwait(true);
        var root = roots.FirstOrDefault(candidate => candidate.Id == file.LibraryRootId);
        if (root is null)
        {
            return null;
        }

        var stored = await provider.GetRequiredService<ICatalogMetadataRepository>()
            .GetAsync(titleId, cancellationToken)
            .ConfigureAwait(true);

        // A parser that warned is a parser saying it does not know what this is, and its clean title
        // has already dropped whatever confused it — `Tomorrow.9999.mkv` reads as "Tomorrow", so
        // renaming to what it read would throw away part of the only name anybody has. Its reading
        // is used when it is sure, and the entry's own metadata is used whenever there is any.
        var read = provider.GetRequiredService<IMediaNameParser>()
            .Parse(FileNameContext.ForFile(file.Path, root.Path));
        var parsed = read.ParseWarnings.Count == 0 ? read : null;

        // Title and year travel together from one source or the other, never one from each: an entry
        // whose title is still the file name carries its year inside the title, and pairing it with
        // the year the parser found writes it twice — measured as "Arrival 2016 (2016).mp4". The
        // season and the episode are the exception because no entry holds them; only the name does.
        var identified = string.IsNullOrWhiteSpace(stored?.Metadata.Title) ? null : stored;
        var proposal = TitleFileNamePolicy.Compose(new TitleNaming(
            identified?.Metadata.Title ?? parsed?.CleanTitle,
            identified is null ? parsed?.Year : identified.Metadata.ReleaseYear,
            parsed?.Season,
            parsed?.Episode,
            EpisodeTitle: null,
            Path.GetExtension(file.Path)));

        return provider.GetRequiredService<PreviewRename>().Execute(new PreviewRenameCommand(
            root.Path,
            proposal is null ? [] : [new RenameRequest(file.Path, proposal)]));
    }

    /// <summary>The versions of one title, or nothing when only one copy of it exists.</summary>
    private static async Task<DuplicateReviewViewModel?> OpenDuplicatesAsync(
        IServiceProvider provider,
        TitleId titleId,
        CancellationToken cancellationToken)
    {
        // Unidentified copies each carry their own title, so the group may be stored under another
        // copy's key; the member lookup is what makes every card reach the same group.
        var repository = provider.GetRequiredService<IMediaVersionGroupRepository>();
        var group = await repository
                .FindByContentKeyAsync(ContentKey.ForTitle(titleId).Value, cancellationToken)
                .ConfigureAwait(true)
            ?? await repository
                .FindByMemberAsync(new MediaFileId(titleId.Value), cancellationToken)
                .ConfigureAwait(true);
        return group is null
            ? null
            : new DuplicateReviewViewModel(
                group,
                provider.GetRequiredService<MediaVersionSelectionPolicy>(),
                provider.GetRequiredService<SetPreferredVersion>(),
                new MediaVersionPreferences(PreferHdr: true));
    }

    /// <summary>
    /// Opens one file from outside the library and builds the little that session puts on screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately not <see cref="OpenPlayerAsync"/> with a flag. That one begins by reading the
    /// media file from the catalogue — a loose file is in no catalogue — and its path attaches the
    /// progress tracker, reads the resume decision, composes the markers and looks up the version
    /// group. Every one of those writes or reads something a loose session promised not to touch: "a
    /// loose session leaves the database exactly as it found it". What a loose session gets is the
    /// player, its transport, the picture's own diagnosis, and the banner that says where this came
    /// from.
    /// </para>
    /// <para>
    /// The use case is asked first and its refusals are what they always were, so an unapproved
    /// container or a file that is not there fails before the engine is touched. What changed on
    /// 2026-08-17 is which of the two opens: the player does, and therefore a failure lands on the
    /// surface that can say so — a file that cannot be decoded now reaches the recovery actions
    /// instead of clearing the banner and leaving an empty screen.
    /// </para>
    /// </remarks>
    private static async Task<PlayerSurfaces?> OpenLoosePlayerAsync(
        IServiceProvider provider,
        string path,
        CancellationToken cancellationToken)
    {
        var session = await OpenLooseFile.ExecuteAsync(path, cancellationToken)
            .ConfigureAwait(true);

        var player = new PlayerViewModel(
            provider.GetRequiredService<IPlaybackSessionCoordinator>(),
            provider.GetRequiredService<IVideoFrameSource>(),
            provider.GetRequiredService<IExternalPlaybackLauncher>())
        {
            Transport = provider.GetRequiredService<TransportControlsViewModel>(),
        };

        var videoStatus = new VideoStatusViewModel();
        var capabilities = provider.GetRequiredService<LibVlcMediaPlayerEngine>().Capabilities;
        videoStatus.Apply(
            capabilities,
            capabilities is { HardwareAccelerationRequested: true, HardwareAccelerationActive: false });

        var banner = provider.GetRequiredService<LooseFileViewModel>();
        banner.Apply(session);

        await player.OpenAsync(session.MediaFileId, session.Path, cancellationToken: cancellationToken)
            .ConfigureAwait(true);

        return new PlayerSurfaces
        {
            Player = player,
            VideoStatus = videoStatus,
            LooseFile = banner,
        };
    }

    /// <summary>
    /// Opens one media file and builds everything that session puts on screen. The tracks, the output
    /// device, and the resume offer are read here so the surface never has to query anything itself.
    /// </summary>
    private static async Task<PlayerSurfaces?> OpenPlayerAsync(
        ApplicationHost host,
        IServiceProvider provider,
        PlayDetailsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MediaFileId is not { } mediaFileId)
        {
            return null;
        }

        var file = await provider.GetRequiredService<IMediaFileRepository>()
            .FindByIdAsync(mediaFileId, cancellationToken)
            .ConfigureAwait(true);
        if (file is null)
        {
            return null;
        }

        var transport = provider.GetRequiredService<TransportControlsViewModel>();

        // Declared here and filled in far below, because the player is built before the version group
        // has been read and it needs to be able to ask. A value passed at this point would always be
        // the answer from before anybody looked.
        PlayerVersionsViewModel? versions = null;
        var player = new PlayerViewModel(
            provider.GetRequiredService<IPlaybackSessionCoordinator>(),
            provider.GetRequiredService<IVideoFrameSource>(),
            provider.GetRequiredService<IExternalPlaybackLauncher>(),
            () => versions?.HasAlternatives == true)
        {
            Transport = transport,
        };

        // An episode belongs to its show, and the T29 model stores markers per series; a file that
        // is not an episode keeps its own identifier as its series, which is what movies already do.
        var episodeEntry = await provider.GetRequiredService<IEpisodeSequenceRepository>()
            .FindByFileAsync(mediaFileId, cancellationToken)
            .ConfigureAwait(true);
        var seriesId = episodeEntry is not null
            ? new SeriesId(episodeEntry.ShowId.Value)
            : new SeriesId(mediaFileId.Value);
        var manualMarkers = await provider.GetRequiredService<IIntroMarkerRepository>()
            .GetForSeriesAsync(seriesId, cancellationToken)
            .ConfigureAwait(true);
        var detectedRows = await provider.GetRequiredService<IDetectedMarkerRepository>()
            .GetForFileAsync(mediaFileId, cancellationToken)
            .ConfigureAwait(true);
        var sessionMarkers = SegmentDetectionPolicy.ComposeForFile(manualMarkers, detectedRows);
        var skip = new SkipMarkerViewModel(position =>
            provider.GetRequiredService<ControlPlayback>().SeekAsync(position, CancellationToken.None));

        // What the skip button follows is recomposed after every marker mutation, so a marker made
        // mid-playback works without closing and reopening the episode (BUG-008). The editor's own
        // list reloads with it, and the composition rule stays the tested one.
        MarkerEditorViewModel? markers = null;
        async Task RefreshSessionMarkersAsync()
        {
            var manual = await provider.GetRequiredService<IIntroMarkerRepository>()
                .GetForSeriesAsync(seriesId, CancellationToken.None)
                .ConfigureAwait(true);
            var detected = await provider.GetRequiredService<IDetectedMarkerRepository>()
                .GetForFileAsync(mediaFileId, CancellationToken.None)
                .ConfigureAwait(true);
            sessionMarkers = SegmentDetectionPolicy.ComposeForFile(manual, detected);
            markers?.Load(seriesId, manual, file.TechnicalMetadata.Duration);
        }

        // The overlay's buttons reach the countdown that is actually running: "play now" skips the
        // wait and "cancel" ends it. Resolved bare, both buttons only hid the card (PLY-011).
        var nextEpisode = new NextEpisodeViewModel(action => HandleNextEpisodeActionAsync(host, action));

        // One router per session: keyboard and media keys resolve into the same action exactly
        // once, which is what stops a media key the focused window also sees as a key press from
        // toggling playback twice (ARQ-002).
        var router = new InputCommandRouter((command, token) =>
            ExecutePlaybackInputAsync(provider, player, transport, command, token));
        var shortcuts = provider.GetRequiredService<ShortcutMap>();
        player.GestureHandler = gesture =>
        {
            if (shortcuts.Resolve(gesture) is not { } command)
            {
                return false;
            }

            PostSafely(() => router.DispatchAsync(command, InputOrigin.Keyboard, CancellationToken.None));
            return true;
        };

        // The two mode buttons in the transport bar, and the double click on the picture, all reach
        // the same place a key does. The shell is asked for at the moment of the press rather than
        // captured: a session outlives more than one of this handler's calls, and the shell is the
        // thing that owns which window the picture is in.
        player.ModeHandler = mode =>
            provider.GetRequiredService<ShellHost>().Shell is { } live
                ? live.TogglePlaybackModeAsync(mode, CancellationToken.None)
                : Task.CompletedTask;


        // The decision has to exist before the media opens: the engine accepts a start position at
        // open and nothing later can recover the moment that was lost by starting at zero. Walking
        // the audit through the assembly showed the prompt appearing over a video already playing
        // from the beginning, with buttons wired to nothing.
        var resume = await provider.GetRequiredService<ResumePlayback>()
            .DecideAsync(
                ContentKey.ForTitle(new TitleId(mediaFileId.Value)),
                file.TechnicalMetadata.Duration,
                cancellationToken)
            .ConfigureAwait(true);
        // A caller that named a position had already decided it, and deciding it again here is how a
        // confirmed version switch lost the very second it had just asked about: the policy reads
        // storage under the new file's content key, which a freshly opened version has none of.
        var startPosition = request.StartPosition
            ?? (resume.Choice == ResumeChoice.Resume ? resume.Position : TimeSpan.Zero);

        // The tracker is attached before the media opens and fed from the engine's own position
        // event. Without this the session plays and nothing about it is ever written, so the resume
        // offer never appears again — which is what walking the real application showed.
        var content = ContentKey.ForTitle(new TitleId(mediaFileId.Value));
        var tracker = provider.GetRequiredService<PlaybackProgressTracker>();
        _ = await tracker.BeginAsync(content, mediaFileId, cancellationToken).ConfigureAwait(true);
        var engine = provider.GetRequiredService<IMediaPlayerEngine>();

        // One session's handlers and its save loop end before the next session's begin: the engine
        // is a singleton, and a subscription per open with no unsubscribe stacks every dead
        // session's work onto the live one.
        host.EndPlaybackSession();
        engine.PositionChanged += OnPositionChanged;
        engine.StateChanged += OnStateChanged;

        // The engine chooses a track on its own whenever nobody has stored a preference: a container
        // names its default and LibVLC honours it while the first frames decode — which is after the
        // panel below was built. Measured on 2026-08-25 against a real episode: the subtitle in force
        // was none at open and track 6 three seconds later, so a panel fed only at open time said
        // «no subtitles» over subtitles somebody could read. The surface it feeds is assigned once
        // it exists, so a choice arriving before then simply has nowhere to land yet.
        TrackSelectorViewModel? sessionTracks = null;
        var activeTracks = engine as IActiveTrackSource;
        void OnActiveTracksChanged(object? sender, EventArgs args) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                _ = RunQuietlyAsync(() => FollowActiveTracksAsync(engine, sessionTracks)));
        if (activeTracks is not null)
        {
            activeTracks.ActiveTracksChanged += OnActiveTracksChanged;
        }

        // The hardware keys listen only while a session exists, which is the source's own rule —
        // and they start after the previous session's teardown, which stops them. CommandReceived
        // is raised on the STA pump thread, so it is marshalled before touching anything bound.
        var mediaKeys = provider.GetRequiredService<IMediaKeySource>();
        void OnMediaKey(object? sender, PlaybackInputCommand command) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                _ = RunQuietlyAsync(() => router.DispatchAsync(command, InputOrigin.MediaKey, CancellationToken.None)));
        mediaKeys.CommandReceived += OnMediaKey;
        await mediaKeys.StartAsync(cancellationToken).ConfigureAwait(true);
        var progressLoop = new CancellationTokenSource();

        // The loop is what makes the five-second promise true: without it only an orderly close
        // writes, and a crash costs the whole session instead of one interval.
        var progressLoopTask = RunProgressLoopAsync(tracker, progressLoop.Token);
        host.BeginPlaybackSession(progressLoop, progressLoopTask, () =>
        {
            engine.PositionChanged -= OnPositionChanged;
            engine.StateChanged -= OnStateChanged;
            if (activeTracks is not null)
            {
                activeTracks.ActiveTracksChanged -= OnActiveTracksChanged;
            }

            // The input chain dies with its session: the keys release their registrations, the
            // gesture handler lets go of the router, and the router lets go of its gate.
            mediaKeys.CommandReceived -= OnMediaKey;
            player.GestureHandler = null;
            player.ModeHandler = null;
            _ = StopMediaKeysQuietlyAsync(mediaKeys);
            router.Dispose();
        });
        await player.OpenAsync(mediaFileId, file.Path, startPosition, cancellationToken).ConfigureAwait(true);

        // Which scopes this session's choices are read from and written to. They are computed once
        // because the two sides have to agree: the reading side already asked for the series when the
        // file is an episode, while the surface that writes was built with no series at all — so the
        // application could resolve a series preference that nothing in it could ever store, and the
        // box offering to store one was disabled for every episode ever played. Walking it with the
        // mouse is what said so: "cannot be pressed: visible=True, enabled=False".
        var fileScopeKey = mediaFileId.Value.ToString("D");
        var seriesScopeKey = episodeEntry is not null ? seriesId.Value.ToString("D") : null;

        // The stored choices are applied the moment the media is open (PLY-A01): the use case
        // resolves file over series over global and falls back by language when a named track is
        // absent. Registered, tested end to end, and invoked by nobody — the audit's house defect.
        AppliedPlaybackPreference? applied = null;
        try
        {
            applied = await provider.GetRequiredService<ApplyPlaybackPreferences>()
                .ApplyAsync(
                    engine,
                    new PlaybackPreferenceContext(
                        fileScopeKey,
                        seriesScopeKey,
                        await DiscoverExternalSubtitlesAsync(provider, file.Path, cancellationToken)
                            .ConfigureAwait(true)),
                    cancellationToken)
                .ConfigureAwait(true);
        }
        catch (PlaybackFailureException exception)
            when (exception.Failure.Code == PlaybackFailureCode.EngineUnavailable)
        {
            // A session that failed to open, or closed underneath the apply, has no tracks to
            // choose between. The surface already carries that session's own diagnosis; the
            // preferences simply have nothing to act on.
        }

        void OnPositionChanged(object? sender, PlaybackPositionChangedEventArgs args)
        {
            tracker.Observe(args.Position, args.Duration);

            // The skip offer follows the playhead. The button was reachable and never fed — the
            // same class of defect the video status overlay had — so the ranges are handed to it
            // here, on the interface thread its bindings live on.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => skip.Apply(sessionMarkers, args.Position));

            // And so does the transport bar, which is the third surface in this handler to have been
            // built, tested and fed by nobody — and the one a person actually watches. Its state
            // changed on its own commands alone, so a session ran with no scrubber and no clock
            // until somebody pressed a button: found by playing a real film for the README's
            // capture, on 2026-08-24.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => transport.Observe(args.Position, args.Duration));
        }

        void OnStateChanged(object? sender, PlaybackStateChangedEventArgs args)
        {
            // The surface follows the engine. Without this, the state machine on screen stays
            // wherever OpenAsync left it: the pause a key produced never shows as paused, a stop
            // never shows as stopped — ApplySessionState existed, was tested, and was fed by
            // nobody, which is what the assembled physical walk found. Failures keep their own
            // path: the open exception carries a code this event does not have.
            if (args.CurrentState != PlaybackState.Failed)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    player.ApplySessionState(args.CurrentState, failure: null));
            }

            // A pause is a stopping point the person picked; it deserves to survive a crash.
            if (args.CurrentState == PlaybackState.Paused)
            {
                _ = FlushQuietlyAsync(tracker, PersistenceTrigger.Pause);
            }

            // The end of an episode is the one moment the next one may be offered. The event
            // arrives on LibVLC's thread, so the whole offer runs through the dispatcher.
            if (args.CurrentState == PlaybackState.Ended && episodeEntry is not null)
            {
                PostSafely(() => OfferNextEpisodeAsync(host, provider, episodeEntry, nextEpisode));
            }
        }

        var snapshot = await provider.GetRequiredService<IMediaPlayerEngine>()
            .GetSnapshotAsync(cancellationToken)
            .ConfigureAwait(true);
        player.ApplyTracks(snapshot.Tracks);
        var tracks = new TrackSelectorViewModel(
            provider.GetRequiredService<SelectTrack>(),
            fileScopeKey,
            seriesScopeKey,
            ReadResource("TrackSelectorSubtitlesDisabled"));
        tracks.Load(snapshot.Tracks, applied?.Audio, applied?.Subtitle);
        sessionTracks = tracks;

        // The output choice reaches the engine (AUD-A01): the stored global device is applied to
        // the session that just opened, and a person's pick goes through the adapter that pauses,
        // routes, resumes, and stores — the surface then shows the machine's answer. A session
        // that failed to open simply has nothing to route; the preference stays for the next one.
        var audioOutput = provider.GetRequiredService<AudioOutputViewModel>();
        var outputAdapter = provider.GetRequiredService<LibVlcAudioOutputAdapter>();
        var outputTarget = new EngineAudioOutputTarget(engine);
        audioOutput.SelectionHandler = (deviceId, layout) => outputAdapter.SelectAsync(
            new AudioOutputRequest(deviceId, layout, PreferenceScope.Global, PlaybackPreference.GlobalKey),
            outputTarget,
            CancellationToken.None);
        var storedOutput = await outputAdapter.ResolveStoredAsync(
                PreferenceScope.Global,
                PlaybackPreference.GlobalKey,
                AudioChannelLayout.Stereo,
                cancellationToken)
            .ConfigureAwait(true);
        if (storedOutput is not null)
        {
            try
            {
                // Applied directly rather than through SelectAsync: a fallback for an absent
                // device must not be stored as if the person had chosen it.
                await outputTarget.SetOutputDeviceAsync(storedOutput.Device.Id, cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (PlaybackFailureException exception)
                when (exception.Failure.Code == PlaybackFailureCode.EngineUnavailable)
            {
            }
        }

        await audioOutput.LoadAsync(storedOutput?.Device.Id, cancellationToken).ConfigureAwait(true);

        // The editor and the skip offer are wired to the use cases rather than resolved bare: a marker
        // surface with no handler looks identical to a working one and saves nothing. Each mutation
        // recomposes the session's marks before it returns.
        markers = new MarkerEditorViewModel(
            async (kind, start, end, markerId) =>
            {
                var saved = await provider.GetRequiredService<SaveManualMarker>().ExecuteAsync(
                        new SaveManualMarkerCommand(
                            seriesId,
                            kind,
                            start,
                            end,
                            file.TechnicalMetadata.Duration,
                            markerId),
                        CancellationToken.None)
                    .ConfigureAwait(true);
                await RefreshSessionMarkersAsync().ConfigureAwait(true);
                return saved;
            },
            async markerId =>
            {
                var deleted = await provider.GetRequiredService<DeleteManualMarker>()
                    .ExecuteAsync(markerId, CancellationToken.None)
                    .ConfigureAwait(true);
                await RefreshSessionMarkersAsync().ConfigureAwait(true);
                return deleted;
            });
        markers.Load(seriesId, manualMarkers, file.TechnicalMetadata.Duration);

        // The review surface shows what detection found in this very episode, and a person's
        // accept, adjust, or delete goes straight through the use case that locks it.
        var review = provider.GetRequiredService<ReviewDetectedSegments>();
        var detectedReview = new DetectedMarkerReviewViewModel(
            fileId => review.ListForFileAsync(fileId, CancellationToken.None),
            async markerId =>
            {
                var accepted = await review.AcceptAsync(markerId, CancellationToken.None).ConfigureAwait(true);
                await RefreshSessionMarkersAsync().ConfigureAwait(true);
                return accepted;
            },
            async (markerId, start, end) =>
            {
                var corrected = await review.CorrectAsync(
                        markerId,
                        start,
                        end,
                        file.TechnicalMetadata.Duration,
                        CancellationToken.None)
                    .ConfigureAwait(true);
                await RefreshSessionMarkersAsync().ConfigureAwait(true);
                return corrected;
            },
            async markerId =>
            {
                var deleted = await review.DeleteAsync(markerId, CancellationToken.None).ConfigureAwait(true);
                await RefreshSessionMarkersAsync().ConfigureAwait(true);
                return deleted;
            });
        await detectedReview.LoadAsync(mediaFileId).ConfigureAwait(true);

        // Opening an episode is what names the series worth reading. The run yields while this
        // playback is active, so the work lands in the quiet time after watching.
        if (episodeEntry is not null)
        {
            provider.GetRequiredService<SegmentDetectionScheduler>()
                .Schedule(episodeEntry.ShowId, seriesId);
        }

        // The engine has already decided the output path and whether acceleration is in force by the
        // time the media is open; nothing was passing that on, so the overlay stayed blank through a
        // real playback while LibVLC decoded on the GPU. Walking the packaged artifact is what showed
        // it: reaching a surface and feeding it are different questions.
        var videoStatus = new VideoStatusViewModel();
        var capabilities = provider.GetRequiredService<LibVlcMediaPlayerEngine>().Capabilities;
        videoStatus.Apply(
            capabilities,
            capabilities is { HardwareAccelerationRequested: true, HardwareAccelerationActive: false });

        // The live session offers its other versions (VSW-A01): the action exists only when the
        // playing title has a version group, and it goes through the use case that writes the old
        // position first and asks before a transfer the policy will not make alone. After a switch
        // the surfaces are rebuilt for the file now playing — the freshly recorded state is what
        // the reopened session resumes from.
        var versionGroups = provider.GetRequiredService<IMediaVersionGroupRepository>();
        var versionGroup = await versionGroups
                .FindByContentKeyAsync(content.Value, cancellationToken)
                .ConfigureAwait(true)
            ?? await versionGroups
                .FindByMemberAsync(mediaFileId, cancellationToken)
                .ConfigureAwait(true);
        MediaVersion? pendingSwitchTarget = null;
        VersionSwitchViewModel versionSwitch = null!;

        async Task SwitchToVersionAsync(MediaVersion target, bool confirmed, bool restartFromZero)
        {
            var switched = await provider.GetRequiredService<SwitchMediaVersion>()
                .ExecuteAsync(
                    new SwitchMediaVersionCommand(
                        content,
                        target,
                        StructureIsCompatible: true,
                        Confirmed: confirmed,
                        RestartFromZero: restartFromZero),
                    CancellationToken.None)
                .ConfigureAwait(true);
            if (switched is { Opened: false, Failure: null, Decision.Kind: ProgressTransferKind.Confirm })
            {
                pendingSwitchTarget = target;
                versionSwitch.Apply(switched.Decision);
                return;
            }

            if (switched.Failure is { } failure)
            {
                player.ApplySessionState(PlaybackState.Failed, failure);
                return;
            }

            if (switched.Opened && provider.GetRequiredService<ShellHost>().Shell is { } shell)
            {
                await shell.OpenPlayerAsync(
                        new PlayDetailsRequest(target.MediaFileId, switched.Decision.Position),
                        CancellationToken.None)
                    .ConfigureAwait(true);
            }
        }

        versionSwitch = new VersionSwitchViewModel(async choice =>
        {
            var target = pendingSwitchTarget;
            pendingSwitchTarget = null;
            if (target is null || choice == VersionSwitchChoice.Cancel)
            {
                return;
            }

            await SwitchToVersionAsync(
                    target,
                    confirmed: true,
                    restartFromZero: choice == VersionSwitchChoice.Restart)
                .ConfigureAwait(true);
        });
        versions = versionGroup is null
            ? null
            : new PlayerVersionsViewModel([.. versionGroup.Versions
                .Where(version => version.MediaFileId != mediaFileId)
                .Select(version => new PlayerVersionRowViewModel(
                    version,
                    versionSwitch,
                    target => SwitchToVersionAsync(target, confirmed: false, restartFromZero: false)))]);

        // The same rows twice on purpose: the side column and the recovery button's flyout must
        // never disagree about what the alternatives are.
        player.Versions = versions;

        return new PlayerSurfaces
        {
            Player = player,
            // What the card that pressed Play called it. An episode chained into by the countdown
            // has no card behind it, so the entry's own title stands in — and a session started from
            // a file outside the library has neither, which is why the header's middle is absent
            // rather than empty.
            Title = request.Title ?? episodeEntry?.Title,
            Subtitle = request.Subtitle,
            Tracks = tracks,
            AudioOutput = audioOutput,
            Markers = markers,
            DetectedReview = detectedReview,
            // The prompt's buttons reach the running session: restart seeks to zero, resume to the
            // stored point. Without the handler either button only hides the prompt.
            //
            // And there is no prompt at all when the caller named a position, because the caller
            // already answered the question this asks. «Al hacer click en continuar vuelve a pedir
            // confirmación de continuar o volver a ver desde el inicio en la vista del reproductor»,
            // 2026-08-25 — the session opened at the right minute and then offered to decide the
            // minute, over a picture already playing. The same silence is what «desde el principio»
            // needs, and for the same reason: an offer to resume over a film somebody just asked to
            // start again is the opposite of what they pressed.
            //
            // startPosition above already reads request.StartPosition first; this is the other half
            // of that decision, and the two are written next to the value they share on purpose —
            // one of them was made and the other was not, which is exactly how this defect got in.
            Resume = request.StartPosition is null
                ? new ResumePromptViewModel(resume, (_, position) =>
                    provider.GetRequiredService<ControlPlayback>().SeekAsync(position, CancellationToken.None))
                : null,
            Skip = skip,
            NextEpisode = nextEpisode,
            VersionSwitch = versionSwitch,
            Versions = versions,
            VideoStatus = videoStatus,
            LooseFile = provider.GetRequiredService<LooseFileViewModel>(),
        };
    }

    /// <summary>
    /// The client the metadata provider uses. It exists so the provider has somewhere to send the
    /// requests a person asked for, and it can reach exactly one declared host.
    /// </summary>
    private static HttpClient CreateProviderClient() => new()
    {
        BaseAddress = new Uri("https://api.themoviedb.org/"),
        Timeout = TimeSpan.FromSeconds(20),
    };

    /// <summary>
    /// The client artwork comes down over. Its own, and not the metadata provider's, because the two
    /// reach different declared hosts and a base address that names one of them must not be able to
    /// carry a request to the other.
    /// </summary>
    private static HttpClient CreateArtworkClient() => new()
    {
        BaseAddress = new Uri($"https://{PosterAddressPolicy.Host}/"),
        Timeout = TimeSpan.FromSeconds(20),
    };

    /// <summary>
    /// The clients the updater uses. Redirects are not followed automatically on purpose: the
    /// downloader follows them itself so that every hop can be required to stay on HTTPS, which is a
    /// promise about the address the bytes actually come from rather than the one that was published.
    /// </summary>
    private static HttpClient CreateUpdateClient(Uri? baseAddress) =>
        new(new HttpClientHandler { AllowAutoRedirect = false }, disposeHandler: true)
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromMinutes(10),
        };

    /// <summary>
    /// Which build this is, read from the process rather than configured. A release for another
    /// architecture installs and then fails to start, and by then the working copy is gone.
    /// </summary>
    private static string GetRuntimeIdentifier() =>
        System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;

    /// <summary>
    /// Opens the verified package the way a person would, which on Windows 11 hands it to the App
    /// Installer. Nothing is composed and no argument is passed: the file is the whole instruction,
    /// and what replaces the running application is Windows rather than this process.
    /// </summary>
    /// <remarks>
    /// The null check is the whole answer, and it was measured on both machines that matter. Where
    /// nothing is registered for `.msix` — a clean Windows with no App Installer — this returns null
    /// without throwing, and the update is refused. Where an App Installer exists it returns a
    /// process and the installer appears, so a refusal cannot be reported while an install is
    /// starting. Both halves are archived in docs/evidence/stable/updater-handover.json.
    /// </remarks>
    /// <summary>
    /// Ends the application, when there is a desktop lifetime to end.
    /// </summary>
    /// <remarks>
    /// It lives here rather than inside <see cref="WindowsSystemHandoff"/> because it cannot be
    /// exercised anywhere else: <see cref="IClassicDesktopStyleApplicationLifetime"/> carries a
    /// member declaring itself not implementable by user code, so nothing can stand in for one, and
    /// shutting a real one down inside a suite would end the suite. This file is where the rest of
    /// the application's dealings with Avalonia's lifetime already are.
    /// </remarks>
    private static void ShutdownDesktop()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    /// <summary>
    /// The language metadata is asked for, with English behind it. A provider that has nothing in the
    /// interface language should answer in one somebody can still read.
    /// </summary>
    private static MetadataLanguage CurrentMetadataLanguage()
    {
        var current = System.Globalization.CultureInfo.CurrentUICulture.Name;
        return new MetadataLanguage(
            string.IsNullOrWhiteSpace(current) ? "en-US" : current,
            current.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? null : "en-US");
    }

    /// <summary>
    /// Holds the shell once it exists, so a title card built before it can still ask it to play. The
    /// alternative is a constructor cycle between the library and the shell that owns it.
    /// </summary>
    public sealed class ShellHost
    {
        public ShellViewModel? Shell { get; set; }
    }

    /// <summary>
    /// Applies one personal mark. Both detail surfaces route through here so a favourite means the
    /// same thing on a film and on a series.
    /// </summary>
    private static async Task ApplyPersonalActionAsync(
        SetPersonalState setPersonalState,
        ContentKey content,
        PersonalActionRequest request)
    {
        switch (request.Kind)
        {
            case PersonalActionKind.ToggleFavorite:
                _ = await setPersonalState.ToggleFavoriteAsync(content, CancellationToken.None)
                    .ConfigureAwait(true);
                break;
            case PersonalActionKind.ToggleWatchLater:
                _ = await setPersonalState.ToggleWatchLaterAsync(content, CancellationToken.None)
                    .ConfigureAwait(true);
                break;
            default:
                _ = await setPersonalState.SetRatingAsync(content, request.Rating, CancellationToken.None)
                    .ConfigureAwait(true);
                break;
        }
    }

    /// <summary>
    /// The recovery screen, pointed at whichever copy <see cref="DatabaseStartup.FindLatestBackup"/>
    /// judged the best chance of being the one the person wants back.
    /// </summary>
    private static DatabaseRecoveryView CreateRecoveryView(
        IServiceProvider services,
        string databasePath,
        string? migrationBackupPath,
        string failureDetail)
    {
        var backupPath = DatabaseStartup.FindLatestBackup(databasePath, migrationBackupPath);
        var handoff = services.GetRequiredService<ISystemHandoff>();
        return new DatabaseRecoveryView
        {
            DataContext = new DatabaseRecoveryViewModel(
                databasePath,
                backupPath,
                failureDetail,
                action => HandleRecoveryAction(action, backupPath, handoff)),
        };
    }

    /// <summary>
    /// The two things the recovery screen can do, both of them handed to the system rather than
    /// carried out here.
    /// </summary>
    /// <remarks>
    /// Which handover is built is decided by the data root, once, in the composition: a run keeping
    /// its data somewhere of its own writes down the folder it would have shown and the shutdown it
    /// would have asked for. Before that, neither button could be pressed by anything but a person —
    /// one opened an Explorer window on whoever was measuring, and the other ended the process doing
    /// the measuring.
    /// <para>
    /// Whether there is a folder to show at all is decided here rather than in either handover, so
    /// the two cannot come to disagree about it: with no copy to offer,
    /// <see cref="DatabaseStartup.FindLatestBackup"/> answers a path that deliberately does not
    /// exist, and the folder holding it is still where a copy would be.
    /// </para>
    /// </remarks>
    private static void HandleRecoveryAction(
        DatabaseRecoveryAction action,
        string backupPath,
        ISystemHandoff handoff)
    {
        if (action == DatabaseRecoveryAction.OpenBackupFolder)
        {
            var folder = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                _ = handoff.TryOpenFolder(folder);
            }
        }
        else if (action == DatabaseRecoveryAction.Exit)
        {
            handoff.RequestExit();
        }
    }

    /// <summary>
    /// What this machine can say about itself, and nothing else. The counts are exact here and become
    /// buckets inside the builder, which is the only place allowed to decide what a number turns into.
    /// </summary>
    private static DiagnosticsInputs DescribeThisMachine(IServiceProvider provider)
    {
        // Every reading here is optional. A provider that cannot answer contributes its default and the
        // report is still produced: a diagnostic that refuses to exist because one probe failed is the
        // least useful diagnostic there is. Nothing is awaited on the UI thread's own context, because a
        // button press must not be able to freeze the window it was pressed in.
        var hdr = Read(() => provider.GetRequiredService<IDisplayCapabilityProvider>()
            .GetCurrentDisplay()
            .HdrEnabled);
        var audioEndpoints = Read(() => Task.Run(() => provider
            .GetRequiredService<IAudioDeviceCatalog>()
            .GetOutputsAsync(CancellationToken.None)).GetAwaiter().GetResult().Count);
        var roots = Read(() => Task.Run(() => provider
            .GetRequiredService<ILibraryRootRepository>()
            .ListAsync(CancellationToken.None)).GetAwaiter().GetResult().Count);

        // What the machine actually did, not what a constant claimed it would (ARQ-009). The
        // acceleration answer is the engine's own for the last media it opened — false before
        // anything has played, which is the truthful reading of "no evidence".
        var acceleration = Read(() => provider
            .GetRequiredService<LibVlcMediaPlayerEngine>()
            .Capabilities?.HardwareAccelerationActive ?? false);
        var libraryItems = Read(() =>
        {
            var summary = Task.Run(() => provider
                .GetRequiredService<IHomeReadModel>()
                .ReadLibrarySummaryAsync(CancellationToken.None)).GetAwaiter().GetResult();
            return summary.MovieCount + summary.ShowCount;
        });
        return new DiagnosticsInputs(
            GetAppVersion(),
            Environment.OSVersion.Version.ToString(),
            Environment.Version.ToString(),
            System.Globalization.CultureInfo.CurrentUICulture.Name,
            acceleration,
            hdr,
            audioEndpoints,
            libraryItems,
            roots,
            Errors: ReadRecordedErrors(provider),
            History: [],
            SearchTerms: []);
    }

    /// <summary>
    /// The failures this machine has actually written down: the rename audit is the one error
    /// record that persists, and each distinct failure arrives as a code with how often it
    /// happened. The builder buckets the counts; nothing here carries a path or a name. A probe
    /// that cannot read contributes nothing, like every other optional reading here.
    /// </summary>
    /// <summary>
    /// What went wrong, from the two places that know: the rename audit the database keeps between
    /// runs, and this session's own failures (ARQ-004) — the commands whose surface had nowhere to
    /// show a failure, and whatever reached the top of the process. Before ARQ-004 the report could
    /// only ever describe renames, which made a quiet application look like a healthy one.
    /// </summary>
    private static IReadOnlyList<DiagnosticsErrorSample> ReadRecordedErrors(IServiceProvider provider) =>
        [.. ReadRenameFailures(provider), .. provider.GetRequiredService<ISessionFailureLog>().Samples()];

    private static IReadOnlyList<DiagnosticsErrorSample> ReadRenameFailures(IServiceProvider provider)
    {
        try
        {
            return Task.Run(async () =>
            {
                var factory = provider.GetRequiredService<SqliteConnectionFactory>();
                await using var connection = await factory.OpenAsync(CancellationToken.None).ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT error, COUNT(*)
                    FROM rename_log
                    WHERE status = {(int)RenameAuditStatus.Failed} AND error IS NOT NULL
                    GROUP BY error
                    ORDER BY error;
                    """;
                await using var reader = await command.ExecuteReaderAsync(CancellationToken.None).ConfigureAwait(false);
                IReadOnlyList<DiagnosticsErrorSample> samples = await ReadSamplesAsync(reader).ConfigureAwait(false);
                return samples;
            }).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return [];
        }

        static async Task<List<DiagnosticsErrorSample>> ReadSamplesAsync(System.Data.Common.DbDataReader reader)
        {
            var samples = new List<DiagnosticsErrorSample>();
            while (await reader.ReadAsync(CancellationToken.None).ConfigureAwait(false))
            {
                samples.Add(new DiagnosticsErrorSample(
                    $"rename:{reader.GetString(0)}",
                    Exception: null,
                    reader.GetInt32(1)));
            }

            return samples;
        }
    }

    /// <summary>One optional reading, with its default when the machine cannot answer.</summary>
    private static T Read<T>(Func<T> probe)
        where T : struct
    {
        try
        {
            return probe();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return default;
        }
    }

    private static string GetAppVersion() =>
        typeof(CompositionRoot).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>
    /// Asks where the archive should go. The picker belongs to Windows, so the application never sees a
    /// folder it was not handed, and a cancelled dialog simply means nothing is exported.
    /// </summary>
    private static async Task<string?> ChooseArchiveDestinationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Avalonia.Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return null;
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = ReadResource("BackupExportLabel"),
            SuggestedFileName = $"apsolutions-localmedia-{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}.zip",
            DefaultExtension = "zip",
            ShowOverwritePrompt = true,
        }).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }

    /// <summary>
    /// Asks which archive to restore from. As with the export, the folder comes from the Windows picker
    /// and never from anything the application composed on its own.
    /// </summary>
    private static async Task<string?> ChooseArchiveSourceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Avalonia.Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = ReadResource("RestoreChooseArchiveLabel"),
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("ZIP") { Patterns = ["*.zip"] }],
        }).ConfigureAwait(true);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <summary>
    /// Asks which folder to catalogue. The Windows picker starts at the user's Videos library —
    /// on a first run there is no "last used" to start from — and a cancelled dialog answers null,
    /// which adds nothing.
    /// </summary>
    private static async Task<string?> ChooseMediaFolderDialogAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Avalonia.Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return null;
        }

        var start = await window.StorageProvider
            .TryGetWellKnownFolderAsync(WellKnownFolder.Videos).ConfigureAwait(true);
        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = ReadResource("AddRootBrowseDialogTitle"),
            AllowMultiple = false,
            SuggestedStartLocation = start,
        }).ConfigureAwait(true);
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    /// <summary>
    /// A folder's kind, which is <see cref="RootKindPolicy"/>'s decision: the only thing Windows
    /// contributes is the one question a policy cannot ask, what type the drive is.
    /// </summary>
    private static RootKind DetectRootKind(string path) =>
        RootKindPolicy.Detect(path, root => new DriveInfo(root).DriveType);

    /// <summary>
    /// The path Windows would launch at sign-in. It is read from the running process rather than
    /// composed, so a moved installation registers the place it actually lives.
    /// </summary>
    private static string GetExecutablePath() =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "ApSolutions.LocalMedia.Windows.exe");

    /// <summary>How far one press of the volume keys moves the level.</summary>
    private const int VolumeStepPercent = 5;

    /// <summary>
    /// What each input command does to the live session. Keyboard and media keys arrive here
    /// through the same router, so each action happens once whichever origin produced it. Play,
    /// pause, and stop go through the same commands the on-screen buttons use — their guards and
    /// their state changes included — and the window modes go through the shell that owns them.
    /// </summary>
    private static async Task ExecutePlaybackInputAsync(
        IServiceProvider provider,
        PlayerViewModel player,
        TransportControlsViewModel transport,
        PlaybackInputCommand command,
        CancellationToken cancellationToken)
    {
        var control = provider.GetRequiredService<ControlPlayback>();
        var shell = provider.GetRequiredService<ShellHost>().Shell;
        switch (command)
        {
            case PlaybackInputCommand.PlayPause:
                (player.CanPause ? player.PauseCommand : player.ResumeCommand).Execute(null);
                break;
            case PlaybackInputCommand.Stop:
                player.StopCommand.Execute(null);
                break;
            case PlaybackInputCommand.SkipBackward:
                _ = await control.SkipBackwardAsync(cancellationToken).ConfigureAwait(true);
                break;
            case PlaybackInputCommand.SkipForward:
                _ = await control.SkipForwardAsync(cancellationToken).ConfigureAwait(true);
                break;
            case PlaybackInputCommand.VolumeUp:
                await transport.SetVolumeAsync(transport.VolumePercent + VolumeStepPercent, cancellationToken)
                    .ConfigureAwait(true);
                break;
            case PlaybackInputCommand.VolumeDown:
                await transport.SetVolumeAsync(transport.VolumePercent - VolumeStepPercent, cancellationToken)
                    .ConfigureAwait(true);
                break;
            case PlaybackInputCommand.ToggleMute:
                transport.ToggleMuteCommand.Execute(null);
                break;
            case PlaybackInputCommand.ToggleFullscreen when shell is not null:
                await shell.TogglePlaybackModeAsync(PlaybackMode.Fullscreen, cancellationToken)
                    .ConfigureAwait(true);
                break;
            case PlaybackInputCommand.ToggleMiniPlayer when shell is not null:
                await shell.TogglePlaybackModeAsync(PlaybackMode.Mini, cancellationToken)
                    .ConfigureAwait(true);
                break;
            case PlaybackInputCommand.ExitOverlayMode when shell is not null:
                await shell.TogglePlaybackModeAsync(PlaybackMode.Embedded, cancellationToken)
                    .ConfigureAwait(true);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Shows in the panel whichever tracks the engine now has in force.
    /// </summary>
    /// <remarks>
    /// Loading never stores a preference — the view model says so itself — so following the engine
    /// can never be mistaken for somebody's choice, which is the one thing that would make this
    /// worse than the stale panel it replaces.
    /// </remarks>
    private static async Task FollowActiveTracksAsync(
        IMediaPlayerEngine engine,
        TrackSelectorViewModel? tracks)
    {
        if (tracks is null)
        {
            return;
        }

        var snapshot = await engine.GetSnapshotAsync(CancellationToken.None).ConfigureAwait(true);
        tracks.Load(
            snapshot.Tracks,
            Announced(snapshot, snapshot.ActiveAudioTrackId),
            Announced(snapshot, snapshot.ActiveSubtitleTrackId));

        static MediaTrack? Announced(PlaybackSnapshot snapshot, string? trackId) =>
            trackId is null
                ? null
                : snapshot.Tracks.FirstOrDefault(track =>
                    string.Equals(track.Id, trackId, StringComparison.Ordinal));
    }

    /// <summary>
    /// The subtitle files sitting beside one media file, confined to the root that holds it.
    /// </summary>
    /// <remarks>
    /// <see cref="ExternalSubtitleDiscovery"/> was written, confined to its root, tested in both
    /// encodings — and called by nobody: the session handed
    /// <see cref="PlaybackPreferenceContext.ExternalSubtitlePaths"/> an empty list on every open, so
    /// a <c>.srt</c> beside an episode was never loaded. The house defect, found again on 2026-08-25
    /// while chasing subtitles that VLC showed and this did not.
    /// <para>
    /// The root is the one that contains the file; a file under none of the roots gets no
    /// candidates at all, which keeps the confinement the discovery already enforces rather than
    /// trusting the media's own directory.
    /// </para>
    /// </remarks>
    private static async Task<IReadOnlyList<string>> DiscoverExternalSubtitlesAsync(
        IServiceProvider provider,
        string mediaPath,
        CancellationToken cancellationToken)
    {
        var roots = await provider.GetRequiredService<ILibraryRootRepository>()
            .ListAsync(cancellationToken)
            .ConfigureAwait(true);
        return
        [
            .. roots
                .SelectMany(root => ExternalSubtitleDiscovery.Discover(mediaPath, root.Path))
                .Select(subtitle => subtitle.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// Releases the hardware key registrations without letting a teardown throw: losing the
    /// registrations at exit is recoverable, taking the session teardown down is not.
    /// </summary>
    private static async Task StopMediaKeysQuietlyAsync(IMediaKeySource mediaKeys)
    {
        try
        {
            await mediaKeys.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Observed on purpose.
        }
    }

    /// <summary>
    /// What the overlay's buttons do: both stop the wait, and "play now" remembers that the person
    /// wants the episode without it.
    /// </summary>
    private static Task HandleNextEpisodeActionAsync(ApplicationHost host, NextEpisodeAction action)
    {
        if (host.NextEpisode is { } offer)
        {
            offer.PlayNowRequested = action == NextEpisodeAction.PlayNow;
            offer.Countdown.Cancel();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Offers the next episode when one just ended. The countdown itself is T28's tested use case —
    /// cancelable, configurable, revalidating the file at zero — and the shell opens whatever was
    /// chosen so the new session gets its tracker, markers, and tracks like any other.
    /// </summary>
    private static async Task OfferNextEpisodeAsync(
        ApplicationHost host,
        IServiceProvider provider,
        EpisodeSequenceEntry currentEpisode,
        NextEpisodeViewModel overlay)
    {
        var countdown = provider.GetRequiredService<StartNextEpisodeCountdown>();
        if (countdown.CountdownSeconds == 0)
        {
            // Zero is the off switch: no offer, no wait, nothing on screen.
            return;
        }

        var shell = provider.GetRequiredService<ShellHost>().Shell;
        var candidate = await provider.GetRequiredService<GetNextEpisode>()
            .ExecuteAsync(currentEpisode.ShowId, currentEpisode.Id, CancellationToken.None)
            .ConfigureAwait(true);
        if (candidate is null)
        {
            // Nothing after this episode: the shell returns to the details, which is the outcome
            // T28 wrote down for the end of a season.
            if (shell is not null)
            {
                await shell.ClosePlayerAsync(CancellationToken.None).ConfigureAwait(true);
            }

            return;
        }

        overlay.Offer($"T{candidate.SeasonNumber} E{candidate.EpisodeNumber}", countdown.CountdownSeconds);
        var offer = new ApplicationHost.NextEpisodeOffer(countdown);
        host.NextEpisode = offer;
        void OnTicked(object? sender, int remaining) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => overlay.Tick(remaining));
        countdown.Ticked += OnTicked;
        NextEpisodeResult result;
        try
        {
            result = await countdown
                .ExecuteAsync(currentEpisode.ShowId, currentEpisode.Id, CancellationToken.None)
                .ConfigureAwait(true);
        }
        finally
        {
            countdown.Ticked -= OnTicked;
            host.NextEpisode = null;
        }

        overlay.Hide();
        var chosen = result.Outcome == NextEpisodeOutcome.Started
            || (result.Outcome == NextEpisodeOutcome.Cancelled && offer.PlayNowRequested);
        if (chosen && result.Episode is { MediaFileId: { } nextFileId })
        {
            // The shell rebuilds the session's surfaces — tracker, markers, tracks, resume — around
            // the chosen episode. When the countdown already opened it through the coordinator, the
            // open below replaces that session with the same file; the brief double open is the
            // price of keeping T28's revalidation-at-zero exactly as tested.
            if (shell is not null)
            {
                await shell.OpenPlayerAsync(
                        new PlayDetailsRequest(nextFileId, TimeSpan.Zero),
                        CancellationToken.None)
                    .ConfigureAwait(true);
            }
        }
        else if (result.Outcome == NextEpisodeOutcome.Unavailable && shell is not null)
        {
            // The file stopped being playable while the countdown ran; the shell returns to the
            // details rather than pretending.
            await shell.ClosePlayerAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// The periodic write, kept from taking the process down: a failing flush surfaces at the next
    /// critical trigger, and the loop's death must never be the reason a session stops saving.
    /// </summary>
    private static async Task RunProgressLoopAsync(PlaybackProgressTracker tracker, CancellationToken cancellationToken)
    {
        try
        {
            await tracker.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Observed on purpose: an unobserved faulted task here would resurface as an unhandled
            // exception on finalisation, long after the session that caused it.
        }
    }

    /// <summary>A flush fired from an engine event, which must never throw into the event source.</summary>
    private static async Task FlushQuietlyAsync(PlaybackProgressTracker tracker, PersistenceTrigger trigger)
    {
        try
        {
            _ = await tracker.FlushAsync(trigger, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The close flush remains the last line of defence; a failed opportunistic write is
            // strictly better than a crashed event handler.
        }
    }

    private static string ReadResource(string key) =>
        Avalonia.Application.Current is { } application
        && application.TryGetResource(key, application.ActualThemeVariant, out var value)
            ? value?.ToString() ?? key
            : key;
}
