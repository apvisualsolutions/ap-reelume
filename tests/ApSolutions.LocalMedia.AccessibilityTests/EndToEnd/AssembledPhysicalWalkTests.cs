// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.TestSupport;
using ApSolutions.LocalMedia.Windows;
using ApSolutions.LocalMedia.Windows.Shell;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

    public void Dispose()
    {
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
        await SeedSeriesAsync(factory, firstId, secondId);

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
    /// Presses a control with the mouse, at its centre in window coordinates, the way a person with a
    /// pointing device does.
    /// </summary>
    private static void Click(ShellHost host, Control control)
    {
        // Scrolling to it first is what a person does, and it is not optional: the editor sits far
        // enough down the shell that the button's centre lands outside the window until it is
        // brought into view, and a click there hits nothing at all. Two scroll viewers are nested
        // here, so the scroll and the layout settle over a few passes rather than one.
        var scrollers = control.GetVisualAncestors().OfType<ScrollViewer>().ToArray();
        Point? centre = null;
        for (var settle = 0; settle < 24; settle++)
        {
            control.BringIntoView();
            host.Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            centre = control.TranslatePoint(
                new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
                host.Window);
            if (centre is { } candidate && IsUnder(host, candidate, control))
            {
                break;
            }

            // BringIntoView stops at the nearest edge, which leaves a control at the very bottom of a
            // nested viewer still clipped. The wheel keeps going, so this does too.
            foreach (var scroller in scrollers.Where(s => s.Offset.Y < s.Extent.Height - s.Viewport.Height))
            {
                scroller.Offset = scroller.Offset.WithY(Math.Min(
                    scroller.Offset.Y + 120,
                    Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height)));
            }
        }

        Assert.True(
            control.IsEffectivelyVisible && control.IsEffectivelyEnabled,
            $"{Describe(control)} is on screen but cannot be pressed: "
            + $"visible={control.IsEffectivelyVisible}, enabled={control.IsEffectivelyEnabled}.");
        Assert.True(centre.HasValue, $"{Describe(control)} has no position in the window.");

        host.Window.MouseMove(centre.Value, RawInputModifiers.None);
        host.Window.MouseDown(centre.Value, MouseButton.Left, RawInputModifiers.None);
        host.Window.MouseUp(centre.Value, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
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
        control.BringIntoView();
        host.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

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
    private static async Task PressAsync<T>(
        ShellHost host,
        string anchor,
        Func<T> probe,
        string complaint,
        string? helpText = null,
        string? recordAs = null)
    {
        var control = Resolve(host, anchor, helpText);
        var before = probe();

        ClickBeside(host, control);
        await SettleAsync();
        Assert.True(
            EqualityComparer<T>.Default.Equals(probe(), before),
            $"Clicking beside {anchor} changed the very thing the press is meant to change, so "
                + "pressing it would have proved nothing.");

        Click(host, control);
        await WaitForAsync(
            () => Task.FromResult(!EqualityComparer<T>.Default.Equals(probe(), before)),
            complaint);
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
    private static async Task SeedSeriesAsync(SqliteConnectionFactory factory, Guid firstFile, Guid secondFile)
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
        return new ShellHost(application, window, shell, Assert.IsType<ShellViewModel>(shell.DataContext));
    }

    private sealed record ShellHost(
        ApplicationHost Application,
        Window Window,
        ShellView Shell,
        ShellViewModel ViewModel) : IDisposable
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
            Application.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
        var destination = Path.Combine(RepositoryLayout.Root, "artifacts", "test-media", "walk", name);
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
