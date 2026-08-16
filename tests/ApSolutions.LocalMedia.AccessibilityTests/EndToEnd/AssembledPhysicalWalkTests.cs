// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Lifecycle;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Presentation.Theme;
using ApSolutions.LocalMedia.TestSupport;
using ApSolutions.LocalMedia.Windows;
using ApSolutions.LocalMedia.Windows.Metadata;
using ApSolutions.LocalMedia.Windows.Shell;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// The physical walk of the assembled artifact, as far as a headless harness can carry it.
/// <para>
/// Every scene here plays against the application the composition root builds — the same assembly
/// the package seals — with real files on a real disk, a real SQLite catalogue, and the real LibVLC
/// engine decoding real frames. Nothing is stubbed and nothing is built by the test except the
/// media files themselves, which come from FFmpeg's synthetic generators. What headless cannot
/// prove — a picture on a physical screen, TMDB answering over the network — is written down as
/// the ten-minute script in docs/evidence/stable/audit-walkthrough.md.
/// </para>
/// </summary>
[Collection(AssembledShellSuites.Name)]
public sealed class AssembledPhysicalWalkTests : IDisposable
{
    private readonly string _dataRoot = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        $"walk-{Guid.NewGuid():N}");

    /// <summary>
    /// What a scene's shutdown threw, raised here rather than where it happened. See
    /// <see cref="ShellHost.Dispose"/>: a throw inside the using replaces the scene's own failure.
    /// </summary>
    private readonly List<Exception> _teardownFailures = [];

    public void Dispose()
    {
        // The startup entry a scene may have consented to. It is under this run's own key rather than
        // the one Windows reads at sign-in, and it still does not outlive the run that wrote it.
        Registry.CurrentUser.DeleteSubKeyTree(
            new AppDataPaths(_dataRoot).StartupRegistrySubKey,
            throwOnMissingSubKey: false);

        // The watcher lets go of its directory handle asynchronously on the close path; the delete
        // retries rather than racing it.
        for (var attempt = 0; attempt < 5 && Directory.Exists(_dataRoot); attempt++)
        {
            try
            {
                Directory.Delete(_dataRoot, recursive: true);
            }
            catch (IOException)
            {
                Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(200);
            }
        }

        // Raised after the scene has reported its own result, so a shutdown that goes wrong is still
        // a failure and no longer speaks over the one the walk was written to find.
        if (_teardownFailures.Count > 0)
        {
            throw new AggregateException(
                "The assembled application failed to shut down after the scene finished.",
                _teardownFailures);
        }
    }

    /// <summary>
    /// LIB-002/003 and LIB-008 as lived, not as wired: the watchers start with the window the way
    /// <c>ConfigureWindow</c> starts them for a person, the startup scan catalogues what was already
    /// in the folder, a file dropped afterwards is catalogued with nobody pressing anything, and the
    /// two copies reach one version group that the card can open.
    /// </summary>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task A_dropped_copy_is_catalogued_by_the_watching_application_and_groups_with_the_first()
    {
        var sample = await RequireSampleAsync("walk-copy.mp4", durationSeconds: 3);
        var watched = Path.Combine(_dataRoot, "watched");
        Directory.CreateDirectory(watched);
        File.Copy(sample, Path.Combine(watched, "Dune.2021.1080p.mp4"));
        var factory = await SeedRootAsync(watched, ScanPolicy.Startup | ScanPolicy.Continuous);

        using var host = ShowShell();
        host.Application.ConfigureWindow(host.Window);

        await WaitForAsync(
            async () => await CountAsync(factory, "media_files") == 1,
            "the startup scan never catalogued the copy that was already there");

        File.Copy(sample, Path.Combine(watched, "Dune.2021.720p.mp4"));
        await WaitForAsync(
            async () => await CountAsync(factory, "media_files") == 2,
            "the watcher never catalogued the copy dropped after the window opened");
        await WaitForAsync(
            async () => await CountAsync(factory, "media_version_groups") >= 1,
            "the watcher's scan never grouped the two copies");

        // The group is reachable from a card, which is where a person meets it.
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(library.Items);
        await library.OpenDetailsAsync(library.Items[0], TestContext.Current.CancellationToken);
        await host.ViewModel.OpenDuplicatesAsync(TestContext.Current.CancellationToken);
        Assert.True(host.ViewModel.HasDuplicates, "The two copies never became a group a card can open.");
    }

    /// <summary>
    /// PLY-014 and BUG-008 as lived: a real video decoding through the session the card opened, the
    /// space bar pausing and resuming it through the assembled chain, and a marker saved mid-session
    /// making the skip offer appear on the playhead without closing and reopening anything.
    /// </summary>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_keys_pause_the_playing_video_and_a_marker_saved_mid_session_offers_the_skip()
    {
        var sample = await RequireSampleAsync("walk-feature.mp4", durationSeconds: 8);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var mediaPath = Path.Combine(media, "Arrival.2016.mp4");
        File.Copy(sample, mediaPath);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var fileId = await SeedMediaFileAsync(factory, media, mediaPath, TimeSpan.FromSeconds(8));

        using var host = ShowShell();
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        await library.OpenDetailsAsync(Assert.Single(library.Items), TestContext.Current.CancellationToken);
        library.MovieDetails.PlayCommand.Execute(null);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the session never reached the playing state on the real engine");

        // The keyboard operates the session through the assembled chain: view → shared map →
        // router → coordinator → engine.
        var playerView = host.Shell.GetVisualDescendants().OfType<PlayerView>().First();
        Assert.True(playerView.Focus(), "The player surface refused the focus.");
        host.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPaused == true),
            "the space bar never paused the playing session");

        // The router coalesces one command arriving twice within 250 ms — the media-key rule — so
        // the second press waits the way a person's second press does.
        await Task.Delay(300, TestContext.Current.CancellationToken);
        host.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the space bar never resumed the paused session");

        // A marker made mid-playback reaches the skip offer without reopening: the save recomposes
        // the session's ranges and the next position event applies them.
        var surfaces = host.ViewModel.Player;
        Assert.NotNull(surfaces?.Markers);
        Assert.NotNull(surfaces?.Skip);
        Assert.False(surfaces!.Skip!.IsVisible, "The skip offer was on screen before any marker existed.");
        surfaces.Markers!.SelectedKind = MarkerKind.Intro;
        surfaces.Markers.StartSeconds = 0;
        surfaces.Markers.EndSeconds = 7;
        surfaces.Markers.SaveCommand.Execute(null);
        await WaitForAsync(
            () => Task.FromResult(surfaces.Skip.IsVisible),
            "the marker saved mid-session never surfaced the skip offer on the playhead");

        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// PLY-011 as lived: the first episode decodes to its own end, the engine's ended state raises
    /// the offer with the next episode's name, and "play now" chains the session onto the second
    /// file — two episodes, one sitting, no hands.
    /// </summary>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_end_of_an_episode_offers_the_next_and_play_now_chains_the_session()
    {
        var sample = await RequireSampleAsync("walk-episode.mp4", durationSeconds: 3);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var first = Path.Combine(media, "Show.S01E01.mp4");
        var second = Path.Combine(media, "Show.S01E02.mp4");
        File.Copy(sample, first);
        File.Copy(sample, second);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var duration = TimeSpan.FromSeconds(3);
        var firstId = await SeedMediaFileAsync(factory, media, first, duration);
        var secondId = await SeedMediaFileAsync(factory, media, second, duration);
        _ = await SeedSeriesAsync(factory, firstId, secondId);

        using var host = ShowShell();
        await host.ViewModel.OpenPlayerAsync(
            new PlayDetailsRequest(new MediaFileId(firstId), TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the first episode never reached the playing state on the real engine");

        var overlay = host.ViewModel.Player?.NextEpisode;
        Assert.NotNull(overlay);
        await WaitForAsync(
            () => Task.FromResult(overlay!.IsVisible),
            "the end of the first episode never raised the next-episode offer");
        Assert.Equal("T1 E2", overlay!.EpisodeLabel);

        overlay.PlayNowCommand.Execute(null);
        await WaitForAsync(
            () => Task.FromResult(
                host.ViewModel.Player?.Player.MediaPath.EndsWith("Show.S01E02.mp4", StringComparison.Ordinal) == true
                && host.ViewModel.Player.Player.IsPlaying),
            "accepting the offer never chained the session onto the second episode");

        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// LIB-006 as lived, and with the mouse: a title somebody identified, its card opened, its
    /// editor opened, and "Refresh from provider" <em>clicked</em> — after which the entry shows
    /// what the provider says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The click is the point. This walk already drove the assembled application with
    /// <c>Window.KeyPress</c>, but nothing in the repository used
    /// <c>Avalonia.Headless</c>'s mouse input, and that gap is how a pair of buttons that were
    /// visible, enabled and incapable of doing anything survived a whole audit.
    /// </para>
    /// <para>
    /// The provider answers out of its own cache, which is the shipped path exactly: with no token
    /// configured, <c>TmdbMetadataProvider</c> serves what it has stored and opens no connection. So
    /// the real provider, the real parser and the real repository all take part — the harness only
    /// puts the payload where a previous lookup would have left it.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task Clicking_refresh_on_an_identified_title_brings_the_provider_into_the_entry()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var mediaPath = Path.Combine(media, "Arrival.2016.mp4");

        // No decoding happens here, so the file only has to exist and be catalogued.
        await File.WriteAllBytesAsync(mediaPath, [0x41, 0x50], TestContext.Current.CancellationToken);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var fileId = await SeedMediaFileAsync(factory, media, mediaPath, TimeSpan.FromMinutes(116));
        await SeedIdentifiedTitleAsync(factory, fileId);

        // Tall enough for the editor to be on screen without scrolling. A person with a smaller
        // window scrolls to it; the click is what this walk is here to prove, not the scrolling.
        using var host = ShowShell(height: 2000);
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        await library.OpenDetailsAsync(Assert.Single(library.Items), TestContext.Current.CancellationToken);
        await host.ViewModel.OpenMetadataEditorAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        host.Window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        var editor = host.ViewModel.MetadataEditor;
        Assert.NotNull(editor);
        Assert.NotEqual("La llegada", editor!.Title);

        // The control that makes the click mean something: pressing beside the button changes
        // nothing, so what changes afterwards can only have come from pressing the button itself.
        // Anchored on the key rather than on the x:Name the button also carries: the key is what the
        // views declare and therefore what the coverage gate counts, and a control that answers to
        // two names would otherwise be pressed under one and reported missing under the other.
        await PressAsync(
            host,
            "MetadataRefreshAction",
            () => editor.Title,
            "clicking Refresh from provider never brought the provider's answer into the editor");
        Assert.Equal("La llegada", editor.Title);

        Assert.Equal("Una lingüista traduce a los visitantes.", editor.Overview);
        Assert.False(editor.IsUnidentified);
        Assert.False(editor.HasNoProviderAnswer);

        // And it is stored, not merely on screen: the entry a person reopens tomorrow says the same.
        var stored = await new CatalogMetadataRepository(factory).GetAsync(
            new TitleId(fileId),
            TestContext.Current.CancellationToken);
        Assert.Equal("La llegada", stored?.Metadata.Title);
        Assert.NotNull(stored?.RefreshedUtc);

        // The lock beside the title has no x:Name — like 69 of the 129 command controls — so it is
        // found by the resource key behind its accessible name. This is the measurement that decides
        // whether the walk can cover the whole application without adding a name to every control.
        Assert.False(editor.LockTitle);
        await PressAsync(
            host,
            "MetadataLockTitle",
            () => editor.LockTitle,
            "clicking the title lock never locked the title");
        Assert.True(editor.LockTitle);

        // The other six locks, each one pressed and each one read back. They are the fields a person
        // protects from the next refresh, so a lock that looks set and is not would hand somebody
        // else's data back over their own work.
        foreach (var (anchor, read) in new (string Anchor, Func<bool> Read)[]
        {
            ("MetadataLockOriginalTitle", () => editor.LockOriginalTitle),
            ("MetadataLockOverview", () => editor.LockOverview),
            ("MetadataLockReleaseYear", () => editor.LockReleaseYear),
            ("MetadataLockGenres", () => editor.LockGenres),
            ("MetadataLockPoster", () => editor.LockPosterPath),
            ("MetadataLockBackdrop", () => editor.LockBackdropPath),
        })
        {
            Assert.False(read(), $"{anchor} was already set before it was pressed.");
            await PressAsync(host, anchor, read, $"clicking {anchor} never set it");
            Assert.True(read());
        }

        // Save is the one whose effect is not on screen: it writes a row. Asserting on the editor
        // would prove only that the editor kept what was typed into it.
        var metadata = new CatalogMetadataRepository(factory);
        editor.Title = "La llegada (edición personal)";
        Dispatcher.UIThread.RunJobs();
        await PressAsync(
            host,
            "MetadataSaveAction",
            async () => (await metadata.GetAsync(new TitleId(fileId), TestContext.Current.CancellationToken))
                ?.Metadata.Title,
            "clicking Save never wrote the edited title to the catalogue");
        Assert.Equal(
            "La llegada (edición personal)",
            (await metadata.GetAsync(new TitleId(fileId), TestContext.Current.CancellationToken))?.Metadata.Title);

        // And Restore puts the provider's answer back over it, which is the whole point of having
        // edited by hand being reversible.
        await PressAsync(
            host,
            "MetadataRestoreAction",
            () => editor.Title,
            "clicking Restore never brought the provider's title back over the edited one");
        Assert.Equal("La llegada", editor.Title);
    }

    /// <summary>
    /// The fourth batch, first half: the preferences that change what the application <em>is</em> —
    /// its theme, its language, whether it watches local folders, and whether it looks for repeated
    /// audio between episodes. Seven controls, pressed with the mouse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Settings is the first page of the walk that does not fit in the window: 3680 pixels of it in a
    /// window of 2000. That is what <see cref="Reveal"/> exists for, and this scene is what measured
    /// the need — pressing down the page used to strand every control above it.
    /// </para>
    /// <para>
    /// The three theme buttons go <b>last</b>, and that order is a measurement rather than a
    /// preference. Applying a theme rebuilds the resources the whole page is drawn from, and after
    /// it a click at the position the layout reports no longer reaches a control that sits above the
    /// one just pressed: on 2026-08-16 the English button answered a press before the themes and
    /// refused eight of them afterwards, at the same point, with the page scrolled to the top both
    /// times. Whatever the theme leaves behind, the walk presses everything else before it.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_preferences_that_change_the_application_are_pressed_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        _ = await SeedRootAsync(media, ScanPolicy.Manual);

        using var host = ShowShell(height: 2000);
        Navigate(host, AppRoute.Settings);

        var appearance = host.ViewModel.AppearanceSettings;
        Assert.NotNull(appearance);
        Assert.Equal(ThemePreference.System, appearance!.CurrentPreference);

        // The language buttons reload every string in the application, including the ones the walk
        // finds its controls by. That is fine — the anchor is resolved against whatever is loaded
        // now — but the pair is pressed together so the rest of the scene runs in one language.
        await PressAsync(
            host,
            "LanguageEnglish",
            () => appearance.CurrentLanguage,
            "clicking English never changed the language");
        Assert.Equal("en", appearance.CurrentLanguage);

        await PressAsync(
            host,
            "LanguageSpanish",
            () => appearance.CurrentLanguage,
            "clicking Spanish never changed the language back");
        Assert.Equal("es", appearance.CurrentLanguage);

        // Each theme is asked for from a state that is not already it, which is what makes the
        // answer mean something: light from system, dark from light, and system back from dark.
        await PressAsync(
            host,
            "ThemeLight",
            () => appearance.CurrentPreference,
            "clicking the light theme never changed the preference");
        Assert.Equal(ThemePreference.Light, appearance.CurrentPreference);

        await PressAsync(
            host,
            "ThemeDark",
            () => appearance.CurrentPreference,
            "clicking the dark theme never changed the preference");
        Assert.Equal(ThemePreference.Dark, appearance.CurrentPreference);

        await PressAsync(
            host,
            "ThemeSystem",
            () => appearance.CurrentPreference,
            "clicking the system theme never gave the choice back to Windows");
        Assert.Equal(ThemePreference.System, appearance.CurrentPreference);

        var scan = host.ViewModel.ScanSettings;
        Assert.NotNull(scan);
        await PressAsync(
            host,
            "ScanSettingsWatchLocal",
            () => scan!.WatchLocalRoots,
            "clicking the local-watching box never changed whether local roots are watched");

        // The segment switch reads and writes the use case itself rather than a field of its own, so
        // the probe is the use case: a switch that only moved its own bool would look identical.
        var segments = host.ViewModel.SegmentDetection;
        Assert.NotNull(segments);
        await PressAsync(
            host,
            "SegmentDetectionSettingsEnable",
            () => host.Application.Services.GetRequiredService<DetectSeriesSegments>().IsEnabled,
            "clicking the segment-detection switch never turned the detection on");
        Assert.True(segments!.IsEnabled);
    }

    /// <summary>
    /// The fourth batch, second half so far: the lifecycle preferences — the tray, closing to it, and
    /// starting with Windows, whose consent is asked for, declined, asked for again and granted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Granting writes a real value into a real registry key, which is why this scene could not exist
    /// before: the assembled application used to name the key Windows reads at sign-in, so pressing
    /// the button would have registered whatever binary the suite had just built to start on the
    /// machine of whoever ran it. A run keeping its data somewhere of its own now keeps this
    /// somewhere of its own too, and the key is deleted when the scene ends.
    /// </para>
    /// <para>
    /// Consent is declined before it is granted, because a decline that is never exercised is a
    /// button nobody has proved says no. Asking twice is what a person does after changing their
    /// mind, and it is the only way to reach both.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_lifecycle_consents_are_given_and_taken_back_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        _ = await SeedRootAsync(media, ScanPolicy.Manual);

        using var host = ShowShell(height: 2000);
        Navigate(host, AppRoute.Settings);

        var lifecycle = host.ViewModel.LifecycleSettings;
        Assert.NotNull(lifecycle);
        Assert.False(lifecycle!.TrayEnabled);
        Assert.False(lifecycle.CanMinimizeToTray);

        // The tray first: closing to it is offered only while it exists, so pressing the second box
        // before the first would press a disabled control.
        await PressAsync(
            host,
            "LifecycleTrayLabel",
            () => lifecycle.TrayEnabled,
            "clicking the tray box never turned the tray on");
        Assert.True(lifecycle.CanMinimizeToTray);

        await PressAsync(
            host,
            "LifecycleCloseToTrayLabel",
            () => lifecycle.MinimizeToTrayOnClose,
            "clicking the close-to-tray box never changed what the close button does");

        // Asking to start with Windows grants nothing: it raises the question, and the answer is two
        // more presses away.
        var startup = host.Application.Services.GetRequiredService<IStartupService>();
        Assert.Equal(StartupEntryState.Absent, startup.Inspect());
        await PressAsync(
            host,
            "LifecycleStartupLabel",
            () => lifecycle.IsStartupConsentPending,
            "clicking the startup box never asked for the consent it needs");
        Assert.False(lifecycle.StartWithWindows);

        await PressAsync(
            host,
            "LifecycleStartupConsentDecline",
            () => lifecycle.IsStartupConsentPending,
            "clicking Decline never withdrew the pending question");
        Assert.False(lifecycle.StartWithWindows);
        Assert.Equal(StartupEntryState.Absent, startup.Inspect());

        await PressAsync(
            host,
            "LifecycleStartupLabel",
            () => lifecycle.IsStartupConsentPending,
            "asking a second time never raised the question again");

        // And Grant, whose effect is neither on the screen nor in the catalogue but in the registry:
        // asserting on the checkbox would prove the checkbox remembers being checked.
        await PressAsync(
            host,
            "LifecycleStartupConsentGrant",
            () => startup.Inspect(),
            "clicking Grant never wrote the startup entry it consents to");
        Assert.Equal(StartupEntryState.Present, startup.Inspect());
        Assert.True(lifecycle.StartWithWindows);
        Assert.False(lifecycle.IsStartupConsentPending);
    }

    /// <summary>
    /// The fourth batch: the privacy surface. Diagnostics consented to, the automatic refresh turned
    /// on, the report previewed, and the report written — the last one read off the disk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The automatic-refresh switch is only offered while a consented connection exists, because
    /// without one it would promise something that cannot happen. So the scene puts a token in the
    /// environment before the application is built and takes it out afterwards. Nothing connects:
    /// the one pass over stale entries is posted when the window is configured, which this scene
    /// never does, and turning the switch on only writes a preference.
    /// </para>
    /// <para>
    /// Export asserts on the file. The screen would say the same thing whether or not anything was
    /// written, and this is the one surface where a person has to be able to tell those apart.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_privacy_surface_consents_previews_and_writes_the_report()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        _ = await SeedRootAsync(media, ScanPolicy.Manual);

        var restore = Environment.GetEnvironmentVariable(TmdbOptions.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(TmdbOptions.EnvironmentVariableName, "walk-token");
        try
        {
            using var host = ShowShell(height: 2000);
            Navigate(host, AppRoute.Settings);

            var privacy = host.ViewModel.PrivacySettings;
            Assert.NotNull(privacy);
            Assert.False(privacy!.DiagnosticsEnabled);
            Assert.True(privacy.CanRefreshAutomatically, "The refresh switch was not even offered.");

            await PressAsync(
                host,
                "PrivacyDiagnosticsLabel",
                () => privacy.DiagnosticsEnabled,
                "clicking the diagnostics box never recorded the consent");

            await PressAsync(
                host,
                "PrivacyAutoRefreshLabel",
                () => privacy.AutomaticRefreshEnabled,
                "clicking the automatic-refresh box never turned the refresh on");

            await PressAsync(
                host,
                "PrivacyPreviewLabel",
                () => privacy.HasPreview,
                "clicking Preview never produced the text an export would write");
            Assert.NotNull(privacy.PreviewJson);

            // The disk, not the screen: the folder holds one report that was not there before.
            var diagnostics = new AppDataPaths(_dataRoot).DiagnosticsDirectory;
            await PressAsync(
                host,
                "PrivacyExportLabel",
                () => Directory.Exists(diagnostics) ? Directory.GetFiles(diagnostics).Length : 0,
                "clicking Export never wrote a report to the diagnostics folder");
            Assert.NotNull(privacy.ExportedFileName);
            Assert.Contains(
                Directory.GetFiles(diagnostics),
                file => Path.GetFileName(file) == privacy.ExportedFileName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TmdbOptions.EnvironmentVariableName, restore);
        }
    }

    /// <summary>
    /// The last of the fourth batch: the recommendation threshold and the shortcut map. Four
    /// controls, and the two that are not a checkbox — a slider and a button whose work happens in
    /// the catalogue — are the reason this batch is worth pressing at all.
    /// </summary>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_recommendation_threshold_and_the_shortcut_defaults_are_pressed_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        _ = await SeedRootAsync(media, ScanPolicy.Manual);

        using var host = ShowShell(height: 2000);
        Navigate(host, AppRoute.Settings);

        var recommendations = host.ViewModel.RecommendationSettings;
        Assert.NotNull(recommendations);
        await PressAsync(
            host,
            "RecommendationSettingsEnableLabel",
            () => recommendations!.IsEnabled,
            "clicking the recommendations box never turned them on");

        Assert.True(recommendations!.HasWatchedThreshold, "The threshold was not offered at all.");
        await PressAsync(
            host,
            "RecommendationSettingsThresholdLabel",
            () => recommendations.WatchedThresholdPercent,
            "clicking the threshold slider never moved the threshold");

        // Apply reaches the catalogue, so the probe is the fact that a pass ran rather than the
        // number it returned: with nothing watched, the honest answer is zero, and zero is what a
        // slider that did nothing would also read.
        await PressAsync(
            host,
            "RecommendationSettingsThresholdApply",
            () => recommendations.HasThresholdResult,
            "clicking Apply never recalculated anything");
        Assert.Equal(0, recommendations.RecalculatedCount);

        // The shortcut map is restored from something other than its defaults, or restoring it would
        // be indistinguishable from doing nothing.
        var shortcuts = host.ViewModel.Shortcuts;
        Assert.NotNull(shortcuts);
        Assert.True(
            shortcuts!.TryRebind(PlaybackInputCommand.PlayPause, new KeyGesture(Key.F9)),
            "The rebind this scene needs was refused.");
        Assert.Equal(PlaybackInputCommand.PlayPause, shortcuts.Resolve(new KeyGesture(Key.F9)));

        await PressAsync(
            host,
            "ShortcutSettingsRestore",
            () => shortcuts.Resolve(new KeyGesture(Key.F9)),
            "clicking Restore defaults never gave the rebound key back");
        Assert.Null(shortcuts.Resolve(new KeyGesture(Key.F9)));
        Assert.Equal(PlaybackInputCommand.PlayPause, shortcuts.Resolve(new KeyGesture(Key.Space)));
    }

    /// <summary>
    /// LIB-012 as lived: the three controls of the rename surface, pressed with the mouse, with the
    /// effect read off <b>the file system</b> rather than off the screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the scene the feature was blocked for. The surface opened and its buttons were
    /// visible and enabled, but the application asked to rename each file to the name it already
    /// had, so the plan was always empty and no press could ever move anything. Asserting on the
    /// view model would not have caught it either — what proves a rename is a file that is no longer
    /// where it was, and a file that is.
    /// </para>
    /// <para>
    /// The proposed name comes from the entry, not from the file: the catalogue says "La llegada"
    /// and 2016 while the file on disk says <c>Arrival.2016.1080p.mp4</c>, so a destination in the
    /// convention can only have been composed from what the entry knows.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_rename_surface_moves_the_file_and_puts_it_back()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var mediaPath = Path.Combine(media, "Arrival.2016.1080p.mp4");
        var renamedPath = Path.Combine(media, "La llegada (2016).mp4");

        // Nothing decodes on this surface, so the file only has to exist and be catalogued.
        await File.WriteAllBytesAsync(mediaPath, [0x41, 0x50], TestContext.Current.CancellationToken);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var fileId = await SeedMediaFileAsync(factory, media, mediaPath, TimeSpan.FromMinutes(116));
        _ = await new CatalogMetadataRepository(factory).TrySaveAsync(
            new CatalogMetadata(
                new TitleId(fileId),
                new EditableMetadata(
                    "La llegada",
                    OriginalTitle: "Arrival",
                    Overview: null,
                    ReleaseYear: 2016,
                    Genres: [],
                    PosterPath: null,
                    BackdropPath: null,
                    TrailerKey: null,
                    LockedFields: new HashSet<MetadataField>()),
                Revision: 0),
            expectedRevision: 0,
            TestContext.Current.CancellationToken);

        using var host = ShowShell(height: 2000);
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        await library.OpenDetailsAsync(Assert.Single(library.Items), TestContext.Current.CancellationToken);
        await host.ViewModel.OpenRenamePreviewAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        host.Window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        var rename = host.ViewModel.Rename;
        Assert.NotNull(rename);
        var operation = Assert.Single(rename!.Operations);
        Assert.Equal(renamedPath, operation.DestinationPath);
        Assert.True(File.Exists(mediaPath), "Asking for a preview must not move anything.");

        // Consent first, because the two buttons below stay disabled without it. Its own effect is
        // on the screen, so the probe is the box: what it unlocks is asserted right after.
        await PressAsync(
            host,
            "RenameExplicitConsent",
            () => rename.IsConfirmed,
            "clicking the consent box never recorded the consent the rename requires");
        Assert.True(rename.ExecuteCommand.CanExecute(null));

        // And the press that matters, proved by the disk: the old name is gone and the new one is
        // there. The catalogue row is untouched on purpose — the file moved, the entry did not.
        await PressAsync(
            host,
            "RenameExecuteAction",
            () => File.Exists(renamedPath),
            "clicking Rename never moved the file to the name the entry deserves");
        Assert.False(File.Exists(mediaPath), "The file was copied rather than renamed.");
        Assert.Equal(RenameExecutionOutcome.Succeeded, rename.LastOutcome);

        // Undo is offered once, and consent is asked for again because executing cleared it: a
        // second irreversible move deserves a second decision.
        Assert.False(rename.IsConfirmed);
        await PressAsync(
            host,
            "RenameExplicitConsent",
            () => rename.IsConfirmed,
            "clicking the consent box a second time never recorded the consent Undo requires");

        await PressAsync(
            host,
            "RenameUndoAction",
            () => File.Exists(mediaPath),
            "clicking Undo never put the file back under the name it had");
        Assert.False(File.Exists(renamedPath), "Undo left the renamed copy behind.");
    }

    /// <summary>
    /// The first batch of the whole-application walk: the browse surface, driven by the mouse alone.
    /// The two drop-downs open, the apply button re-runs the query the search box was given, a card
    /// opens from its own entry in the list, and the back button returns to exactly what was there.
    /// </summary>
    /// <remarks>
    /// The entry in the list is one of the two command controls in the application whose accessible
    /// name is not a resource key but its own data — so its anchor is the title the walk itself
    /// seeded, which ties the click to something this test controls rather than to a string somebody
    /// may reword. It is recorded under the shape it is declared with.
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_library_is_browsed_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);

        // No decoding happens on this surface, so the files only have to exist and be catalogued.
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        foreach (var title in new[] { "Arrival.2016.mp4", "Dune.2021.mp4" })
        {
            var path = Path.Combine(media, title);
            await File.WriteAllBytesAsync(path, [0x41, 0x50], TestContext.Current.CancellationToken);
            _ = await SeedMediaFileAsync(factory, media, path, TimeSpan.FromMinutes(116));
        }

        using var host = ShowShell();
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, library.Items.Count);

        // A drop-down's effect is that it opens: what is chosen inside it lands in a popup root of
        // its own, which is a separate top level and not this window's business.
        await PressAsync(
            host,
            "LibraryFilterLabel",
            () => Resolve(host, "LibraryFilterLabel") is ComboBox { IsDropDownOpen: true },
            "clicking the filter never opened the list of filters");
        host.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        await PressAsync(
            host,
            "LibrarySortLabel",
            () => Resolve(host, "LibrarySortLabel") is ComboBox { IsDropDownOpen: true },
            "clicking the sort order never opened the list of orders");
        host.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        // The apply button is the one that runs the query, so the search has to already say something
        // the result can be told apart by: two entries before it, one after.
        library.Search = "Dune";
        Dispatcher.UIThread.RunJobs();
        await PressAsync(
            host,
            "LibraryApplyAction",
            () => library.Items.Count,
            "clicking Apply never re-ran the query the search box was holding");
        Assert.Equal("Dune.2021", Assert.Single(library.Items).Title);

        await PressAsync(
            host,
            "Dune.2021",
            () => library.Surface,
            "clicking the entry in the list never opened its card",
            recordAs: "{Binding Title}");
        Assert.Equal(LibrarySurface.MovieDetails, library.Surface);

        await PressAsync(
            host,
            "LibraryBackAction",
            () => library.Surface,
            "clicking Back never returned to the list");
        Assert.Equal(LibrarySurface.Browse, library.Surface);

        // Coming back lands on what was there, rather than on a library reloaded from scratch.
        Assert.Equal("Dune", library.Search);
        Assert.Equal("Dune.2021", Assert.Single(library.Items).Title);
    }

    /// <summary>
    /// The first batch, second half: the film card operated by the mouse alone — the watch state
    /// marked, cleared and handed back to the automatic rules, the three personal marks made and
    /// unmade, and the film opened on the real engine by pressing Play.
    /// </summary>
    /// <remarks>
    /// Every assertion here reads the surface <b>after</b> the repository answered, not the click's
    /// hope: each of these controls hands its request to a use case and the control then shows what
    /// came back. A toggle that only flipped its own bit would pass a test that asserted on the click.
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_card_is_operated_with_the_mouse()
    {
        var sample = await RequireSampleAsync("walk-card.mp4", durationSeconds: 3);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var mediaPath = Path.Combine(media, "Arrival.2016.mp4");
        File.Copy(sample, mediaPath);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        _ = await SeedMediaFileAsync(factory, media, mediaPath, TimeSpan.FromSeconds(3));

        using var host = ShowShell(height: 1600);
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        await library.OpenDetailsAsync(Assert.Single(library.Items), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        host.Window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        var watch = library.MovieDetails.WatchStatus;
        var personal = library.MovieDetails.PersonalActions;

        await PressAsync(
            host,
            "WatchStatusMarkWatched",
            () => watch.IsWatched,
            "clicking Mark as watched never recorded the film as watched");
        Assert.True(watch.IsManualOverride);

        await PressAsync(
            host,
            "WatchStatusMarkNotStarted",
            () => watch.IsNotStarted,
            "clicking Mark as not started never took the film back to not started");

        await PressAsync(
            host,
            "WatchStatusClearOverride",
            () => watch.IsManualOverride,
            "clicking Clear never handed the state back to the automatic rules");
        Assert.False(watch.IsManualOverride);

        await PressAsync(
            host,
            "PersonalFavoriteAction",
            () => personal.IsFavorite,
            "clicking Favourite never recorded the film as a favourite");
        Assert.True(personal.IsFavorite);

        await PressAsync(
            host,
            "PersonalWatchLaterAction",
            () => personal.IsWatchLater,
            "clicking Watch later never recorded the film for later");
        Assert.True(personal.IsWatchLater);

        // The ten scores share one accessible name by design and are told apart by the score itself,
        // which is what a screen reader reads after the name.
        await PressAsync(
            host,
            "PersonalRatingLabel",
            () => personal.Rating,
            "clicking a score never recorded the rating",
            helpText: "7");
        Assert.Equal(7, personal.Rating);

        await PressAsync(
            host,
            "PersonalRatingClearAction",
            () => personal.HasRating,
            "clicking Clear rating never removed the score");
        Assert.Null(personal.Rating);

        // The control clicks land one control-height above their target, and on this card that strip
        // is the row above — another command control's row. If any of them had reached a neighbour,
        // these three marks would no longer be where their own presses left them. It is the check
        // that keeps "the beside click did not do what the button does" from quietly becoming "the
        // beside click did something else instead".
        Assert.True(personal.IsFavorite, "A control click reached the favourite toggle.");
        Assert.True(personal.IsWatchLater, "A control click reached the watch-later toggle.");
        Assert.False(watch.IsManualOverride, "A control click reached one of the watch-state buttons.");

        // And Play, which is where the card stops being a page and becomes a session: the real engine
        // opens the real file the walk put on the disk.
        await PressAsync(
            host,
            "MoviePlayAction",
            () => host.ViewModel.Player is not null,
            "clicking Play never opened a session on the film");
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the session Play opened never reached the playing state on the real engine");
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The provider's trailer offer (LIB-015), pressed with the mouse on both cards — and the
    /// address each press would have opened, read back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This scene is what the isolation rule was for. The press hands an address to the shell, which
    /// opens a real browser on whatever machine the suite runs on, so the control had been declared
    /// uncoverable on both cards. A run whose data root is not this machine's profile now writes the
    /// address down under that root instead, so the press can happen and — the part that matters —
    /// what it would have opened can be asserted. Nothing in this repository checked that before:
    /// every assertion stopped at the view model, because the layer past it went to a browser.
    /// </para>
    /// <para>
    /// Two cards, two different stored keys, and the addresses read back in order: one key on the
    /// film and another on the series is what tells the two presses apart. The same resource key is
    /// declared in both cards, and each is the control it is — pressing one proves nothing about the
    /// other.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_provider_trailer_link_is_pressed_on_both_cards_and_says_where_it_would_have_gone()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var film = Path.Combine(media, "Arrival.2016.mp4");
        var firstEpisode = Path.Combine(media, "Show.S01E01.mp4");
        var secondEpisode = Path.Combine(media, "Show.S01E02.mp4");

        // Nothing decodes on a card, so the files only have to exist and be catalogued.
        foreach (var path in new[] { film, firstEpisode, secondEpisode })
        {
            await File.WriteAllBytesAsync(path, [0x41, 0x50], TestContext.Current.CancellationToken);
        }

        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var filmId = await SeedMediaFileAsync(factory, media, film, TimeSpan.FromMinutes(116));
        var showId = await SeedSeriesAsync(
            factory,
            await SeedMediaFileAsync(factory, media, firstEpisode, TimeSpan.FromMinutes(42)),
            await SeedMediaFileAsync(factory, media, secondEpisode, TimeSpan.FromMinutes(42)));
        await SeedTrailerKeyAsync(factory, new TitleId(filmId), "FilmTrailer");
        await SeedTrailerKeyAsync(factory, new TitleId(showId), "ShowTrailer");

        // Where an isolated run says it puts what it would have handed to Windows. The walk reads the
        // application's own answer rather than composing a path of its own, because the rule is the
        // application's to state.
        var handoff = new AppDataPaths(_dataRoot).SystemHandoffDirectory;
        Assert.NotNull(handoff);
        var record = Path.Combine(handoff!, RecordingExternalLinkLauncher.FileName);
        string Recorded() => File.Exists(record) ? File.ReadAllText(record) : string.Empty;

        using var host = ShowShell(height: 1600);
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        await library.OpenDetailsAsync(
            library.Items.Single(item => item.Item.Id.Value == filmId),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        host.Window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
        Assert.True(
            library.MovieDetails.HasTrailerLink,
            "The film card offered no provider trailer, so there was nothing to press.");

        await PressAsync(
            host,
            "DetailsTrailerLinkAction",
            Recorded,
            "clicking the film card's trailer link never said where it would have gone");
        Assert.Equal(
            ["https://www.youtube.com/watch?v=FilmTrailer"],
            await File.ReadAllLinesAsync(record, TestContext.Current.CancellationToken));

        await library.OpenDetailsAsync(
            library.Items.Single(item => item.Item.Kind == CatalogTitleKind.Show),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        host.Window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(LibrarySurface.ShowDetails, library.Surface);
        Assert.True(
            library.ShowDetails.HasTrailerLink,
            "The series card offered no provider trailer, so there was nothing to press.");

        await PressAsync(
            host,
            "DetailsTrailerLinkAction",
            Recorded,
            "clicking the series card's trailer link never said where it would have gone");

        // Each card opened its own title's trailer, in the order they were pressed. The second line
        // is what proves the series card is a control of its own rather than the film card's.
        Assert.Equal(
            [
                "https://www.youtube.com/watch?v=FilmTrailer",
                "https://www.youtube.com/watch?v=ShowTrailer",
            ],
            await File.ReadAllLinesAsync(record, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The second batch: the player's transport, pressed with the mouse on a session the real engine
    /// is decoding. Pause and resume, both skips, mute, the volume slider, and stop.
    /// </summary>
    /// <remarks>
    /// The walk already drove this surface from the keyboard and the space bar; what it had never
    /// done is press the buttons a person with a pointing device presses, which is the one thing a
    /// keyboard route cannot stand in for.
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_players_transport_is_operated_with_the_mouse()
    {
        // Long enough to survive a forward skip: the default is thirty seconds, and on a twelve-second
        // sample the first skip ran off the end, so Stop was disabled by the time the walk reached it.
        var sample = await RequireSampleAsync("walk-transport.mp4", durationSeconds: 90);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var mediaPath = Path.Combine(media, "Transport.2024.mp4");
        File.Copy(sample, mediaPath);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var fileId = await SeedMediaFileAsync(factory, media, mediaPath, TimeSpan.FromSeconds(90));

        using var host = ShowShell(height: 1200);
        await host.ViewModel.OpenPlayerAsync(
            new PlayDetailsRequest(new MediaFileId(fileId), TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the session never reached the playing state on the real engine");
        Dispatcher.UIThread.RunJobs();
        host.Window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        var player = host.ViewModel.Player!.Player;
        var transport = player.Transport;
        Assert.NotNull(transport);

        await PressAsync(
            host,
            "PlayerPauseAction",
            () => player.IsPaused,
            "clicking Pause never paused the session the engine was decoding");

        await PressAsync(
            host,
            "PlayerPlayAction",
            () => player.IsPlaying,
            "clicking Play never resumed the paused session");

        // The skips are the bar's own rule: one in flight refuses the next, so each press is given
        // its answer before the following one.
        await PressAsync(
            host,
            "TransportSkipForward",
            () => transport!.Position,
            "clicking the forward skip never moved the playhead");

        await PressAsync(
            host,
            "TransportSkipBackward",
            () => transport!.Position,
            "clicking the backward skip never moved the playhead");

        await PressAsync(
            host,
            "TransportToggleMute",
            () => transport!.IsMuted,
            "clicking Mute never muted the session");
        Assert.True(transport!.IsMuted);

        // The volume slider is a command control like any other, and pressing it has to reach the
        // level the session is playing at - not only the thumb on the screen.
        await PressAsync(
            host,
            "TransportVolumeLabel",
            () => transport.VolumePercent,
            "clicking the volume slider never changed the level the session plays at");

        await PressAsync(
            host,
            "PlayerStopAction",
            () => player.IsStopped,
            "clicking Stop never stopped the session");

        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Where the proposed name comes from, on the entry this used to be blocked by: one somebody
    /// identified whose stored title is still the file name, year and all, and which holds no year
    /// of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the scene that measured the block, kept and turned around. It used to assert that the
    /// plan was empty — the application asked to rename each file to the name it already had, which
    /// <see cref="RenamePolicy"/> answers correctly with <c>NoChange</c>. What it asserts now is the
    /// rule that replaced it: the title and the year travel together from one source, so an entry
    /// carrying "2016" inside its title is not handed the parser's 2016 as well. Pairing them wrote
    /// <c>Arrival 2016 (2016).mp4</c>, which is what this run caught.
    /// </para>
    /// <para>
    /// The season and the episode are the one exception, because no entry in the catalogue holds
    /// them: identification writes metadata, and the numbers live only in the file name.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_proposed_name_takes_its_title_and_its_year_from_the_same_source()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var original = Path.Combine(media, "arrival.2016.1080p.web.mp4");

        // Nothing is decoded here, so the file only has to exist, be catalogued and be identified.
        await File.WriteAllBytesAsync(original, [0x41, 0x50], TestContext.Current.CancellationToken);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var fileId = await SeedMediaFileAsync(factory, media, original, TimeSpan.FromMinutes(116));
        await SeedIdentifiedTitleAsync(factory, fileId);

        using var host = ShowShell(height: 2000);
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        await library.OpenDetailsAsync(Assert.Single(library.Items), TestContext.Current.CancellationToken);
        await host.ViewModel.OpenRenamePreviewAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        var rename = host.ViewModel.Rename;
        Assert.NotNull(rename);

        // The stored title is "Arrival 2016" and the entry holds no year, so the name is that title
        // and nothing else: no second 2016, and no conflict standing in for a decision.
        var operation = Assert.Single(rename!.Operations);
        Assert.Equal("Arrival 2016.mp4", Path.GetFileName(operation.DestinationPath));
        Assert.Empty(rename.Conflicts);
        Assert.True(
            ((AsyncRelayCommand)rename.ExecuteCommand).CanExecute(null) == rename.IsConfirmed,
            "Rename declared itself executable without the consent that gates it.");
        Assert.True(File.Exists(original), "Asking for a preview must not move anything.");
    }

    /// <summary>
    /// Finds one command control by its <b>anchor</b>: the resource key behind its accessible name,
    /// its <c>x:Name</c>, or — for the two controls named by their data — the name the walk itself
    /// seeded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured on 2026-08-15: of the 129 command controls across the 48 views, only 60 carry an
    /// <c>x:Name</c>, so anchoring on that would mean adding one to 69 controls for the benefit of a
    /// test. 239 elements already carry an accessible name, 80 tests require every interactive one to
    /// have it, and a redesign changes the shape without removing it — so the key is the anchor that
    /// costs no surface and survives what is coming.
    /// </para>
    /// <para>
    /// The text is resolved rather than written down, so the walk does not break when a string is
    /// reworded and does not depend on which language is loaded.
    /// </para>
    /// <para>
    /// Only what is <b>on screen</b> is a candidate, and that is not a refinement: two of the 129 keys
    /// are declared twice — the back button in each of the library's two mutually exclusive detail
    /// branches, and the provider-trailer link on the film card and the series card. Both branches
    /// live in the visual tree at once, so matching on the key alone finds two controls where a click
    /// can only ever reach one. <paramref name="helpText"/> separates the rest: the ten rating buttons
    /// share one accessible name by design and are told apart by the score they carry, which is what a
    /// screen reader reads out after the name.
    /// </para>
    /// </remarks>
    private static Control Resolve(ShellHost host, string anchor, string? helpText = null)
    {
        var expected = Avalonia.Application.Current!.TryFindResource(anchor, out var resolved) && resolved is string text
            ? text
            : anchor;
        var matches = host.Shell.GetVisualDescendants()
            .OfType<Control>()
            .Where(candidate =>
                candidate.Name == anchor || AutomationProperties.GetName(candidate) == expected)
            .Where(candidate => candidate.IsEffectivelyVisible)
            .Where(candidate => helpText is null || AutomationProperties.GetHelpText(candidate) == helpText)
            .ToArray();

        Assert.True(
            matches.Length == 1,
            $"{anchor}{(helpText is null ? string.Empty : $" [{helpText}]")} matched {matches.Length} "
                + "controls on screen; a click needs exactly one.");
        return matches[0];
    }

    /// <summary>
    /// Puts a control where a click can reach it, and answers with the scrollers it had to move.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule is: scroll as little as possible, and prefer not scrolling at all. Measured on
    /// 2026-08-16 by sweeping the offsets of the settings page — with the offset at 0 a click at the
    /// position the layout reports reaches the control; at <em>every</em> offset above 0 it does not,
    /// even with the control well inside the window. The same sweep against a bare ScrollViewer in a
    /// bare window behaves correctly, so this is the assembled shell's own nesting, not Avalonia's
    /// hit testing in general.
    /// </para>
    /// <para>
    /// So the page goes back to the top first, and only a control that still does not fit is scrolled
    /// to — which is what a person does anyway, and what makes a press repeatable no matter which
    /// control the walk pressed before it. Without it the settings page was a one-way trip: every
    /// press left the page where its search stopped, and the next control higher up was unreachable.
    /// </para>
    /// </remarks>
    private static ScrollViewer[] Reveal(ShellHost host, Control control)
    {
        var scrollers = control.GetVisualAncestors().OfType<ScrollViewer>().ToArray();
        foreach (var scroller in scrollers)
        {
            scroller.Offset = scroller.Offset.WithY(0);
        }

        host.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        if (Fits(host, control))
        {
            return scrollers;
        }

        // Too far down the page to be reached from the top, so it is scrolled to — and the scroll and
        // the layout settle over a few passes rather than one, because these viewers nest.
        for (var settle = 0; settle < 24 && !Fits(host, control); settle++)
        {
            control.BringIntoView();
            host.Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            // BringIntoView stops at the nearest edge, which leaves a control at the very bottom of a
            // nested viewer still clipped. The wheel keeps going, so this does too.
            foreach (var scroller in scrollers.Where(s => s.Offset.Y < s.Extent.Height - s.Viewport.Height))
            {
                scroller.Offset = scroller.Offset.WithY(Math.Min(
                    scroller.Offset.Y + 120,
                    Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height)));
                host.Window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }
        }

        return scrollers;
    }

    /// <summary>Whether the control's middle is inside the window as the layout has it.</summary>
    private static bool Fits(ShellHost host, Control control) =>
        control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
            host.Window) is { } centre
        && centre.X >= 0
        && centre.Y >= 0
        && centre.X < host.Window.Bounds.Width
        && centre.Y < host.Window.Bounds.Height;

    /// <summary>
    /// Presses a control with the mouse, at its centre in window coordinates, the way a person with a
    /// pointing device does.
    /// </summary>
    private static Point Click(ShellHost host, Control control)
    {
        var scrollers = Reveal(host, control);

        // The middle, except on a range control, where the middle is usually where the value already
        // is: the volume slider runs from 0 to 200 and starts at 100, so pressing its centre asks for
        // exactly the level already playing and nothing changes. A quarter along asks for something
        // else, which is what pressing a slider is for. Measured on 2026-08-15, after a press that
        // did reach the control and still moved nothing.
        var alongX = control is Slider ? 0.25 : 0.5;
        var centre = control.TranslatePoint(
            new Point(control.Bounds.Width * alongX, control.Bounds.Height / 2),
            host.Window);

        Assert.True(
            control.IsEffectivelyVisible && control.IsEffectivelyEnabled,
            $"{Describe(control)} is on screen but cannot be pressed: "
            + $"visible={control.IsEffectivelyVisible}, enabled={control.IsEffectivelyEnabled}.");
        Assert.True(centre.HasValue, $"{Describe(control)} has no position in the window.");

        // A point outside the window is a click that lands on nothing, and without this the only
        // symptom is the effect never arriving - sixty seconds of waiting and no word about where
        // the press went.
        Assert.True(
            centre.Value.X >= 0
                && centre.Value.Y >= 0
                && centre.Value.X < host.Window.Bounds.Width
                && centre.Value.Y < host.Window.Bounds.Height,
            $"{Describe(control)} sits at {centre.Value} and the window is "
                + $"{host.Window.Bounds.Width}x{host.Window.Bounds.Height}, so the press would land "
                + "outside it. Scrollers between it and the window: "
                + (scrollers.Length == 0
                    ? "none, so nothing here could have moved it."
                    : string.Join(
                        ", ",
                        scrollers.Select(s =>
                            $"offset {s.Offset.Y:F0}, viewport {s.Viewport.Height:F0}, "
                            + $"extent {s.Extent.Height:F0}"))));

        host.Window.MouseMove(centre.Value, RawInputModifiers.None);
        host.Window.MouseDown(centre.Value, MouseButton.Left, RawInputModifiers.None);
        host.Window.MouseUp(centre.Value, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        return centre.Value;
    }

    /// <summary>What a click at this point reaches, innermost first, a few levels up.</summary>
    /// <remarks>
    /// This is the second half of a complaint that used to be only its first half. A press that
    /// misses arrives as sixty seconds of waiting and "the effect never came", which says nothing
    /// about where the press went; naming the chain is what turns that into a lead. It is a
    /// diagnosis and not an assertion on purpose: <c>InputHitTest</c> was measured on 2026-08-15 not
    /// to predict where a click goes, so it may not decide whether one is allowed.
    /// </remarks>
    private static string DescribeChainAt(ShellHost host, Point point)
    {
        if (host.Window.InputHitTest(point) is not Visual hit)
        {
            return "nothing at all";
        }

        return string.Join(
            " inside ",
            new[] { hit }.Concat(hit.GetVisualAncestors()).OfType<Control>().Take(5).Select(Describe));
    }

    /// <summary>
    /// Presses clear of a control, on a point that belongs to no command control at all. It is the
    /// control for the click: whatever the button does, this must not do it.
    /// </summary>
    private static void ClickBeside(ShellHost host, Control control)
    {
        var beside = BesidePoint(host, control);
        host.Window.MouseMove(beside, RawInputModifiers.None);
        host.Window.MouseDown(beside, MouseButton.Left, RawInputModifiers.None);
        host.Window.MouseUp(beside, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Finds a point next to a control that lies inside <b>no</b> command control on screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This started as "one control-height above", and that was wrong in a way only a measurement
    /// showed: on the film card the controls wrap into rows, so the strip above a button is the row
    /// above it — another button. Measured on 2026-08-15, the control click for <c>Clear rating</c>
    /// landed on the favourite toggle and turned it back off, and the walk said nothing, because the
    /// assertion only asked whether the <em>rating</em> had changed. A control click that presses
    /// something else is not a control; it is a second, unrecorded press.
    /// </para>
    /// <para>
    /// So the point is chosen by geometry rather than hoped for: every command control on screen is
    /// measured in window coordinates and the first candidate offset that falls inside none of them
    /// wins. Geometry, and not <c>InputHitTest</c>, because that was already measured not to predict
    /// where a click goes. If no such point exists the walk says so instead of clicking blind.
    /// </para>
    /// </remarks>
    private static Point BesidePoint(ShellHost host, Control control)
    {
        Reveal(host, control);

        var centre = control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
            host.Window);
        Assert.True(centre.HasValue, $"{Describe(control)} has no position in the window.");

        var occupied = host.Shell.GetVisualDescendants()
            .OfType<Control>()
            .Where(candidate => candidate is Button or ComboBox or Slider && candidate.IsEffectivelyVisible)
            .Select(candidate => candidate.TranslatePoint(default, host.Window) is { } origin
                ? new Rect(origin, candidate.Bounds.Size)
                : (Rect?)null)
            .OfType<Rect>()
            .ToArray();

        var step = new Vector(Math.Max(control.Bounds.Width, 8), Math.Max(control.Bounds.Height, 8));
        foreach (var offset in new[]
        {
            new Vector(0, -step.Y), new Vector(0, step.Y),
            new Vector(-step.X, 0), new Vector(step.X, 0),
            new Vector(0, -step.Y * 2), new Vector(0, step.Y * 2),
            new Vector(-step.X, -step.Y), new Vector(step.X, step.Y),
        })
        {
            var candidate = centre!.Value + offset;
            if (candidate.X < 0
                || candidate.Y < 0
                || candidate.X >= host.Window.Bounds.Width
                || candidate.Y >= host.Window.Bounds.Height)
            {
                continue;
            }

            if (!occupied.Any(rect => rect.Contains(candidate)))
            {
                return candidate;
            }
        }

        Assert.Fail(
            $"{Describe(control)} is surrounded by other command controls, so there is nowhere to "
                + "put the click that proves the press did the work.");
        return default;
    }

    /// <summary>
    /// Presses one command control the way a person does, and proves the press is what did it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three things are required and all three happen here, which is why coverage is recorded by this
    /// method and nowhere else: the click <b>beside</b> the control, which must change nothing; the
    /// real click; and the <b>effect</b>, read through <paramref name="probe"/> rather than assumed.
    /// A control is in the ledger only once all three have happened, so "pressed" cannot drift into
    /// "a press was written down in the file".
    /// </para>
    /// <para>
    /// The beside click is given the same time to work as the real one. Without that wait, "nothing
    /// happened" would only ever mean "nothing has happened yet", and the control would be no control
    /// at all.
    /// </para>
    /// </remarks>
    private static Task PressAsync<T>(
        ShellHost host,
        string anchor,
        Func<T> probe,
        string complaint,
        string? helpText = null,
        string? recordAs = null) =>
        PressAsync(host, anchor, () => Task.FromResult(probe()), complaint, helpText, recordAs);

    /// <summary>
    /// The same, for a control whose effect is not on the surface but in the catalogue.
    /// </summary>
    /// <remarks>
    /// Save is the reason this exists. What it changes is a row, and asserting on the editor instead
    /// would prove the editor kept what was typed into it — which it would do whether or not anything
    /// was written down.
    /// </remarks>
    private static async Task PressAsync<T>(
        ShellHost host,
        string anchor,
        Func<Task<T>> probe,
        string complaint,
        string? helpText = null,
        string? recordAs = null)
    {
        var control = Resolve(host, anchor, helpText);
        var before = await probe();

        ClickBeside(host, control);
        await SettleAsync();
        Assert.True(
            EqualityComparer<T>.Default.Equals(await probe(), before),
            $"Clicking beside {anchor} changed the very thing the press is meant to change, so "
                + "pressing it would have proved nothing.");

        // Pressed, and pressed again if nothing happened. A person whose click misses does not wait
        // sixty seconds and give up; they look at where the thing is and press it again. That is
        // what this is, and it is here because the position a click reaches and the position the
        // layout reports do not always agree inside the assembled shell's nested scroll viewers —
        // measured on 2026-08-16, where the same control at the same offset answered a press on one
        // pass and not on the one before it. Only a press that changed nothing is repeated, so a
        // control that answers the first time is never pressed twice.
        var pressed = Click(host, control);
        var attempts = 1;
        while (attempts < 8 && EqualityComparer<T>.Default.Equals(await probe(), before))
        {
            await SettleAsync();
            if (!EqualityComparer<T>.Default.Equals(await probe(), before))
            {
                break;
            }

            pressed = Click(host, control);
            attempts++;
        }

        await WaitForAsync(
            async () => !EqualityComparer<T>.Default.Equals(await probe(), before),
            $"{complaint}. {attempts} presses, the last at {pressed}, where a click reaches "
                + $"{DescribeChainAt(host, pressed)}.");
        WalkLedger.Record(control, recordAs ?? anchor);
    }

    /// <summary>Names a control in a complaint, by whatever it does carry.</summary>
    private static string Describe(Control control) =>
        control.Name ?? AutomationProperties.GetName(control) ?? control.GetType().Name;

    /// <summary>
    /// Pumps the dispatcher while a little real time passes, which is what an effect arriving on a
    /// command's continuation needs in order to arrive at all.
    /// </summary>
    private static async Task SettleAsync()
    {
        for (var pass = 0; pass < 6; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Whether a click at this point would land on that control, or inside it.</summary>
    private static bool IsUnder(ShellHost host, Point point, Control control) =>
        host.Window.InputHitTest(point) is Visual hit
        && (ReferenceEquals(hit, control) || hit.GetVisualAncestors().Contains(control));

    /// <summary>
    /// A title somebody already identified, plus the provider answer a previous lookup cached.
    /// Written through the real repository and the real cache so the refresh reads them the way the
    /// application does.
    /// </summary>
    private static async Task SeedIdentifiedTitleAsync(SqliteConnectionFactory factory, Guid fileId)
    {
        const string ProviderKey = "movie:329865";
        _ = await new CatalogMetadataRepository(factory).TrySaveAsync(
            new CatalogMetadata(
                new TitleId(fileId),
                new EditableMetadata(
                    "Arrival 2016",
                    OriginalTitle: null,
                    Overview: null,
                    ReleaseYear: null,
                    Genres: [],
                    PosterPath: null,
                    BackdropPath: null,
                    TrailerKey: null,
                    LockedFields: new HashSet<MetadataField>()),
                Revision: 0,
                Provider: "tmdb",
                ProviderKey: ProviderKey),
            expectedRevision: 0,
            TestContext.Current.CancellationToken);

        var now = DateTimeOffset.UtcNow;
        await new SqliteMetadataCache(factory).StoreAsync(
            new MetadataCacheEntry(
                new MetadataCacheKey("tmdb", ProviderKey, "es-ES", new TmdbOptions(accessToken: null).ProviderVersion),
                """
                {
                  "title": "La llegada",
                  "original_title": "Arrival",
                  "overview": "Una lingüista traduce a los visitantes.",
                  "release_date": "2016-11-11",
                  "genres": [{ "name": "Ciencia ficción" }],
                  "poster_path": "/poster.jpg",
                  "backdrop_path": "/backdrop.jpg"
                }
                """,
                ETag: null,
                now,
                now.AddDays(1)),
            TestContext.Current.CancellationToken);
    }

    /// <summary>Drives the dispatcher while real time passes, because the engine works on its own threads.</summary>
    private static async Task WaitForAsync(Func<Task<bool>> condition, string complaint)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTimeOffset.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (await condition())
            {
                return;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.Fail(complaint);
    }

    private static void Navigate(ShellHost host, AppRoute route)
    {
        host.ViewModel.NavigateCommand.Execute(route);
        Dispatcher.UIThread.RunJobs();
        host.Window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
    }

    private async Task<SqliteConnectionFactory> SeedRootAsync(string mediaRoot, ScanPolicy policy)
    {
        Directory.CreateDirectory(_dataRoot);
        var factory = new SqliteConnectionFactory(new AppDataPaths(_dataRoot).DatabasePath);
        using (var runner = new MigrationRunner(factory))
        {
            await runner.MigrateAsync(TestContext.Current.CancellationToken);
        }

        await new LibraryRootRepository(factory).AddAsync(
            new LibraryRoot(
                new LibraryRootId(Guid.NewGuid()),
                mediaRoot,
                RootKind.Local,
                RootAvailability.Available,
                policy),
            TestContext.Current.CancellationToken);
        return factory;
    }

    private static async Task<Guid> SeedMediaFileAsync(
        SqliteConnectionFactory factory,
        string mediaRoot,
        string mediaPath,
        TimeSpan duration)
    {
        var roots = new LibraryRootRepository(factory);
        var all = await roots.ListAsync(TestContext.Current.CancellationToken);
        var root = all.Single(candidate => candidate.Path == mediaRoot);
        var id = Guid.NewGuid();
        await new MediaFileRepository(factory).UpsertAsync(
            new MediaFile(
                new MediaFileId(id),
                root.Id,
                mediaPath,
                new FileInfo(mediaPath).Length,
                DateTimeOffset.UnixEpoch,
                new TechnicalMetadata(duration, "mp4", ["H264"], ["AAC"], 320, 240)),
            TestContext.Current.CancellationToken);
        return id;
    }

    /// <summary>
    /// One show, one season, two episodes, each backed by its file — written through SQL because the
    /// catalogue writes these rows during identification, which needs the network the harness does
    /// not have.
    /// </summary>
    private static async Task<Guid> SeedSeriesAsync(
        SqliteConnectionFactory factory,
        Guid firstFile,
        Guid secondFile)
    {
        var showId = Guid.NewGuid();
        var firstEpisode = Guid.NewGuid();
        var secondEpisode = Guid.NewGuid();
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO titles (id, kind, primary_title, sort_title, release_year, added_utc,
                                last_played_utc, has_progress, is_personal, is_available)
            VALUES ($show, 1, 'Show', 'show', 2020, $added, NULL, 0, 0, 1);
            INSERT INTO seasons (show_id, season_number, title) VALUES ($show, 1, 'T1');
            INSERT INTO episodes (id, show_id, season_number, episode_number, absolute_number,
                                  title, sort_order, is_available)
            VALUES ($e1, $show, 1, 1, 1, 'E1', 1, 1), ($e2, $show, 1, 2, 2, 'E2', 2, 1);
            INSERT INTO episode_media (episode_id, media_file_id) VALUES ($e1, $f1), ($e2, $f2);
            """;
        command.Parameters.AddWithValue("$show", showId.ToString("D"));
        command.Parameters.AddWithValue("$added", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$e1", firstEpisode.ToString("D"));
        command.Parameters.AddWithValue("$e2", secondEpisode.ToString("D"));
        command.Parameters.AddWithValue("$f1", firstFile.ToString("D"));
        command.Parameters.AddWithValue("$f2", secondFile.ToString("D"));
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return showId;
    }

    /// <summary>
    /// The provider's trailer key, stored on a title the way an identification stores it.
    /// </summary>
    private static async Task SeedTrailerKeyAsync(
        SqliteConnectionFactory factory,
        TitleId titleId,
        string trailerKey)
    {
        _ = await new CatalogMetadataRepository(factory).TrySaveAsync(
            new CatalogMetadata(
                titleId,
                new EditableMetadata(
                    "Arrival",
                    OriginalTitle: null,
                    Overview: null,
                    ReleaseYear: 2016,
                    Genres: [],
                    PosterPath: null,
                    BackdropPath: null,
                    TrailerKey: trailerKey,
                    LockedFields: new HashSet<MetadataField>()),
                Revision: 0),
            expectedRevision: 0,
            TestContext.Current.CancellationToken);
    }

    private static async Task<long> CountAsync(SqliteConnectionFactory factory, string table)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0L);
    }

    private ShellHost ShowShell(int height = 1000)
    {
        Assert.NotNull(Avalonia.Application.Current);
        ApSolutions.LocalMedia.Presentation.App.ApplyLanguage(
            Avalonia.Application.Current,
            CultureInfo.GetCultureInfo("es-ES"));
        Directory.CreateDirectory(_dataRoot);
        var application = ApplicationHost.Create(new AppDataPaths(_dataRoot));

        // The window shows what the application put in it — the container — rather than the shell
        // lifted out of it. Lifting it out left the whole tree attached to a container that was never
        // shown, and a Button only evaluates its Command once it is on the logical tree: every
        // command-bound button in the shell reported itself disabled, so no click could reach one.
        // Buttons wired with Click= were unaffected, which is why nothing noticed until this walk
        // tried to press one.
        var created = application.CreateShell();
        var window = new Window { Width = 1600, Height = height, Content = created };
        window.Show();

        // ARQ-005: the shell arrives after the database is ready, not with it, so the walk waits
        // for it. The wait names what stood in its place if it never comes.
        var shell = Assert.IsType<ShellView>(AssembledStartup.FinalContent(created));
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
        return new ShellHost(
            application,
            window,
            shell,
            Assert.IsType<ShellViewModel>(shell.DataContext),
            _teardownFailures.Add);
    }

    private sealed record ShellHost(
        ApplicationHost Application,
        Window Window,
        ShellView Shell,
        ShellViewModel ViewModel,
        Action<Exception> ReportTeardownFailure) : IDisposable
    {
        public void Dispose()
        {
            // The close walks the assembled path: the window lifecycle's handler flushes and stops
            // the background work, and it needs the dispatcher pumped to finish before the directory
            // underneath it is deleted.
            Window.Close();
            for (var attempt = 0; attempt < 20; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(50);
            }

            // And then the application lets go of what it built, which is what a real exit does.
            //
            // A throw here would not add a failure: inside a using statement it *replaces* the
            // body's, so a scene that failed its own assertion reports whatever the teardown said
            // instead. Measured on 2026-08-15: pressing Stop and then closing the player leaves the
            // session coordinator stopping an engine that is already gone, and the
            // ObjectDisposedException out of that shutdown took the place of the assertion the walk
            // had just failed — sixty seconds of waiting, and not a word about what was waited for.
            //
            // So it is handed to the suite instead, which raises it after the scene has had its say.
            // A teardown defect stays visible; it just stops speaking over the defect being hunted.
            try
            {
                Application.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                ReportTeardownFailure(exception);
            }
        }
    }

    /// <summary>
    /// Materialises one synthetic sample under the ignored test-media tree, or declares the machine
    /// assumption when no encoder exists — the same rule the media suites follow.
    /// </summary>
    private static async Task<string> RequireSampleAsync(string name, int durationSeconds)
    {
        var encoder = FindEncoder();
        Assert.SkipWhen(
            encoder is null,
            "ffmpeg was not found. Set FFMPEG_PATH or install ffmpeg to generate the walk's media.");
        // The duration is part of the name because it is part of what was asked for. Keyed on the
        // name alone, the cache hands back whatever length happened to be produced first: asking for
        // ninety seconds after a twelve-second sample existed returned the twelve, and the scene
        // failed on a forward skip that ran off the end of a file it thought was long enough.
        var destination = Path.Combine(
            RepositoryLayout.Root,
            "artifacts",
            "test-media",
            "walk",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{Path.GetFileNameWithoutExtension(name)}-{durationSeconds}s{Path.GetExtension(name)}"));
        if (File.Exists(destination) && new FileInfo(destination).Length > 0)
        {
            return destination;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var arguments =
            $"-hide_banner -loglevel error -nostdin -y " +
            $"-f lavfi -i testsrc2=size=320x240:rate=15:duration={durationSeconds} " +
            $"-f lavfi -i sine=frequency=440:duration={durationSeconds} " +
            $"-c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac -b:a 64k -shortest " +
            $"\"{destination}\"";
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(encoder!, arguments)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        _ = process.Start();
        var error = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(process.ExitCode == 0, $"The encoder failed with exit code {process.ExitCode}: {error}");
        return destination;
    }

    private static string? FindEncoder()
    {
        var configured = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var candidates = new List<string> { @"C:\ffmpeg\bin\ffmpeg.exe" };
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        candidates.AddRange(pathVariable
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, "ffmpeg.exe")));
        return candidates.FirstOrDefault(File.Exists);
    }
}
