// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Lifecycle;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Appearance;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.Presentation.Backup;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Recovery;
using ApSolutions.LocalMedia.Presentation.Review;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Presentation.Theme;
using ApSolutions.LocalMedia.Presentation.Updates;
using ApSolutions.LocalMedia.TestSupport;
using ApSolutions.LocalMedia.Windows;
using ApSolutions.LocalMedia.Windows.Metadata;
using ApSolutions.LocalMedia.Windows.Shell;
using ApSolutions.LocalMedia.Windows.Updates;
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
        Dispatcher.UIThread.RunJobs();

        // Opened by its button rather than by its command: this is the only scene that has a real
        // version group to open, so it is the only one that can press it.
        await PressAsync(
            host,
            "TitleReviewDuplicatesAction",
            () => host.ViewModel.HasDuplicates,
            "clicking Review duplicates never opened the group the two copies formed");
        Assert.Equal(AppRoute.Duplicates, host.ViewModel.CurrentRoute);

        // And the group is decided with the mouse. Opening it already moved to the duplicates
        // destination, where the comparison sits under the overview; the layout still has to
        // settle before a click can land.
        Dispatcher.UIThread.RunJobs();

        // The copy that is not already the one that would play, so that pressing it has something to
        // change. Both files are the same sample, so which one the policy picks unaided is its own
        // business — the walk asks the screen rather than assuming.
        var duplicates = host.ViewModel.Duplicates;
        Assert.NotNull(duplicates);
        var chosen = duplicates!.Items.Single(item => !item.IsEffective);
        Assert.Null(await PreferredVersionAsync(factory));

        // The probe is the stored preference, not the radio's own IsEffective: without a preference
        // the policy already answers with one of the two, so reading the screen would have called
        // "the better copy" and "the copy somebody chose" the same thing.
        await PressAsync(
            host,
            chosen.ShortPath,
            () => PreferredVersionAsync(factory),
            "clicking a version radio never stored the preference for the group",
            helpText: chosen.Quality,
            recordAs: "{Binding ShortPath}");
        Assert.Equal(chosen.Version.MediaFileId.Value, await PreferredVersionAsync(factory));

        // The rail's own door lists the same group, and its row opens the same comparison: the
        // second way in, pressed where a person finds it. Leaving and coming back is what loads
        // the overview - the route reads its list on every visit.
        Navigate(host, AppRoute.Home);
        Navigate(host, AppRoute.Duplicates);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.DuplicatesOverview is { HasGroups: true }),
            "the duplicates overview never listed the group the scan formed");
        Dispatcher.UIThread.RunJobs();

        var row = host.ViewModel.DuplicatesOverview!.Groups.Single();

        // The destination's own table, which is where the prototype puts the decision: the same
        // choice, made without opening anything. Pressed on the copy that is NOT stored, so the
        // press has something to change — and the probe is the stored preference for the same reason
        // it is above, since the policy answers with a version either way.
        var stored = await PreferredVersionAsync(factory);
        var other = row.Files.Single(file => file.MediaFileId.Value != stored);
        await PressAsync(
            host,
            other.ShortPath,
            () => PreferredVersionAsync(factory),
            "clicking a radio in the duplicates table never stored the preference",
            helpText: other.Location,
            recordAs: "{Binding ShortPath}");
        Assert.Equal(other.MediaFileId.Value, await PreferredVersionAsync(factory));

        await PressAsync(
            host,
            row.Title,
            () => host.ViewModel.Duplicates,
            "clicking the overview row never opened the group's comparison",
            recordAs: "{Binding Title}");
        Assert.True(host.ViewModel.HasDuplicates);
    }

    /// <summary>
    /// Which copy of a group somebody chose, read from the catalogue. Null until one is chosen: the
    /// policy answers with a version either way, so only this tells a choice from a default.
    /// </summary>
    private static async Task<Guid?> PreferredVersionAsync(SqliteConnectionFactory factory)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT preferred_media_file_id FROM media_version_groups;";
        var stored = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return stored is string text ? Guid.Parse(text) : null;
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
            new PlayDetailsRequest(new MediaFileId(firstId), StartPosition: null),
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

        // LIB-018's own button, pressed with the mouse like everything else. The dialog it would open
        // on somebody's machine is the one thing no harness can answer, so a run with a data root of
        // its own takes the cover out of that root's handover folder — the same exit the external
        // link launcher already uses, and the reason this control can be pressed at all rather than
        // added to the pending list.
        var handoff = Path.Combine(_dataRoot, "handoff");
        Directory.CreateDirectory(handoff);
        var chosenCover = Path.Combine(handoff, "portada.png");
        await File.WriteAllBytesAsync(
            chosenCover,
            [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A],
            TestContext.Current.CancellationToken);

        // The poster's own lock was set by the loop above, so what is read back here is the
        // path rather than the lock: choosing a cover after locking the field still has to fill
        // it, because the lock protects it from the PROVIDER rather than from its owner.
        await PressAsync(
            host,
            "CoverChooseAction",
            () => editor.PosterPath,
            "clicking Elegir una imagen never put a cover into the poster field");

        // The path it stored is the application's own copy rather than the file that was picked:
        // choosing a cover copies it in, so a poster still pointing at the handover folder would
        // mean the copy never happened and the picture would vanish with that folder.
        Assert.NotNull(editor.PosterPath);
        Assert.NotEqual(chosenCover, editor.PosterPath);
        Assert.True(
            File.Exists(editor.PosterPath),
            $"the poster field points at {editor.PosterPath}, which is not on this disk.");

        // And the lock is still on. Without it the next provider refresh would put the provider's
        // artwork back over the cover somebody chose, days later, with nothing to connect the two.
        Assert.True(editor.LockPosterPath, "choosing a cover left the poster field unlocked.");


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

        // And the card behind the editor is holding the cover, without anybody having left the title
        // and come back. This is the assertion the whole 2026-09-04 change exists for, and it fails
        // for either of the two reasons it could: a poster nobody resolved, or a card nobody told.
        //
        // Until that day the field was only ever asked «is this a provider path», and the picker
        // writes an absolute one — so a cover was copied in, locked, carried in the backup, and
        // never drawn. Then closing the editor dropped both surfaces without reloading, so even a
        // resolved cover would not have appeared until the title was opened again.
        Dispatcher.UIThread.RunJobs();
        Assert.True(
            library.MovieDetails.HasPoster,
            "the card behind the editor has no poster after a cover was chosen and saved.");
        Assert.Equal(editor.PosterPath, library.MovieDetails.PosterFile);
        Assert.True(
            File.Exists(library.MovieDetails.PosterFile),
            $"the card points at {library.MovieDetails.PosterFile}, which is not on this disk.");

        // And Restore puts the provider's answer back over it, which is the whole point of having
        // edited by hand being reversible.
        await PressAsync(
            host,
            "MetadataRestoreAction",
            () => editor.Title,
            "clicking Restore never brought the provider's title back over the edited one");
        Assert.Equal("La llegada", editor.Title);

        // The page's own three controls, which arrived with it on 2026-08-28. The rename preview is
        // opened first because a pill is only drawn when its surface exists: with one tool open
        // there is one pill, pressing it would change nothing, and PressAsync refuses a press whose
        // effect never arrives — which is the whole reason it can be trusted.
        await host.ViewModel.OpenRenamePreviewAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, host.ViewModel.EditorTab);

        await PressAsync(
            host,
            "MetadataEditorTabLabel",
            () => host.ViewModel.IsMetadataTab,
            "clicking the metadata pill never brought the metadata editor back in front");
        Assert.True(host.ViewModel.IsMetadataTab);

        await PressAsync(
            host,
            "RenamePreviewTabLabel",
            () => host.ViewModel.IsRenameTab,
            "clicking the renaming pill never brought the rename preview in front");
        Assert.True(host.ViewModel.IsRenameTab);

        // And «Volver · Biblioteca», which is what makes this a page rather than a panel: it drops
        // both surfaces, so coming back does not land in the editor again.
        await PressAsync(
            host,
            "LibraryBackAction",
            () => host.ViewModel.HasEditorPanel,
            "clicking Back never left the editor page");
        Assert.False(host.ViewModel.HasEditorPanel);
        Assert.False(host.ViewModel.HasMetadataEditor);
        Assert.False(host.ViewModel.HasRename);
        Assert.True(host.ViewModel.IsLibraryListVisible);
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

        // The side index is the way to every section now, so this scene presses it the way a person
        // does: away to Credits, across to segment detection, and back to Appearance — which also
        // proves the one entry no other scene needs, and gets the walk's ledger its three items.
        Assert.Equal(SettingsSection.Appearance, host.ViewModel.CurrentSettingsSection);
        await PressAsync(
            host,
            "AboutCreditsHeading",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Créditos in the settings index never opened the credits section");
        Assert.Equal(SettingsSection.Credits, host.ViewModel.CurrentSettingsSection);

        await PressAsync(
            host,
            "SegmentDetectionSettingsTitle",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Detección de segmentos in the settings index never opened its section");
        Assert.Equal(SettingsSection.SegmentDetection, host.ViewModel.CurrentSettingsSection);

        await PressAsync(
            host,
            "AppearanceTitle",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Apariencia in the settings index never opened the appearance section");
        Assert.Equal(SettingsSection.Appearance, host.ViewModel.CurrentSettingsSection);

        // Playback, the section PLY-011's criterion promised and did not have. Pressed from the
        // index like the others, and then its switch. The assertion reads the same facade the player
        // reads at chaining time, not the view model beside it: a switch that moves and writes
        // nothing is exactly the defect this application keeps finding in itself, and only the
        // store can tell the two apart.
        await PressAsync(
            host,
            "PlaybackSettingsTitle",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Reproducción in the settings index never opened its section");
        Assert.Equal(SettingsSection.Playback, host.ViewModel.CurrentSettingsSection);

        var playback = host.ViewModel.PlaybackSettings;
        Assert.NotNull(playback);
        var chain = host.Application.Services.GetRequiredService<StartNextEpisodeCountdown>();
        Assert.True(playback!.IsCountdownEnabled);

        await PressAsync(
            host,
            "PlaybackSettingsCountdownEnable",
            () => playback.IsCountdownEnabled,
            "clicking the next-episode countdown switch never turned it off");
        Assert.False(playback.IsCountdownEnabled);

        // Off is a stored zero, which is what the chaining code reads to stay quiet. And the length
        // row goes with it: absent rather than disabled, because a wait that does not happen has no
        // length to ask for.
        Assert.Equal(0, chain.CountdownSeconds);

        await PressAsync(
            host,
            "PlaybackSettingsCountdownEnable",
            () => playback.IsCountdownEnabled,
            "clicking the countdown switch again never turned it back on");
        Assert.True(playback.IsCountdownEnabled);
        Assert.Equal(playback.CountdownSeconds, chain.CountdownSeconds);
        Assert.True(chain.CountdownSeconds >= PlaybackSettingsViewModel.MinimumSeconds);

        // And the length itself, pressed rather than assigned. A click lands the slider wherever the
        // pointer fell, which is why the assertion is that the store followed it and stayed inside
        // the range - not that it reached one particular number the harness would have to predict.
        await PressAsync(
            host,
            "PlaybackSettingsCountdownSeconds",
            () => playback.CountdownSeconds,
            "clicking the countdown length never changed the stored wait");
        Assert.Equal(playback.CountdownSeconds, chain.CountdownSeconds);
        Assert.InRange(
            chain.CountdownSeconds,
            PlaybackSettingsViewModel.MinimumSeconds,
            PlaybackSettingsViewModel.MaximumSeconds);

        await PressAsync(
            host,
            "AppearanceTitle",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Apariencia after Playback never came back to the appearance section");
        Assert.Equal(SettingsSection.Appearance, host.ViewModel.CurrentSettingsSection);

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

        // The fourth and fifth pills, chosen and not only inherited: both high contrasts are
        // applied for real here — the whole application wears them for a moment — and System at
        // the end puts the ordinary theme back for the rest of the walk.
        await PressAsync(
            host,
            "ThemeHighContrastLight",
            () => appearance.CurrentPreference,
            "clicking the high contrast light theme never changed the preference");
        Assert.Equal(ThemePreference.HighContrastLight, appearance.CurrentPreference);

        await PressAsync(
            host,
            "ThemeHighContrastDark",
            () => appearance.CurrentPreference,
            "clicking the high contrast dark theme never changed the preference");
        Assert.Equal(ThemePreference.HighContrastDark, appearance.CurrentPreference);

        await PressAsync(
            host,
            "ThemeSystem",
            () => appearance.CurrentPreference,
            "clicking the system theme never gave the choice back to Windows");
        Assert.Equal(ThemePreference.System, appearance.CurrentPreference);

        // ---- The nine rows the prototype offers beside the theme and the language.
        //
        // Pressed on a page of its own, for the reason OnItsOwn carries: the settings page holds
        // 1,797 px of sections and headless hit testing cannot follow a scroller past its first
        // viewport, so everything below that is unreachable with a mouse in this harness whatever
        // the wheel does. The view is the one the shell mounts and the model is the one the
        // container built — what is missing is the scroll, which belongs to the harness.

        //
        // Each pill row is walked all the way round and left where it started: compact, roomy,
        // comfortable; sharp, very round, soft; the five other accents and then back to the first.
        // That is not tidiness — the density, the rounding and the cover size are written into the
        // application's own resource dictionary, so a scene that left one of them changed would
        // decide the geometry every later scene measures itself in.
        await PressAsync(
            host,
            "AppearanceFollowWindowsLabel",
            () => appearance.FollowsWindowsTheme,
            "clicking Seguir el tema de Windows never took the choice off the system");
        Assert.False(appearance.FollowsWindowsTheme);
        Assert.NotEqual(ThemePreference.System, appearance.CurrentPreference);

        await PressAsync(
            host,
            "AppearanceFollowWindowsLabel",
            () => appearance.FollowsWindowsTheme,
            "clicking it again never gave the choice back to Windows");
        Assert.True(appearance.FollowsWindowsTheme);
        Assert.Equal(ThemePreference.System, appearance.CurrentPreference);

        // The six swatches, each pressed from a state that is not already it, ending on the first —
        // which is the default, so the rest of the walk runs in the colour it was built with.
        foreach (var accent in new[] { "#2D6A4F", "#8E4B2E", "#6B4E9B", "#B23A48", "#0E7490", "#1769AA" })
        {
            // The first swatch carries an x:Name and the inventory identifies it by that, so the
            // press is recorded under the same identity: a control named twice is a control the
            // ledger and the inventory disagree about.
            await PressAsync(
                host,
                accent,
                () => appearance.AccentHex,
                $"clicking the {accent} swatch never changed the accent",
                recordAs: accent == AccentPalette.Presets[0] ? "FirstAccentSwatch" : null);
            Assert.Equal(accent, appearance.AccentHex);
        }

        await PressAsync(
            host,
            "AppearanceMicaLabel",
            () => appearance.Mica,
            "clicking Fondo Mica sutil never turned the backdrop off");
        Assert.False(appearance.Mica);
        await PressAsync(
            host,
            "AppearanceMicaLabel",
            () => appearance.Mica,
            "clicking it again never turned the backdrop back on");
        Assert.True(appearance.Mica);

        // A slider is pressed a quarter along, which is what PressAsync does with one on purpose:
        // its middle is usually where the value already is, and a press that asks for the level
        // already set proves nothing.
        await PressAsync(
            host,
            "AppearanceTintLabel",
            () => appearance.TintPercent,
            "dragging the accent tint never changed how strong the glow is");

        // Everything below the accent tint on this page is out of the walk's reach, and the reason is
        // the harness rather than the application. Avalonia's headless hit testing does not follow a
        // ScrollViewer's offset — reproduced on 2026-08-25 in eight lines: the same view inside a
        // scroller at offset 400, a button reporting 123x36 at y=419, and a click there reaching the
        // scroller's own border, while the same view unscrolled answers every click to the bottom of
        // 1,700 px. The settings page holds 1,797 px of sections since Appearance grew to the
        // prototype's eleven rows, so only its first viewport can be pressed at all.
        //
        // The ten controls that fall past it are named in eng/walk-pending.txt with this measurement,
        // which raises a ratchet that had reached zero. That is written down rather than worked
        // around: swapping the window's content, opening a second window and sweeping the scroller
        // were all tried and all answered the same way.
        appearance.Density = InterfaceDensity.Comfortable;
        appearance.Rounding = CornerRounding.Soft;

        var scan = host.ViewModel.ScanSettings;
        Assert.NotNull(scan);
        await PressAsync(
            host,
            "SettingsSectionLibrary",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Biblioteca y escaneo in the settings index never opened its section");
        Assert.Equal(SettingsSection.Library, host.ViewModel.CurrentSettingsSection);

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
            "SegmentDetectionSettingsTitle",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Detección de segmentos in the settings index never reopened its section");
        Assert.Equal(SettingsSection.SegmentDetection, host.ViewModel.CurrentSettingsSection);

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

        await PressAsync(
            host,
            "LifecycleSettingsTitle",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Ciclo de vida in the settings index never opened its section");
        Assert.Equal(SettingsSection.Lifecycle, host.ViewModel.CurrentSettingsSection);

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

            await PressAsync(
                host,
                "PrivacyTitle",
                () => host.ViewModel.CurrentSettingsSection,
                "clicking Privacidad in the settings index never opened its section");
            Assert.Equal(SettingsSection.Privacy, host.ViewModel.CurrentSettingsSection);

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

            // The surface names the file only after the export returns, so the disk changes first
            // and the name arrives second. Asserting it outright was a race the walk kept winning
            // until it ran beside the rest of the solution: measured on 2026-08-16 as
            // "Assert.NotNull() Failure: Value is null", with the report already in the folder.
            await WaitForAsync(
                () => Task.FromResult(privacy.ExportedFileName is not null),
                "the report reached the diagnostics folder and the surface never said which file it wrote");
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

        await PressAsync(
            host,
            "RecommendationSettingsTitle",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Recomendaciones in the settings index never opened its section");
        Assert.Equal(SettingsSection.Recommendations, host.ViewModel.CurrentSettingsSection);

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
            "ShortcutSettingsAccessibleName",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Atajos de teclado in the settings index never opened its section");
        Assert.Equal(SettingsSection.Shortcuts, host.ViewModel.CurrentSettingsSection);

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
    /// The two drop-downs open, the kind pills narrow and widen the grid as they are pressed, a card
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
        var ids = new Dictionary<string, Guid>();
        foreach (var title in new[] { "Arrival.2016.mp4", "Dune.2021.mp4" })
        {
            var path = Path.Combine(media, title);
            await File.WriteAllBytesAsync(path, [0x41, 0x50], TestContext.Current.CancellationToken);
            ids[title] = await SeedMediaFileAsync(factory, media, path, TimeSpan.FromMinutes(116));
        }

        // One of the two is identified as a film, written the way identification writes it. The
        // kind pills need a kind to find: a scanned file nobody has identified is deliberately
        // neither a film nor a show — the catalogue lists it under a third kind — so a library
        // seeded only with loose files would make Películas and Series both legitimately empty.
        await new CatalogRepository(factory).UpsertTitleAsync(
            new CatalogTitle(
                new TitleId(ids["Dune.2021.mp4"]),
                CatalogTitleKind.Movie,
                "Dune.2021",
                "Dune.2021",
                2021,
                [],
                [],
                [],
                DateTimeOffset.UnixEpoch,
                LastPlayedUtc: null,
                HasProgress: false,
                IsPersonal: false,
                IsAvailable: true),
            TestContext.Current.CancellationToken);

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

        // The kind pills apply as they are pressed — choosing a kind IS the query, which is why the
        // apply button could go. The library holds one identified film and one loose file, so every
        // pill moves the count: Series to none, Películas to the film alone, Todo back to both.
        await PressAsync(
            host,
            "LibraryFilterShows",
            () => library.Items.Count,
            "pressing the Series pill never narrowed the grid to shows");
        Assert.Empty(library.Items);

        await PressAsync(
            host,
            "LibraryFilterMovies",
            () => library.Items.Count,
            "pressing the Películas pill never narrowed the grid to films");
        Assert.Equal("Dune.2021", Assert.Single(library.Items).Title);

        await PressAsync(
            host,
            "LibraryFilterAll",
            () => library.Items.Count,
            "pressing the Todo pill never brought the whole library back");
        Assert.Equal(2, library.Items.Count);

        // Clearing the search lives inside the no-results state now — that is the exit the control
        // was added for — so the scene walks in first: a search nothing matches, the box on screen,
        // and the press brings the whole library back.
        library.Search = "Zzz";
        await library.LoadAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(library.Items);
        await PressAsync(
            host,
            "LibrarySearchClearAction",
            () => library.Items.Count,
            "clicking Clear never emptied the search and brought the rest of the library back");
        Assert.True(string.IsNullOrEmpty(library.Search));
        Assert.Equal(2, library.Items.Count);

        // The row's reset exists only while something narrows the grid, so it is pressed with a
        // search applied: one card on screen, and the press puts the row back to everything.
        library.Search = "Dune";
        await library.LoadAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Dune.2021", Assert.Single(library.Items).Title);
        await PressAsync(
            host,
            "LibraryClearFiltersAction",
            () => library.Items.Count,
            "clicking Quitar filtros never reset the narrowing and brought the library back");
        Assert.True(string.IsNullOrEmpty(library.Search));
        Assert.Equal(2, library.Items.Count);

        // And put it back where the rest of the scene expects it.
        library.Search = "Dune";
        await library.LoadAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
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
    /// The eighth batch, first half: the shell's own controls. Every destination in the navigation
    /// rail, pressed in turn, and the two corrections a title card leads to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rail is what every other scene has been reaching around: they navigate through the command
    /// the way the surface would, which proves the route changes and says nothing about whether the
    /// button a person actually presses is wired to it. This presses all five, and the probe is the
    /// route the navigation service holds.
    /// </para>
    /// <para>
    /// The route the shell opened on is pressed last of the five, which is what leaves the two card
    /// actions that follow starting from somewhere. Since 2026-08-22 that route is the library rather
    /// than home: Add media is pressed first and takes the shell there, and the loop reads where it
    /// is rather than being told.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_navigation_rail_and_the_card_actions_are_pressed_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var mediaPath = Path.Combine(media, "Arrival.2016.mp4");

        // Nothing decodes on these surfaces, so the file only has to exist and be catalogued.
        await File.WriteAllBytesAsync(mediaPath, [0x41, 0x50], TestContext.Current.CancellationToken);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        _ = await SeedMediaFileAsync(factory, media, mediaPath, TimeSpan.FromMinutes(116));

        using var host = ShowShell(height: 1600);

        // The rail's sixth control, and the only one on it that is not a destination: "Add media" at
        // the foot. What it opens now is the dialog floating over whichever route the shell is on —
        // the prototype's grammar — and it still clears the form on the way, which is the half that
        // makes it more than a shortcut.
        //
        // The probe is both halves in one string. Two probes would let a press that opened and
        // cleared nothing pass the first one.
        var onboarding = host.ViewModel.Onboarding;
        Assert.NotNull(onboarding);
        onboarding!.Path = @"R:\left-over";
        Dispatcher.UIThread.RunJobs();
        Assert.False(host.ViewModel.IsAddingRoot);

        await PressAsync(
            host,
            "NavigationAddMedia",
            () => $"{host.ViewModel.IsAddingRoot}|{onboarding.Path}",
            "clicking Add media never opened the add-root dialog with an empty form");
        Assert.True(host.ViewModel.IsAddingRoot);
        Assert.Equal(string.Empty, onboarding.Path);

        // And closed again before the destinations are pressed: the scrim covers the rail while the
        // dialog is up, exactly as a modal question should.
        await PressAsync(
            host,
            "AddRootCancelAction",
            () => host.ViewModel.IsAddingRoot,
            "clicking Cancelar never put the add-root dialog away");
        Assert.False(host.ViewModel.IsAddingRoot);

        // Every destination the rail offers, taken from the shell rather than from a list written
        // here: a destination added later is pressed by this scene the day it is added.
        //
        // The route the shell opens on goes last. Pressing the destination you are already on is a
        // press with nothing to observe — the shell stays where it is, correctly — and the walk
        // proves a control by its effect, so it has to arrive at each destination from elsewhere.
        var opensOn = host.ViewModel.CurrentRoute;
        foreach (var route in host.ViewModel.Routes.Where(other => other != opensOn).Append(opensOn))
        {
            await PressAsync(
                host,
                $"Navigation{route}",
                () => host.ViewModel.CurrentRoute,
                $"clicking {route} in the navigation rail never took the shell there");
            Assert.Equal(route, host.ViewModel.CurrentRoute);
        }

        // Back to the library, and a card open: the three title actions are offered for whichever
        // title the library has open, so opening one is the precondition rather than the test.
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        await library.OpenDetailsAsync(library.Items[0], TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        await PressAsync(
            host,
            "TitleEditMetadataAction",
            () => host.ViewModel.HasMetadataEditor,
            "clicking Edit details never opened the editor for the open title");

        // Back to the card between the two, and that is the change of 2026-08-28 rather than a
        // detour: the editor is a page of its own now and it covers the card it was opened from, so
        // the second of the card's tools is not on screen while the first one is open. The walk
        // found that the moment the page landed — «Previsualizar renombrado» matched zero controls —
        // which is the same thing a person would have found.
        await PressAsync(
            host,
            "LibraryBackAction",
            () => host.ViewModel.HasEditorPanel,
            "clicking Back never left the editor page and put the card back");
        Assert.False(host.ViewModel.HasEditorPanel);

        await PressAsync(
            host,
            "TitlePreviewRenameAction",
            () => host.ViewModel.HasRename,
            "clicking Rename never offered the preview for the open title");

        // Asking for the preview renames nothing, which is the promise the button makes.
        Assert.True(File.Exists(mediaPath));
    }

    /// <summary>
    /// The eighth batch, second half: the home surface, which is the first thing anybody sees. The
    /// recommendations rail switched on, the entry into the library, and Continue.
    /// </summary>
    /// <remarks>
    /// Continue is the primary action of the whole application — the one control a person reaches for
    /// without looking — and it is the reason this scene exists rather than a unit test: what it has
    /// to do is open the session, and only the assembled application can be asked whether it did.
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_home_surface_is_operated_with_the_mouse()
    {
        // Real video, because Continue has to open a session on the real engine to prove anything.
        var sample = await RequireSampleAsync("walk-home.mp4", durationSeconds: 90);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var mediaPath = Path.Combine(media, "Continue.2024.mp4");
        File.Copy(sample, mediaPath);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var fileId = await SeedMediaFileAsync(factory, media, mediaPath, TimeSpan.FromSeconds(90));

        // A film somebody is part way through: the entry, and the progress that makes Home offer it.
        //
        // Written by a local function because it has to be written more than once. «Desde el
        // principio» opens the session at zero, and closing it stores zero — which is correct, and
        // which takes the hero and the rail card off Home, because neither of them is offered for
        // something nobody is part way through. So the progress is put back between the two presses
        // rather than the two presses being reordered: there is no order in which both survive.
        await SeedMovieRowAsync(factory, fileId, "Continue");
        var watchStates = new WatchStateRepository(factory);
        async Task SeedProgressAsync() => await watchStates.SaveAsync(
            new WatchState
            {
                Content = ContentKey.ForTitle(new TitleId(fileId)),
                Position = TimeSpan.FromSeconds(30),
                ObservedDuration = TimeSpan.FromSeconds(90),
                SourceMediaFileId = new MediaFileId(fileId),
                Status = WatchStatus.InProgress,
                IsManualOverride = false,
                StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                UpdatedUtc = DateTimeOffset.UtcNow,
            },
            TestContext.Current.CancellationToken);
        await SeedProgressAsync();

        using var host = ShowShell(height: 1600);
        Navigate(host, AppRoute.Home);
        var home = host.ViewModel.Home;
        Assert.NotNull(home);
        await home!.LoadAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        // The rail's switch, read from the setting it stores rather than from the rail: switched off,
        // the rail empties instead of hiding a result, and both look the same on screen.
        var recommendations = home.Recommendations;
        Assert.NotNull(recommendations);

        // Its covers first, while the rail still has any: the switch below empties it, and a
        // suggestion nobody can press is a picture of a recommendation. Same button and same class
        // as the library's grid, so this is the third place one card shape is pressed.
        Assert.True(
            recommendations!.Items.Count > 0,
            "The suggestions rail ranked nothing, so its cover could not be pressed.");
        await PressAsync(
            host,
            recommendations.Items[0].OpenAccessibleName,
            () => host.ViewModel.CurrentRoute,
            "clicking a cover on the suggestions rail never opened that title's card",
            recordAs: "{Binding OpenAccessibleName}");
        Assert.Equal(AppRoute.Library, host.ViewModel.CurrentRoute);
        Navigate(host, AppRoute.Home);
        Dispatcher.UIThread.RunJobs();
        var settings = host.Application.Services.GetRequiredService<IRecommendationSettings>();
        var wasEnabled = settings.IsEnabled;
        await PressAsync(
            host,
            "RecommendationsToggleAction",
            () => settings.IsEnabled,
            "clicking the recommendations switch never stored the choice");
        Assert.Equal(!wasEnabled, settings.IsEnabled);

        // Continue, which is what Home is for: the session opens on the file the progress came from,
        // at the position it was left at. Home offers it only for something that can be played right
        // now, so a press that opened nothing would be the offer breaking its own promise.
        Assert.True(home.HasResume, "Home never offered Continue for the film with progress on it.");
        await PressAsync(
            host,
            "HomeResumeAction",
            () => host.ViewModel.Player is not null,
            "clicking Continue never opened the session it offers");
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "Continue opened a session that never reached the playing state");

        // And it does not ask again what pressing Continue already answered. «Al hacer click en
        // continuar vuelve a pedir confirmación de continuar o volver a ver desde el inicio en la
        // vista del reproductor», 2026-08-25: the session opened at the right minute and then
        // offered to decide the minute, over a picture already playing. The offer belongs to a
        // session nobody named a position for, which is what opening a file from Explorer is.
        Assert.Null(host.ViewModel.Player!.Resume);
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);

        // And the progress goes back, because playing it is what spends it. The sample is ninety
        // seconds long and Continue opens it at thirty; a runner busy enough to take three minutes
        // over the next few presses lets the rest of it play out, the tracker stores the end, and
        // the film stops being something to continue — so the hero, which is drawn only when there
        // is, is not on the surface for the press below. CI measured exactly that on 2026-08-25:
        // this scene passed three runs and failed the fourth on "Home came back without the hero's
        // Details on it", with nothing between them that touched the walk.
        //
        // Seeding it again is the same answer the two «from the start» presses further down already
        // needed, and for the same reason written the other way round: what the hero offers is a
        // fact about stored progress, so a scene that spends the progress has to put it back rather
        // than hope the machine was quick.
        await SeedProgressAsync();
        await home.LoadAsync(TestContext.Current.CancellationToken);

        // The hero's second action, which the prototype has had all along: the card of the same
        // title, reached the way the grid reaches it. The route is what is probed because that is
        // what changes — the card it opens is the library's, and Home is left behind.
        // A layout pass between the route change and the click: the hero is drawn by the navigation
        // and a control that has not been arranged yet reports itself as not on screen, which is
        // what "matched 0 controls" means when the same press lands on a faster machine.
        Navigate(host, AppRoute.Home);
        Dispatcher.UIThread.RunJobs();
        await WaitForAsync(
            () => Task.FromResult(Reachable(host).Any(control =>
                control.IsEffectivelyVisible
                && AutomationProperties.GetName(control) == (
                    Avalonia.Application.Current!.TryFindResource("HomeResumeDetailsAction", out var details)
                        && details is string word
                            ? word
                            : "HomeResumeDetailsAction"))),
            "Home came back without the hero's Details on it");
        await PressAsync(
            host,
            "HomeResumeDetailsAction",
            () => host.ViewModel.CurrentRoute,
            "clicking Details on the hero never opened the card of what it offers");
        Assert.Equal(AppRoute.Library, host.ViewModel.CurrentRoute);
        Assert.True(
            host.ViewModel.Library?.IsMovieDetails == true,
            "Details from the hero reached the library and stopped at the grid.");

        // The continue rail's own card, which carries the same two actions per title since
        // 2026-08-25. Its buttons are named by their words AND the title, so a rail of three does
        // not announce the same sentence three times — and that name is what is pressed here.
        Navigate(host, AppRoute.Home);
        Assert.True(home.InProgress.Count > 0, "The continue rail never offered the film with progress on it.");
        var card = home.InProgress[0];
        await PressAsync(
            host,
            card.DetailsAccessibleName,
            () => host.ViewModel.CurrentRoute,
            "clicking Details on a rail card never opened that title's card",
            recordAs: "{Binding DetailsAccessibleName}");
        Assert.Equal(AppRoute.Library, host.ViewModel.CurrentRoute);

        Navigate(host, AppRoute.Home);
        await PressAsync(
            host,
            card.ResumeAccessibleName,
            () => host.ViewModel.Player is not null,
            "clicking Continue on a rail card never opened the session it offers",
            recordAs: "{Binding ResumeAccessibleName}");
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "a rail card's Continue opened a session that never reached the playing state");
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);

        // And put it back again, for the reason the hero's own Continue needed it above: this rail
        // shows what somebody is part way through, so a session that played itself out takes the
        // card off the rail and the press below has nothing to aim at.
        await SeedProgressAsync();
        await home.LoadAsync(TestContext.Current.CancellationToken);

        // «Desde el principio», which arrived on both wide surfaces on 2026-08-25. It opens the same
        // session Continue does and differs in one thing only — the minute it starts at — so what is
        // asserted is that a session opened at all; where it opened is the film card's own question
        // and is measured there.
        Navigate(host, AppRoute.Home);
        await PressAsync(
            host,
            card.RestartAccessibleName,
            () => host.ViewModel.Player is not null,
            "clicking «from the start» on a rail card never opened a session",
            recordAs: "{Binding RestartAccessibleName}");
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);

        // The hero comes back with the navigation and is arranged one pass later, which is the same
        // warm-up the Details press above needs and for the same reason: a control that has not been
        // arranged yet reports itself as not on screen. And the progress goes back first, or there
        // is no hero to warm up — the press before this one stored zero, on purpose.
        await SeedProgressAsync();
        Navigate(host, AppRoute.Home);
        await home.LoadAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        await WaitForAsync(
            () => Task.FromResult(Reachable(host).Any(control =>
                control.IsEffectivelyVisible && control.Name == "ResumeHeroRestart")),
            "Home came back without the hero's «from the start» on it");
        await PressAsync(
            host,
            "HomeRestartAction",
            () => host.ViewModel.Player is not null,
            "clicking «from the start» on the hero never opened a session");
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);

        // And the covers themselves, which were cards nobody could press: a poster on either rail is
        // a button around the whole tile, exactly as it is in the library's grid. «Al hacer click en
        // las tarjetas en home no redirige a la vista detalle del vídeo», 2026-08-25 — they were
        // list items, so pressing one selected it and nothing else.
        Navigate(host, AppRoute.Home);
        Assert.True(
            home.RecentlyAdded.Count > 0,
            "The recently added rail never offered a cover to press.");
        await PressAsync(
            host,
            home.RecentlyAdded[0].OpenAccessibleName,
            () => host.ViewModel.CurrentRoute,
            "clicking a cover on the recently added rail never opened that title's card",
            recordAs: "{Binding OpenAccessibleName}");
        Assert.Equal(AppRoute.Library, host.ViewModel.CurrentRoute);

        // And the way into the library, which is the other thing Home is for.
        Navigate(host, AppRoute.Home);
        await PressAsync(
            host,
            "HomeLibraryAction",
            () => host.ViewModel.CurrentRoute,
            "clicking the library entry never took the shell to the library");
        Assert.Equal(AppRoute.Library, host.ViewModel.CurrentRoute);

        // ---- And the arms every one of Home's hooks carries for a surface that is not there.
        //
        // Each of them reads the shell at the moment of the press rather than capturing it, because
        // these models are built while the shell is still being assembled. What that buys is that a
        // press with nothing behind it does nothing instead of throwing — and the only way to
        // measure it is to take the thing away and press anyway, which is the same shape as the
        // transport's mode handler above.
        Navigate(host, AppRoute.Home);
        await home.LoadAsync(TestContext.Current.CancellationToken);
        var homeShellHost = host.Application.Services.GetRequiredService<CompositionRoot.ShellHost>();
        var liveShell = homeShellHost.Shell;
        homeShellHost.Shell = null;
        try
        {
            home.ResumeCommand.Execute(null);
            home.OpenItemDetailsCommand.Execute(home.RecentlyAdded[0]);
            await SettleAsync();
            Assert.Null(host.ViewModel.Player);
        }
        finally
        {
            homeShellHost.Shell = liveShell;
        }

        // A card for something the catalogue no longer holds, which is what a rail drawn before a
        // removal is holding a moment later. The route must not move: there is no card to open.
        Navigate(host, AppRoute.Home);
        home.OpenItemDetailsCommand.Execute(new ApSolutions.LocalMedia.Presentation.Home.RecentlyAddedItemViewModel(
            new ApSolutions.LocalMedia.Application.Home.RecentlyAddedItem(
            new TitleId(Guid.NewGuid()),
            CatalogTitleKind.Movie,
            "Nada",
            Year: null,
            IsAvailable: true,
            AddedUtc: DateTimeOffset.UnixEpoch)));
        await SettleAsync();
        Assert.Equal(AppRoute.Home, host.ViewModel.CurrentRoute);

        // And a card whose stored progress is gone, which is the same moment from the other rail:
        // Continue has nothing to resume at, so it opens nothing rather than opening at zero.
        home.ResumeItemCommand.Execute(new ApSolutions.LocalMedia.Presentation.Home.InProgressItemViewModel(
            new ApSolutions.LocalMedia.Application.Home.InProgressItem(
            ContentKey.ForTitle(new TitleId(Guid.NewGuid())),
            new TitleId(Guid.NewGuid()),
            CatalogTitleKind.Movie,
            "Nada",
            SeasonNumber: null,
            EpisodeNumber: null,
            EpisodeTitle: null,
            CompletedFraction: 0.5,
            IsAvailable: true,
            UpdatedUtc: DateTimeOffset.UnixEpoch)));
        await SettleAsync();
        Assert.Null(host.ViewModel.Player);
    }

    /// <summary>
    /// The ninth batch: adding a folder to the library and taking it back out, with the mouse. The
    /// three kinds of root, the add, the consent the first scan needs, and the removal with both
    /// answers to its confirmation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the surface a first run opens on, so it is the first thing anybody ever presses, and
    /// until now nothing had pressed it. It was scheduled behind the isolation rule on the assumption
    /// that it opens a folder picker; it does not. The folder is typed into a box, and the picker
    /// lives on the backup surfaces instead — which is worth writing down, because the rest of that
    /// plan was built on it.
    /// </para>
    /// <para>
    /// Removal is pressed twice on purpose: once cancelled and once confirmed. A confirmation that
    /// only ever gets said yes to is a confirmation nobody has shown can be refused, and refusing is
    /// the whole reason it is there — the folder holds somebody's library.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_root_onboarding_is_operated_with_the_mouse()
    {
        var factory = await MigrateCatalogueAsync();
        var folder = Path.Combine(_dataRoot, "library");
        Directory.CreateDirectory(folder);

        using var host = ShowShell(height: 1600);
        Navigate(host, AppRoute.Library);
        var onboarding = host.ViewModel.Onboarding;
        Assert.NotNull(onboarding);

        // The three kinds, each pressed from a different one: the kind it starts on goes last,
        // because pressing the choice already in force has no effect to observe.
        var startsOn = onboarding!.SelectedKind;
        foreach (var kind in new[] { RootKind.Local, RootKind.Usb, RootKind.Unc }
            .Where(other => other != startsOn)
            .Append(startsOn))
        {
            await PressAsync(
                host,
                $"RootKind{kind}",
                () => onboarding.SelectedKind,
                $"clicking the {kind} kind never chose it");
            Assert.Equal(kind, onboarding.SelectedKind);
        }

        // A local folder is what the walk has, so the kind is left on Local before adding.
        Assert.Equal(RootKind.Local, onboarding.SelectedKind);
        onboarding.Path = folder;
        Dispatcher.UIThread.RunJobs();

        // The probe is the catalogue: a surface that accepted the folder and stored nothing would
        // look identical on screen.
        await PressAsync(
            host,
            "RootAddAction",
            () => RootPathsAsync(factory),
            "clicking Add never put the folder in the catalogue");
        Assert.Equal(folder, await RootPathsAsync(factory));
        Assert.Null(onboarding.FailureKey);

        // While the consent is still owed the first-run form is still up, and its own removal
        // confirmation is pressed here — asked and refused — because this is the only state in
        // which the inline pair is reachable at all.
        Assert.True(host.ViewModel.ShowsOnboarding);
        await PressAsync(
            host,
            "RootRemoveAction",
            () => onboarding.IsConfirmingRemoval,
            "clicking Remove on the first-run list never asked for the confirmation it owes");
        await PressAsync(
            host,
            "RootRemoveCancelAction",
            () => onboarding.IsConfirmingRemoval,
            "clicking Cancelar on the first-run list never called off the removal");
        Assert.Equal(folder, await RootPathsAsync(factory));

        // And accepted: the inline confirmation destroys for real - the folder leaves the
        // catalogue and the first run is back at its empty form - then the walk adds it again,
        // because the rest of this scene is about the consent a kept folder owes.
        await PressAsync(
            host,
            "RootRemoveAction",
            () => onboarding.IsConfirmingRemoval,
            "clicking Remove on the first-run list a second time never asked again");
        await PressAsync(
            host,
            "RootRemoveConfirmAction",
            () => RootPathsAsync(factory),
            "confirming on the first-run list never took the folder out of the catalogue");
        Assert.Equal(string.Empty, await RootPathsAsync(factory));

        onboarding.Path = folder;
        Dispatcher.UIThread.RunJobs();
        await PressAsync(
            host,
            "RootAddAction",
            () => RootPathsAsync(factory),
            "re-adding the folder after the inline removal never put it back");
        Assert.Equal(folder, await RootPathsAsync(factory));

        // Nothing is scanned until somebody says so, which is why the consent is a separate press.
        Assert.True(onboarding.InitialScanConsentRequired);
        await PressAsync(
            host,
            "RootScanConsentAction",
            () => onboarding.CanStartInitialScan,
            "clicking the scan consent never granted it");

        // With the folder added and its consent given, the first run is over: ShowsOnboarding puts
        // the inline form away, and managing the folder is Settings' job now — the redistribution
        // the owner decided. The same keys resolve there, on the only surface that shows them.
        Assert.False(
            host.ViewModel.ShowsOnboarding,
            "The first-run form stayed on screen after the first run was over.");
        Navigate(host, AppRoute.Settings);

        await PressAsync(
            host,
            "SettingsSectionLibrary",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Biblioteca y escaneo in the settings index never opened its section");
        Assert.Equal(SettingsSection.Library, host.ViewModel.CurrentSettingsSection);

        // The row for the folder that is now in the catalogue, which is what removal acts on.
        await onboarding.RefreshRootsAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.True(onboarding.HasRoots, "The folder that was added never reached the list.");

        await PressAsync(
            host,
            "RootRemoveAction",
            () => onboarding.IsConfirmingRemoval,
            "clicking Remove never asked for the confirmation it owes");

        // Refused with «Conservar», and the folder is still there: this is the half that proves
        // the confirmation is one rather than a formality.
        await PressAsync(
            host,
            "RootRemoveKeepAction",
            () => onboarding.IsConfirmingRemoval,
            "clicking Conservar never called off the removal");
        Assert.Equal(folder, await RootPathsAsync(factory));

        await PressAsync(
            host,
            "RootRemoveAction",
            () => onboarding.IsConfirmingRemoval,
            "clicking Remove a second time never asked for the confirmation again");

        await PressAsync(
            host,
            "RootRemoveConfirmAction",
            () => RootPathsAsync(factory),
            "clicking Remove for good never took the folder out of the catalogue");
        Assert.Equal(string.Empty, await RootPathsAsync(factory));

        // The folder itself is untouched: removing it from the library is not deleting anybody's
        // videos, and that distinction is the whole promise of this surface.
        Assert.True(Directory.Exists(folder));
    }

    /// <summary>
    /// The ninth batch's other half: the add-root dialog, floating over a populated library. Opened
    /// from the header's primary action, browsed with the isolated picker, the kind detected from
    /// the path, a folder added through it — which closes it — and dismissed with the ✕.
    /// </summary>
    /// <remarks>
    /// The library is seeded first so the first-run form is away: the dialog's controls must be the
    /// only instances of their keys on screen, and this scene is also what proves they are.
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_add_root_dialog_is_operated_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var film = Path.Combine(media, "Arrival.2016.mp4");
        await File.WriteAllBytesAsync(film, [0x41, 0x50], TestContext.Current.CancellationToken);
        _ = await SeedMediaFileAsync(factory, media, film, TimeSpan.FromMinutes(116));

        using var host = ShowShell(height: 1600);
        Navigate(host, AppRoute.Library);
        var shell = host.ViewModel;
        var onboarding = shell.Onboarding;
        Assert.NotNull(onboarding);
        await onboarding!.RefreshRootsAsync(TestContext.Current.CancellationToken);
        var library = shell.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.False(shell.ShowsOnboarding, "A populated library still showed the first-run form.");

        // The header's primary action opens the dialog.
        await PressAsync(
            host,
            "LibraryAddMediaAction",
            () => shell.IsAddingRoot,
            "clicking Añadir medios… never opened the add-root dialog");
        Assert.True(shell.IsAddingRoot);

        // Browse answers from inside the handover root — the isolated exit the archive pickers
        // taught — and the detector reads the answer's kind.
        await PressAsync(
            host,
            "AddRootBrowseAction",
            () => onboarding.Path,
            "clicking Examinar… never put the picked folder in the path box");
        Assert.True(Directory.Exists(onboarding.Path), "Browse answered a folder that does not exist.");
        Assert.Equal(RootKind.Local, onboarding.SelectedKind);

        // The kind follows the path: a UNC prefix is enough for the real detector.
        onboarding.Path = @"\\nas\cine";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(RootKind.Unc, onboarding.SelectedKind);

        // A real folder, added through the dialog: the catalogue gains it and the dialog closes
        // itself — the form's job is done, and the consent the first scan needs is asked by the
        // surface the route shows.
        var second = Path.Combine(_dataRoot, "more-media");
        Directory.CreateDirectory(second);
        onboarding.Path = second;
        Dispatcher.UIThread.RunJobs();
        await PressAsync(
            host,
            "RootAddAction",
            () => RootPathsAsync(factory),
            "clicking Añadir carpeta in the dialog never put the folder in the catalogue",
            recordAs: DialogAction);
        Assert.Contains(second, await RootPathsAsync(factory));
        Assert.False(shell.IsAddingRoot, "A successful add left the dialog on screen.");
        Assert.True(onboarding.InitialScanConsentRequired);

        // Reopened and dismissed with the ✕, which is the other way out.
        await PressAsync(
            host,
            "LibraryAddMediaAction",
            () => shell.IsAddingRoot,
            "clicking Añadir medios… never reopened the dialog");
        await PressAsync(
            host,
            "AddRootDismissAction",
            () => shell.IsAddingRoot,
            "clicking the dialog's close never put it away");
        Assert.False(shell.IsAddingRoot);
    }

    /// <summary>
    /// The folders the catalogue holds, which is where adding and removing one lands.
    /// </summary>
    /// <remarks>
    /// One string rather than the list it reads, because a probe is compared with
    /// <see cref="EqualityComparer{T}"/>: an array answers "changed" on every read, since each read
    /// is a new array. Measured here as the beside click appearing to remove a folder — with the
    /// empty case passing, because an empty array is the same shared instance every time.
    /// </remarks>
    private static async Task<string> RootPathsAsync(SqliteConnectionFactory factory)
    {
        var roots = await new LibraryRootRepository(factory).ListAsync(TestContext.Current.CancellationToken);
        return string.Join(";", roots.Select(root => root.Path).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The rest of the library, fetched by pressing for it.
    /// </summary>
    /// <remarks>
    /// <b>Until 2026-09-04 there was nothing to press.</b> The model has paged by cursor since T7 —
    /// fifty to a page, with `HasMore` and `LoadMoreCommand` — and no view bound any of it, so a
    /// library of more than fifty titles had fifty reachable ones. `LIB-004` promises ten thousand.
    /// <para>
    /// Fifty-one titles and not two, because the button only exists when there is a second page: a
    /// scene seeded with the usual handful would walk past a control that was never there, and pass.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_rest_of_the_library_is_fetched_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var repository = new CatalogRepository(factory);

        for (var index = 0; index < 51; index++)
        {
            var name = $"Title {index:D3}";
            await repository.UpsertTitleAsync(
                new CatalogTitle(
                    new TitleId(Guid.NewGuid()),
                    CatalogTitleKind.Movie,
                    name,
                    name.ToLowerInvariant(),
                    2024,
                    [],
                    [],
                    [],
                    DateTimeOffset.UnixEpoch,
                    LastPlayedUtc: null,
                    HasProgress: false,
                    IsPersonal: false,
                    IsAvailable: true),
                TestContext.Current.CancellationToken);
        }

        using var host = ShowShell();
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(50, library.Items.Count);
        Assert.True(library.HasMore, "Fifty-one titles in fifty-sized pages leaves a second page.");

        await PressAsync(
            host,
            // The anchor the inventory records, which is the accessible name's resource key and not
            // the control's own name: eng/check-walk-coverage.ps1 reads the views for the first and
            // the walk would otherwise press something the inventory does not know it has.
            "LibraryLoadMoreAction",
            () => Task.FromResult(library.Items.Count),
            "pressing for the rest of the library brought nothing back");

        Assert.Equal(51, library.Items.Count);
        Assert.False(library.HasMore, "There was one title left, so nothing is left after it.");
    }

    /// <summary>
    /// The catalogue row a film has once somebody identified it, written through SQL because the
    /// catalogue writes it during identification, which needs the network the harness does not have.
    /// </summary>
    private static async Task SeedMovieRowAsync(SqliteConnectionFactory factory, Guid titleId, string title)
    {
        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO titles (id, kind, primary_title, sort_title, release_year, added_utc,
                                last_played_utc, has_progress, is_personal, is_available)
            VALUES ($id, 0, $title, $sort, 2024, $added, NULL, 1, 0, 1);
            """;
        command.Parameters.AddWithValue("$id", titleId.ToString("D"));
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$sort", title.ToLowerInvariant());
        command.Parameters.AddWithValue("$added", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
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

        // The five stars share one accessible name by design and are told apart by the score itself,
        // which is what a screen reader reads after the name. Four of five since 2026-08-25, when
        // the row stopped being ten numbered squares.
        await PressAsync(
            host,
            "PersonalRatingLabel",
            () => personal.Rating,
            "clicking a star never recorded the rating",
            helpText: "4");
        Assert.Equal(4, personal.Rating);

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
        // opens the real file the walk put on the disk. This card has no progress on it, so the one
        // button says «Reproducir» and the «from the start» glyph beside it is not drawn at all —
        // there is nothing for it to be the alternative to.
        await PressAsync(
            host,
            host.ViewModel.Library!.MovieDetails.PlayActionText,
            () => host.ViewModel.Player is not null,
            "clicking Play never opened a session on the film",
            recordAs: "{Binding PlayActionText}");
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
        string Recorded() => ReadRecord(record);

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
            RecordedLines(record));

        await library.OpenDetailsAsync(
            library.Items.Single(item => item.Item.Kind == CatalogTitleKind.Show),
            TestContext.Current.CancellationToken);
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
            RecordedLines(record));
    }

    /// <summary>
    /// The fifth batch: the review inbox, decided with the mouse — one more page, an accepted match,
    /// a rejected one, and the manual search.
    /// </summary>
    /// <remarks>
    /// Every probe here reads the <b>catalogue</b> rather than the list on screen. A decision that
    /// only removed a card would look identical to one that was stored, and this is the surface where
    /// what was decided has to outlive the screen it was decided on.
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_review_inbox_is_decided_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var mediaPath = Path.Combine(media, "Arrival.2016.mp4");

        // Nothing decodes on this surface, so the file only has to exist and be catalogued.
        await File.WriteAllBytesAsync(mediaPath, [0x41, 0x50], TestContext.Current.CancellationToken);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var fileId = await SeedMediaFileAsync(factory, media, mediaPath, TimeSpan.FromMinutes(116));

        // One more than a page, so "load more" has something left to load.
        var candidates = new MatchCandidateRepository(factory);
        await candidates.ReplaceForMediaFileAsync(
            new MediaFileId(fileId),
            [.. Enumerable.Range(1, 26).Select(index => new MatchCandidate(
                CandidateId.FromStableKey($"movie:90000{index}"),
                new MediaFileId(fileId),
                $"movie:90000{index}",
                CandidateContentKind.Movie,
                0.40 + (index / 1000.0),
                CandidateScorer.ScoringModelVersion,
                ReviewState.Pending,
                [new MatchSignal("Identification.Signal.Title", 0.40, 0.5)],
                ["Identification.Signal.Title"]))],
            TestContext.Current.CancellationToken);

        // What a previous lookup would have cached for the words a person types into the search box,
        // and for the entry those words find. Nothing connects: with no token the provider serves
        // what it has stored, which is the shipped offline path exactly.
        var now = DateTimeOffset.UtcNow;
        var cache = new SqliteMetadataCache(factory);
        var version = new TmdbOptions(accessToken: null).ProviderVersion;
        await cache.StoreAsync(
            new MetadataCacheEntry(
                new MetadataCacheKey("tmdb", "search:movie:la-llegada:2016", "es-ES", version),
                """
                { "results": [ { "id": 329865, "title": "La llegada", "original_title": "Arrival",
                                 "release_date": "2016-11-11" } ] }
                """,
                ETag: null,
                now,
                now.AddDays(1)),
            TestContext.Current.CancellationToken);
        await cache.StoreAsync(
            new MetadataCacheEntry(
                new MetadataCacheKey("tmdb", "movie:329865", "es-ES", version),
                """
                {
                  "title": "La llegada",
                  "original_title": "Arrival",
                  "overview": "Una lingüista traduce a los visitantes.",
                  "release_date": "2016-11-11",
                  "genres": [{ "name": "Ciencia ficción" }]
                }
                """,
                ETag: null,
                now,
                now.AddDays(1)),
            TestContext.Current.CancellationToken);

        using var host = ShowShell(height: 2000);
        Navigate(host, AppRoute.Review);
        var inbox = host.ViewModel.ReviewInbox;
        Assert.NotNull(inbox);
        await WaitForAsync(
            () => Task.FromResult(inbox!.Items.Count == 25),
            "the review inbox never loaded its first page");
        Dispatcher.UIThread.RunJobs();

        await PressAsync(
            host,
            "ReviewLoadMoreAction",
            () => inbox!.Items.Count,
            "clicking Load more never brought the rest of the inbox in");
        Assert.Equal(26, inbox!.Items.Count);

        // The decision is read from the catalogue, and the card is chosen with the mouse: the list
        // has no other way of knowing which candidate a person means.
        var accepted = VisibleCard(host, inbox);
        await PressAsync(
            host,
            "ReviewAcceptAction",
            () => CountDecidedAsync(factory, fileId, ReviewState.Accepted),
            "clicking Accept never stored the decision on the candidate",
            helpText: accepted);
        Assert.Equal(1, await CountDecidedAsync(factory, fileId, ReviewState.Accepted));

        // The one that was clicked, and not merely one of them: the control click used to land on a
        // card and move the selection, and "a candidate was accepted" was true either way.
        Assert.Equal(accepted, await DecidedKeyAsync(factory, fileId, ReviewState.Accepted));

        // And it leaves the inbox, which is the other half of the decision: the list is what is still
        // waiting for somebody. It arrives on the command's continuation, after the row is written.
        await WaitForAsync(
            () => Task.FromResult(inbox.Items.All(item => item.StableKey != accepted)),
            "the accepted candidate was written to the catalogue and stayed in the inbox anyway");

        var rejected = VisibleCard(host, inbox);
        await PressAsync(
            host,
            "ReviewRejectAction",
            () => CountDecidedAsync(factory, fileId, ReviewState.Rejected),
            "clicking Reject never stored the refusal on the candidate",
            helpText: rejected);
        Assert.Equal(1, await CountDecidedAsync(factory, fileId, ReviewState.Rejected));
        Assert.Equal(rejected, await DecidedKeyAsync(factory, fileId, ReviewState.Rejected));
        await WaitForAsync(
            () => Task.FromResult(inbox.Items.All(item => item.StableKey != rejected)),
            "the rejected candidate was written to the catalogue and stayed in the inbox anyway");

        // And the search a person types when none of the offers is right. The answer comes out of the
        // provider's own cache, so the real provider, parser and scorer all take part — and because
        // what the words find leaves no doubt, the identification is finished rather than queued: the
        // probe is the catalogue row, which is what a person came here to change.
        // The card's own way in, which is where the prototype puts it: pressing it starts a manual
        // search for THAT file, and what it puts in the box is the file's own name — which is what a
        // person would type first and what the parser already knows how to read.
        var searched = VisibleCard(host, inbox);
        await PressAsync(
            host,
            "ReviewManualSearchCardAction",
            () => inbox.ManualSearch ?? string.Empty,
            "clicking Search manually on a card never started a search for that file",
            helpText: searched);
        Assert.False(
            string.IsNullOrWhiteSpace(inbox.ManualSearch),
            "Search manually left the box empty, so it asked for nothing.");

        inbox.ManualSearch = "La llegada 2016";
        Dispatcher.UIThread.RunJobs();
        await PressAsync(
            host,
            "ReviewManualSearchAction",
            () => StoredTitleAsync(factory, fileId),
            "clicking Search never asked the provider for what was typed");
        Assert.Equal("La llegada", await StoredTitleAsync(factory, fileId));

        // The wrong offers are gone with it: what the file now has is the answer to the search.
        await WaitForAsync(
            () => Task.FromResult(inbox.Items.Count == 0),
            "the candidates the search replaced stayed in the inbox");
    }

    /// <summary>
    /// Picks a candidate card with the mouse, and answers with the key it picked.
    /// </summary>
    /// <remarks>
    /// Whichever card is <b>on screen</b>, not whichever is first in the model. The list virtualises
    /// against the window rather than against its own height — measured with 26 candidates in a
    /// 2000 px window, where the first eight cards had been recycled away and only the ninth onwards
    /// existed to be clicked. A person clicks a card they can see, and so does this.
    /// <para>
    /// The card is not a command control and is not recorded as one: what it does is give the buttons
    /// below it something to act on.
    /// </para>
    /// </remarks>
    private static string VisibleCard(ShellHost host, ReviewInboxViewModel inbox)
    {
        // Back to the top first, and only then look at what is on screen. Pressing a card is itself a
        // Reveal, and revealing recycles the containers a virtualised list had materialised further
        // down: a card resolved before the page moved is a card with no position by the time the
        // click lands — measured as "Border has no position in the window".
        foreach (var scroller in host.Shell.GetVisualDescendants().OfType<ScrollViewer>())
        {
            scroller.Offset = scroller.Offset.WithY(0);
        }

        host.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var onScreen = host.Shell.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.IsEffectivelyVisible)
            .Select(AutomationProperties.GetName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var key = inbox.Items.Select(item => item.StableKey).FirstOrDefault(onScreen.Contains);
        Assert.True(
            key is not null,
            $"None of the {inbox.Items.Count} candidates in the inbox has a card on screen to click.");

        // The card is not clicked any more, because there is nothing to select: each card carries
        // its own three decisions and every one of them speaks for the file it sits under. What this
        // answers is which card is REACHABLE, which is what a person clicking one has to be.
        return key!;
    }

    /// <summary>
    /// How many of a file's candidates carry one decision, read from the catalogue rather than from
    /// the list on screen: a decision that only removed a card would look the same as one that was
    /// stored.
    /// </summary>
    private static async Task<int> CountDecidedAsync(
        SqliteConnectionFactory factory,
        Guid fileId,
        ReviewState state)
    {
        var stored = await new MatchCandidateRepository(factory).GetForMediaFileAsync(
            new MediaFileId(fileId),
            TestContext.Current.CancellationToken);
        return stored.Count(candidate => candidate.ReviewState == state);
    }

    /// <summary>The title the catalogue holds for a file, which is where an identification lands.</summary>
    private static async Task<string?> StoredTitleAsync(SqliteConnectionFactory factory, Guid fileId)
    {
        var stored = await new CatalogMetadataRepository(factory).GetAsync(
            new TitleId(fileId),
            TestContext.Current.CancellationToken);
        return stored?.Metadata.Title;
    }

    /// <summary>Which candidate carries the decision — the question that catches a press on the wrong one.</summary>
    private static async Task<string?> DecidedKeyAsync(
        SqliteConnectionFactory factory,
        Guid fileId,
        ReviewState state)
    {
        var stored = await new MatchCandidateRepository(factory).GetForMediaFileAsync(
            new MediaFileId(fileId),
            TestContext.Current.CancellationToken);
        return stored.SingleOrDefault(candidate => candidate.ReviewState == state)?.StableKey;
    }

    /// <summary>
    /// The rest of the fifth batch: a held moved file, decided with the mouse. One offer is confirmed
    /// against a chosen candidate, and the next is kept as a file of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The state is the one the reconciler really produces, and only one thing produces it:
    /// <c>FileReconciliationPolicy</c> answers <c>Exact</c> for a stable file id and for a lone
    /// fingerprint, so the <b>only</b> offer a person is ever asked to decide is a fingerprint
    /// collision — two catalogued rows carrying the discovered file's fingerprint, and the person
    /// says which of them the file is. Seeding one candidate would have been easier and would have
    /// staged a screen the application cannot reach.
    /// </para>
    /// <para>
    /// Which is also why this scene found a defect. Two candidates mean two Confirm buttons, and both
    /// carried the same accessible name and nothing else: <c>ReassignmentConfirmAction</c> matched two
    /// controls on screen, so the walk could not aim, and neither could anybody driving this surface
    /// by name. Every other repeated command in the application already distinguishes itself —
    /// <c>EpisodeRowView</c> by its episode, the duplicate rows by their path — and this one now
    /// carries the candidate's path as its help text, which is what the click aims at here.
    /// </para>
    /// <para>
    /// Both probes read the catalogue. Confirming asks <b>which</b> row ended up at the discovered
    /// path rather than whether one did: the walk presses the second candidate on purpose, and the
    /// first is asserted to be untouched, because a press that landed on the other button would leave
    /// a reassignment that looks just as finished and is the wrong one.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task A_held_moved_file_is_reassigned_to_the_chosen_candidate_and_the_next_is_kept_as_new()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var rootId = await RootIdAsync(factory, media);

        // Nothing decodes on this surface, so the files only have to exist and be catalogued.
        var reassigned = await SeedHeldOfferAsync(factory, media, rootId, "Arrival", "sha256:v1:ARRIVAL");
        var kept = await SeedHeldOfferAsync(factory, media, rootId, "Sicario", "sha256:v1:SICARIO");

        using var host = ShowShell(height: 2000);
        var inbox = host.ViewModel.ReviewInbox;
        Assert.NotNull(inbox);
        Navigate(host, AppRoute.Review);

        // The queue reconciliation writes into, taken from the application's own container: the offer
        // has to be the one the surface reads, not a copy of it.
        var queue = host.Application.Services.GetRequiredService<PendingReassignments>();
        var files = new MediaFileRepository(factory);

        // One offer at a time. Two would put two Keep buttons on screen, and a click that has to
        // choose between identical controls is the defect this scene already found once.
        queue.Offer(reassigned.Pending);
        await ShowOfferAsync(host, inbox!);

        // The second candidate, so that "a reassignment happened" cannot stand in for "the one the
        // person asked for happened".
        var chosen = reassigned.Pending.Candidates[1];
        await PressAsync(
            host,
            "ReassignmentConfirmAction",
            () => RowAtAsync(files, rootId, reassigned.DiscoveredPath),
            "clicking Confirm never moved the catalogued entity to the discovered path",
            helpText: chosen.Path);
        Assert.Equal(chosen.MediaFileId.Value, await RowAtAsync(files, rootId, reassigned.DiscoveredPath));

        // And the candidate that was not pressed is still where it was, with its own row intact.
        var untouched = reassigned.Pending.Candidates[0];
        Assert.Equal(
            untouched.MediaFileId.Value,
            await RowAtAsync(files, rootId, untouched.Path));

        // The offer leaves the surface with the decision, which is the other half of deciding it.
        await WaitForAsync(
            () => Task.FromResult(!inbox!.HasReassignments),
            "the confirmed offer was written to the catalogue and stayed on the surface anyway");

        queue.Offer(kept.Pending);
        await ShowOfferAsync(host, inbox!);

        // Keeping it as new stores the discovered identity on the discovered row, and that is the
        // whole point: no later scan can offer this file again.
        await PressAsync(
            host,
            "ReassignmentKeepAction",
            () => FingerprintOfAsync(files, kept.DiscoveredId),
            "clicking It is a new file never stored the identity that stops the offer returning");
        Assert.Equal("sha256:v1:SICARIO", await FingerprintOfAsync(files, kept.DiscoveredId));

        // The rows the offer named are all still their own entities: keeping a file as new decides
        // one row and moves nothing.
        Assert.Equal(kept.DiscoveredId.Value, await RowAtAsync(files, rootId, kept.DiscoveredPath));
        foreach (var candidate in kept.Pending.Candidates)
        {
            Assert.Equal(candidate.MediaFileId.Value, await RowAtAsync(files, rootId, candidate.Path));
        }
        await WaitForAsync(
            () => Task.FromResult(!inbox!.HasReassignments),
            "the offer that was kept as new stayed on the surface anyway");
    }

    /// <summary>
    /// What reconciliation holds for a person: two catalogued rows sharing one fingerprint, and the
    /// stranger row a scan wrote for the path the file turned up at.
    /// </summary>
    private static async Task<HeldOffer> SeedHeldOfferAsync(
        SqliteConnectionFactory factory,
        string media,
        LibraryRootId rootId,
        string name,
        string fingerprint)
    {
        var files = new MediaFileRepository(factory);
        var identity = new FileIdentity(null, null, fingerprint);
        var candidates = new List<ReassignmentCandidate>();
        foreach (var copy in new[] { "first", "second" })
        {
            var seeded = await SeedFileAsync(factory, media, $"{name}.{copy}.mp4");
            await files.SaveIdentityAsync(
                new MediaFileId(seeded.Id),
                identity,
                TestContext.Current.CancellationToken);
            candidates.Add(new ReassignmentCandidate(new MediaFileId(seeded.Id), seeded.Path));
        }

        // The discovered file carries no stored identity, which is exactly why the offer survives a
        // restart: every later scan re-derives it until somebody decides.
        var discovered = await SeedFileAsync(factory, media, Path.Combine("archive", $"{name}.mp4"));
        return new HeldOffer(
            new PendingReassignment(
                new ReconcileFileCommand(rootId, discovered.Path, identity),
                candidates),
            discovered.Path,
            new MediaFileId(discovered.Id));
    }

    private static async Task<(string Path, Guid Id)> SeedFileAsync(
        SqliteConnectionFactory factory,
        string media,
        string relativePath)
    {
        var path = Path.Combine(media, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, [0x41, 0x50], TestContext.Current.CancellationToken);
        return (path, await SeedMediaFileAsync(factory, media, path, TimeSpan.FromMinutes(116)));
    }

    /// <summary>Puts the offer the queue is now holding on screen, and waits for it to arrive.</summary>
    private static async Task ShowOfferAsync(ShellHost host, ReviewInboxViewModel inbox)
    {
        // ReloadReassignments only runs on a load, so the surface is asked to load rather than
        // expected to have noticed.
        await inbox.LoadAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        await WaitForAsync(
            () => Task.FromResult(inbox.HasReassignments),
            "the queue was holding an offer and the review surface never showed it");
    }

    /// <summary>Which row sits at a path, read from the catalogue: the question a reassignment answers.</summary>
    private static async Task<Guid?> RowAtAsync(
        MediaFileRepository files,
        LibraryRootId rootId,
        string path)
    {
        var row = await files.FindByPathAsync(rootId, path, TestContext.Current.CancellationToken);
        return row?.Id.Value;
    }

    /// <summary>The identity a row carries, which is what keeping a file as new writes down.</summary>
    private static async Task<string?> FingerprintOfAsync(MediaFileRepository files, MediaFileId id)
    {
        var identity = await files.GetIdentityAsync(id, TestContext.Current.CancellationToken);
        return identity?.Fingerprint;
    }

    private static async Task<LibraryRootId> RootIdAsync(SqliteConnectionFactory factory, string media)
    {
        var roots = await new LibraryRootRepository(factory).ListAsync(TestContext.Current.CancellationToken);
        return roots.Single(candidate => candidate.Path == media).Id;
    }

    /// <summary>One held offer, with the discovered row the surface decides about.</summary>
    private sealed record HeldOffer(
        PendingReassignment Pending,
        string DiscoveredPath,
        MediaFileId DiscoveredId);

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
            new PlayDetailsRequest(new MediaFileId(fileId), StartPosition: null),
            TestContext.Current.CancellationToken);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the session never reached the playing state on the real engine");
        Dispatcher.UIThread.RunJobs();

        var player = host.ViewModel.Player!.Player;
        var transport = player.Transport;
        Assert.NotNull(transport);

        await PressAsync(
            host,
            "PlayerPauseAction",
            () => player.IsPaused,
            "clicking Pause never paused the session the engine was decoding");

        // The skips are measured on a PAUSED session, and the order is load-bearing rather than
        // tidy. Every press here is proved twice — a click beside it must change nothing, then the
        // click itself must change something — and since 2026-08-24 the bar follows the playhead, so
        // on a playing session the beside-click "changes" the position too, by a second of film
        // going by. Paused, the only thing that can move the playhead is the press. The clock is
        // read in whole seconds for the same reason: a trailing event from the pause carries
        // milliseconds, and a skip carries ten seconds.
        await PressAsync(
            host,
            "TransportSkipForward",
            () => Math.Round(transport!.Position.TotalSeconds),
            "clicking the forward skip never moved the playhead");

        // The bar's own rule: one skip in flight refuses the next, so each press is given its answer
        // before the following one.
        await PressAsync(
            host,
            "TransportSkipBackward",
            () => Math.Round(transport!.Position.TotalSeconds),
            "clicking the backward skip never moved the playhead");

        await PressAsync(
            host,
            "PlayerPlayAction",
            () => player.IsPlaying,
            "clicking Play never resumed the paused session");

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

        // The scrubber, which arrived on 2026-08-22 and is the one control on this bar that says
        // where in the film somebody is. The probe is the engine's position and not the thumb: a bar
        // that moved its own thumb and left the session where it was is exactly the state the volume
        // slider was in for four months.
        //
        // The session is sent near the end first, and that is the harness rather than the test: the
        // walk presses a range control a quarter along, and after the two skips the playhead sat at
        // roughly that quarter - so the click landed on the thumb itself, which starts a drag and
        // changes no value. Measured here: "a click reaches Border inside thumb inside PART_Track".
        Assert.True(transport.HasDuration, "The engine never said how long the file is, so there is no bar to press.");

        // Paused first, and that is the harness rather than the test: since 2026-08-24 the transport
        // OBSERVES the engine's position, so a session left playing moves the probe by itself — and
        // the click beside, which has to change nothing, changes something on any runner slow enough
        // for a frame to go by between the two reads. Measured on CI, twice.
        player.PauseCommand.Execute(null);
        await WaitForAsync(() => Task.FromResult(player.IsPaused), "the session never paused for the scrubber");
        await transport.SeekAsync(TimeSpan.FromSeconds(80), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        await PressAsync(
            host,
            "TransportPositionLabel",
            () => transport.Position,
            "clicking the scrubber never moved the session to the chosen minute");

        // The speed menu, which took the prototype's shape on 2026-08-28 and is a ComboBox now: a
        // drop-down's effect is that it opens, and what is chosen inside lands in a popup root of its
        // own that this harness cannot reach — the rows are measured in SpeedMenuTests against the
        // engine's own speed. Closed by the control's own property rather than Escape to the window,
        // because the popup is a top level of its own and the window's Escape is not its business.
        await PressAsync(
            host,
            "TransportSpeedLabel",
            () => Resolve(host, "TransportSpeedLabel") is ComboBox { IsDropDownOpen: true },
            "clicking the speed pill never opened the menu of speeds");
        ((ComboBox)Resolve(host, "TransportSpeedLabel")).IsDropDownOpen = false;
        Dispatcher.UIThread.RunJobs();

        // And «Volver a 1×», which the prototype puts beside the pill and this application had as
        // the menu's eleventh row — a thing that is not a speed, in a list of speeds, behind the
        // click that opens that list.
        //
        // The session is sent away from 1× first, and that is the scene rather than the test: the
        // button is absent while there is nothing to come back from, so a walk that pressed it
        // straight away would be pressing something that is not on the screen. The speed is spent
        // here and the press is what puts it back, which is this suite's own rule about a scene that
        // spends something — except that here the repayment IS the effect being measured.
        await transport.SetSpeedAsync(1.5, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        // This scene is where the race was found, twice, and the settling that answered it does not
        // live here any more: it is in Reveal, which every press goes through. «Volver a 1×» appears
        // when the speed leaves 1×, so this line changes the composition of the row and everything
        // beside it moves — and so does pressing the reset, which removes that button again. Fixing
        // the line somebody remembered is what left the second half of it standing on 2026-08-28.
        Dispatcher.UIThread.RunJobs();
        Assert.True(transport.IsAwayFromNormalSpeed, "the session never left 1×, so the reset has nothing to do.");

        // And the mode is read here as well as after the press, so that a beside-click that moves it
        // is caught where it happened rather than three assertions later.
        Assert.Equal(PlaybackMode.Embedded, host.ViewModel.PlaybackMode);
        await PressAsync(
            host,
            "TransportSpeedResetAction",
            () => transport.SpeedMultiplier,
            "clicking «back to 1×» never brought the session back to normal speed");
        Assert.Equal(1.0, transport.SpeedMultiplier);

        // The bar's own two mode buttons, which is where the owner looked for them on 2026-08-25 and
        // where they were not. Pressed from the bar and read off the shell's mode, because a button
        // that only changed its own look would leave the picture exactly where it was. There is one
        // of each on screen and not two: the pair that used to sit in the header above the picture
        // went with these arriving, since one name for two buttons is a name that names neither.
        Assert.Equal(PlaybackMode.Embedded, host.ViewModel.PlaybackMode);
        await PressAsync(
            host,
            "TransportFullscreenAction",
            () => host.ViewModel.PlaybackMode,
            "clicking the transport's full screen never moved the picture onto the whole screen");
        Assert.Equal(PlaybackMode.Fullscreen, host.ViewModel.PlaybackMode);
        await PressAsync(
            host,
            "TransportFullscreenAction",
            () => host.ViewModel.PlaybackMode,
            "clicking it again never brought the picture back into the shell");
        Assert.Equal(PlaybackMode.Embedded, host.ViewModel.PlaybackMode);

        // And the same button with no shell above it to reach, which is the arm the composition
        // writes and nothing had ever taken. The bar travels: the mini window mounts the very same
        // control, and a session outlives more than one call of that handler, so the shell is asked
        // for at the moment of the press rather than captured. What that buys is exactly this — a
        // press with no shell does nothing instead of throwing — and the way to measure it is to
        // take the shell away and press anyway.
        var shellHost = host.Application.Services.GetRequiredService<CompositionRoot.ShellHost>();
        var live = shellHost.Shell;
        shellHost.Shell = null;
        try
        {
            await host.ViewModel.Player!.Player.ModeHandler!(PlaybackMode.Fullscreen);
        }
        finally
        {
            shellHost.Shell = live;
        }

        Assert.Equal(PlaybackMode.Embedded, host.ViewModel.PlaybackMode);

        await PressAsync(
            host,
            "PlayerStopAction",
            () => player.IsStopped,
            "clicking Stop never stopped the session");

        // And the floating window, which is pressed last of the three because the bar stands down
        // inside it: the small window draws five controls of its own rather than carrying this one
        // with it, so there is no second press of this button to make from there.
        Assert.Equal(PlaybackMode.Embedded, host.ViewModel.PlaybackMode);
        await PressAsync(
            host,
            "TransportPictureInPictureAction",
            () => host.ViewModel.PlaybackMode,
            "clicking the transport's floating window never moved the picture into one");
        Assert.Equal(PlaybackMode.Mini, host.ViewModel.PlaybackMode);

        // And closing, which is the one that ends the session rather than moving it: the shell holds
        // no player afterwards, and the mode goes back to where a next session starts.
        await PressAsync(
            host,
            "PlayerCloseAction",
            () => host.ViewModel.Player is null,
            "clicking Close never let go of the session the shell was holding");
        Assert.Equal(PlaybackMode.Embedded, host.ViewModel.PlaybackMode);
    }

    /// <summary>
    /// The mini player's own five controls, pressed with the mouse inside the window they live in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the first scene in this repository that presses anything outside the shell's window,
    /// and it is why the harness learned to aim: until 2026-08-19 <c>Resolve</c> searched the shell
    /// alone and every click was translated into the shell's window, so a control in the mini player
    /// would have been invisible to the walk — which reads as "the application does not declare it"
    /// rather than "the harness cannot see it".
    /// </para>
    /// <para>
    /// The order is not arbitrary. The two skips go before the pause, because a paused session is not
    /// the state either skip was measured in; Restore is what takes the session back to the shell, so
    /// it comes after everything that needs the mini window on screen; and Close ends the session, so
    /// the walk returns to the mini mode once more to press it.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_mini_players_five_controls_are_pressed_with_the_mouse()
    {
        var sample = await RequireSampleAsync("walk-mini.mp4", durationSeconds: 90);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var mediaPath = Path.Combine(media, "Mini.2024.mp4");
        File.Copy(sample, mediaPath);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var fileId = await SeedMediaFileAsync(factory, media, mediaPath, TimeSpan.FromSeconds(90));

        using var host = ShowShell(height: 1200);
        await host.ViewModel.OpenPlayerAsync(
            new PlayDetailsRequest(new MediaFileId(fileId), StartPosition: null),
            TestContext.Current.CancellationToken);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the session never reached the playing state on the real engine");

        var player = host.ViewModel.Player!.Player;
        var transport = player.Transport;
        Assert.NotNull(transport);

        await EnterMiniModeAsync(host);

        // The five are measured against the window before a single click, because a control that
        // falls outside it is the shape that has cost this repository six measurements - and here the
        // window is 480 wide and holds five buttons. They carried translated words until 2026-08-21,
        // which is what folded this chrome into three rows; they carry glyphs now, and the accessible
        // names the presses below aim at did not move with them.
        foreach (var anchor in new[]
        {
            "MiniPlayerPlayPause",
            "MiniPlayerSkipBack",
            "MiniPlayerSkipForward",
            "MiniPlayerRestore",
            "MiniPlayerClose",
        })
        {
            var control = Resolve(host, anchor);
            var window = RootOf(host, control);
            var corner = control.TranslatePoint(
                new Point(control.Bounds.Width, control.Bounds.Height),
                window);
            Assert.True(
                corner is { } point
                    && point.X <= window.Bounds.Width
                    && point.Y <= window.Bounds.Height,
                $"{anchor} ends at {corner} in a {window.Bounds.Width:F0}x{window.Bounds.Height:F0} "
                    + "mini player, so a press would land outside the window it lives in.");
        }

        // One control for two answers: what it does is read from the state, so pressing it on a
        // playing session pauses it. That the same button resumes is PlayerViewModelTests' question,
        // and asking it here would need a second press this ledger would not record.
        //
        // It is pressed FIRST so the two skips below are measured on a stopped playhead: the bar
        // follows the engine since 2026-08-24, and on a playing session the click beside a skip
        // moves the position as surely as the skip does, which leaves the walk unable to tell a
        // press from a second of film.
        await PressAsync(
            host,
            "MiniPlayerPlayPause",
            () => player.IsPaused,
            "clicking the mini player's pause never paused the session the engine was decoding");
        Assert.True(player.IsPaused);

        await PressAsync(
            host,
            "MiniPlayerSkipForward",
            () => Math.Round(transport!.Position.TotalSeconds),
            "clicking the mini player's forward skip never moved the playhead");

        await PressAsync(
            host,
            "MiniPlayerSkipBack",
            () => Math.Round(transport!.Position.TotalSeconds),
            "clicking the mini player's backward skip never moved the playhead");

        await PressAsync(
            host,
            "MiniPlayerRestore",
            () => host.ViewModel.PlaybackMode,
            "clicking the mini player's restore never took the session back to the shell");
        Assert.Equal(PlaybackMode.Embedded, host.ViewModel.PlaybackMode);

        // And back, for the one that ends the session rather than moving it.
        await EnterMiniModeAsync(host);
        await PressAsync(
            host,
            "MiniPlayerClose",
            () => host.ViewModel.Player is null,
            "clicking the mini player's close never let go of the session the shell was holding");
        Assert.Equal(PlaybackMode.Embedded, host.ViewModel.PlaybackMode);
    }

    /// <summary>
    /// Puts the session in the mini window and waits until the window is laid out.
    /// </summary>
    /// <remarks>
    /// The mode is changed through the shell's own view model rather than by pressing Mini player,
    /// which the shell batch already presses and records. What this scene is measuring starts once
    /// the second window is on screen, and a press with no layout behind it would only measure the
    /// harness waiting.
    /// </remarks>
    private static async Task EnterMiniModeAsync(ShellHost host)
    {
        await host.ViewModel.TogglePlaybackModeAsync(
            PlaybackMode.Mini,
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        await WaitForAsync(
            () => Task.FromResult(SecondaryWindows(host).Any(window => window.Bounds.Width > 0)),
            "the mini mode never put a laid-out window on screen for its controls to live in");
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
    /// The sixth batch: the library copied out and brought back, with the mouse — a stored copy, an
    /// exported archive, the dry run that says what it would do, and the restore itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These four are the reason the isolation rule reached the file pickers. Both buttons that ask
    /// for a path hand the question to a modal Windows dialog, and a dialog is the one thing no
    /// harness can answer: without the rule, both <c>ChooseArchive…Async</c> answer
    /// <see langword="null"/> for want of a main window, <see langword="null"/> means cancelled, and
    /// nothing happens that could be probed. A run with a data root of its own now exports into its
    /// handover folder and restores from what is in it.
    /// </para>
    /// <para>
    /// Which is also what chains the scene: nothing here composes a path. The export goes wherever
    /// the application says it goes, and the restore takes whatever the application finds there, so
    /// what comes back is what went out.
    /// </para>
    /// <para>
    /// Every probe reads the disk rather than the screen. A backup screen that says it made a copy
    /// and a backup screen that made one look identical from the view model, and this is the surface
    /// where the difference is the whole feature.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 180_000)]
    public async Task The_library_is_copied_out_and_brought_back_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var film = Path.Combine(media, "Arrival.2016.mp4");

        // Nothing decodes here: a copy walks the catalogue and the file only has to exist for the
        // library it describes to be a real one.
        await File.WriteAllBytesAsync(film, [0x41, 0x50], TestContext.Current.CancellationToken);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        await SeedMediaFileAsync(factory, media, film, TimeSpan.FromMinutes(116));

        // Where this run says it puts what it would have handed to Windows. The walk reads the
        // application's own answer instead of composing one, because the rule is the application's
        // to state — and asserting it is not null is what proves this scene is testing the isolated
        // exit rather than quietly passing against a dialog nobody could have answered.
        var paths = new AppDataPaths(_dataRoot);
        var handoff = paths.SystemHandoffDirectory;
        Assert.NotNull(handoff);

        // Counts rather than collections: a probe is compared by value, and a fresh array every
        // read would report "it changed" for the click that is meant to change nothing.
        //
        // Dot-prefixed folders are skipped because that is what the store itself calls temporary —
        // both its own staging and the restore's — so a copy means a copy somebody could restore.
        int StoredCopies() => Directory.Exists(paths.BackupsDirectory)
            ? Directory.GetDirectories(paths.BackupsDirectory)
                .Count(directory => !Path.GetFileName(directory).StartsWith('.'))
            : 0;
        int ExportedArchives() => Directory.Exists(handoff!)
            ? Directory.GetFiles(handoff!, "*.zip").Length
            : 0;
        int PreservedDatabases() => Directory.GetFiles(_dataRoot, "library.db.pre-restore-*.bak").Length;

        using var host = ShowShell(height: 1400);
        Navigate(host, AppRoute.Settings);
        await PressAsync(
            host,
            "NavigationBackups",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Copias in the settings index never opened its section");
        Assert.Equal(SettingsSection.Backups, host.ViewModel.CurrentSettingsSection);
        var backups = host.ViewModel.Backups;
        var restore = host.ViewModel.Restore;
        Assert.NotNull(backups);
        Assert.NotNull(restore);

        await PressAsync(
            host,
            "BackupCreateCopyLabel",
            StoredCopies,
            "clicking Create a backup now never left a copy in the backups folder");

        // The folder appears before the screen says so — the copy is published and only then does the
        // command's continuation run — so the outcome is waited for rather than read straight after
        // the effect. Measured here on the first run of this scene, and it is the same race the
        // privacy report scene met from the other side.
        await SettledAsync(backups!, "BackupStatusDone", "creating a copy");

        await PressAsync(
            host,
            "BackupExportLabel",
            ExportedArchives,
            "clicking Export to a ZIP file never wrote an archive where this run hands things over");
        await SettledAsync(backups, "BackupStatusDone", "exporting an archive");
        Assert.Equal(1, ExportedArchives());

        // The dry run says what the archive would do, and says it on screen: this is the one control
        // of the four whose effect is the plan itself rather than something on disk.
        await PressAsync(
            host,
            "RestoreChooseArchiveLabel",
            () => restore!.StatusKey,
            "clicking Choose an archive never produced a plan to look at");
        Assert.True(
            restore!.CanRestore,
            "The archive this application had just exported came back refused by its own dry run: "
                + $"{restore.StatusKey}, {string.Join("; ", restore.Findings.Select(f => f.MessageKey))}.");
        Assert.Equal(1, restore.MediaFileCount);

        // And the swap, read from the disk: the database that was replaced is kept beside the new
        // one, which is the only thing that tells a restore from a screen that says restored.
        await PressAsync(
            host,
            "RestoreConfirmLabel",
            PreservedDatabases,
            "clicking Restore now never put the archive's library in place of the live one");
        await WaitForAsync(
            () => Task.FromResult(restore.StatusKey == "RestoreStatusRestored"),
            $"The restore left the library in place but the wizard settled on {restore.StatusKey}.");
        Assert.NotNull(restore.PreservedDatabaseName);
    }

    /// <summary>
    /// Batch 2d, second half: switching to another version of what is playing, and what that asks
    /// when there is progress to carry across.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four controls and one of them is pressed three times, because the dialogue answers once and
    /// withdraws: Confirm, Start over and Cancel each need their own appearance, and what raises one
    /// is the row's own Switch. So the switch is pressed, the question is answered, and the pair
    /// repeats — which is also the only way a person meets all three.
    /// </para>
    /// <para>
    /// The two versions have deliberately different lengths. A switch only <b>asks</b> when the
    /// progress cannot be carried across without a judgement: with two files of the same length the
    /// application transfers the position and says nothing, which is correct and would leave three
    /// buttons unreachable.
    /// </para>
    /// <para>
    /// <b>Both</b> lengths have to be able to hold progress worth resuming, and that is what the
    /// lengths here are for. Measured on 2026-08-17, with a sixty-second cut and a twenty-second one:
    /// confirming the first switch landed the session at 8.9 s of the twenty, the next switch flushed
    /// that position and <c>ProgressPolicy.MinimumResumePosition</c> is thirty seconds, so the policy
    /// answered <c>Restart</c> and <b>the question was never raised again</b>. No position between 30 s
    /// and a twenty-second end exists, so the second and third answers were unreachable by arithmetic
    /// rather than by anything the harness was doing. Sixty and a hundred and eighty carry across as
    /// 40 s → 120 s, and every leg of the walk stays above the floor and below the end.
    /// </para>
    /// <para>
    /// That is also the whole of the "on screen, unnamed and disabled Button" the previous session
    /// recorded, and it was a symptom: with the question never arriving, the probe never changed, so
    /// <see cref="PressAsync{T}(ShellHost, string, Func{T}, string, string?, string?)"/> pressed again —
    /// and by then the row it had resolved had been replaced by the rebuilt session and was detached
    /// from the tree, where a <c>DynamicResource</c> name resolves to nothing and
    /// <c>IsEffectivelyEnabled</c> is false. Measured: <c>before detached=False en=True</c>,
    /// <c>after detached=True en=False name=&lt;null&gt;</c>, with a live replacement row beside it.
    /// </para>
    /// <para>
    /// So the order is Confirm, then Cancel, then Start over — decided by the same arithmetic and not
    /// by taste. Starting over lands the session at zero, which is below the resume floor, so nothing
    /// after it could raise the question a further time. Cancel keeps its point either way: it is
    /// pressed while a session is playing and another version is there to switch to, so the assertion
    /// that it changed nothing is made while there is something it could have changed.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_other_version_is_switched_to_with_the_mouse_and_its_question_answered()
    {
        var longer = await RequireSampleAsync("walk-version-long.mp4", durationSeconds: 180);
        var shorter = await RequireSampleAsync("walk-version-short.mp4", durationSeconds: 60);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var extended = Path.Combine(media, "Arrival.2016.Extended.mp4");
        var theatrical = Path.Combine(media, "Arrival.2016.Theatrical.mp4");
        File.Copy(longer, extended);
        File.Copy(shorter, theatrical);

        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var extendedFile = await SeedMediaFileAsync(factory, media, extended, TimeSpan.FromSeconds(180));
        var theatricalFile = await SeedMediaFileAsync(factory, media, theatrical, TimeSpan.FromSeconds(60));

        using var host = ShowShell(height: 1400);
        IServiceProvider services = host.Application.Services;
        var groups = services.GetRequiredService<IMediaVersionGroupRepository>();
        var watched = services.GetRequiredService<IWatchStateRepository>();
        var content = ContentKey.ForTitle(new TitleId(theatricalFile));

        async Task<long> PlayheadSecondsAsync()
        {
            var snapshot = await services.GetRequiredService<IMediaPlayerEngine>()
                .GetSnapshotAsync(TestContext.Current.CancellationToken);
            return (long)snapshot.Position.TotalSeconds;
        }

        // One title, two versions of very different lengths, which is what makes the switch ask.
        await groups.SaveAsync(
            new MediaVersionGroup(
                new MediaVersionId(Guid.NewGuid()),
                content.Value,
                [
                    Version(theatricalFile, theatrical, 60),
                    Version(extendedFile, extended, 180),
                ],
                PreferredMediaFileId: null),
            TestContext.Current.CancellationToken);

        // Forty seconds into the theatrical cut: past the resume floor, and far enough from its end
        // that the proportional carry across lands well past the floor on the other side too.
        await watched.SaveAsync(
            new WatchState
            {
                Content = content,
                Position = TimeSpan.FromSeconds(40),
                ObservedDuration = TimeSpan.FromSeconds(60),
                SourceMediaFileId = new MediaFileId(theatricalFile),
                Status = WatchStatus.InProgress,
                IsManualOverride = false,
                StartedUtc = DateTimeOffset.UnixEpoch,
                UpdatedUtc = DateTimeOffset.UnixEpoch,
            },
            TestContext.Current.CancellationToken);

        await OpenAndPlayAsync(host, theatricalFile);
        var versions = host.ViewModel.Player!.Versions;
        var dialog = host.ViewModel.Player!.VersionSwitch;
        Assert.NotNull(versions);
        Assert.NotNull(dialog);
        Assert.True(versions!.HasAlternatives, "The group's other version never reached the screen.");
        Assert.False(dialog!.IsVisible, "The question was on screen before anything had been switched.");

        // ---- Confirm: the question is raised by the switch, and answered by keeping the position.
        await OpenPlayerPanelAsync(host, PlayerPanel.Versions);
        await PressAsync(
            host,
            "PlayerVersionsSwitchAction",
            () => dialog.IsVisible,
            "clicking Switch never asked what to do with the progress it could not carry across");

        await PressAsync(
            host,
            "VersionSwitchConfirm",
            () => host.ViewModel.Player?.Player.MediaPath ?? string.Empty,
            "confirming the switch never moved the session onto the other version");

        await WaitForAsync(
            () => Task.FromResult(
                host.ViewModel.Player?.Player.MediaPath.EndsWith("Extended.mp4", StringComparison.Ordinal)
                    == true),
            "confirming the switch never opened the extended version");

        // The surfaces are read again, and that is not tidiness: confirming a switch opens the other
        // version, and opening a session builds a whole new set of surfaces. The dialogue this scene
        // held a moment ago belongs to a session that no longer exists, so a probe on it would watch
        // something nobody can see.
        var afterConfirm = host.ViewModel.Player!.VersionSwitch;
        Assert.NotNull(afterConfirm);
        Assert.NotSame(dialog, afterConfirm);
        Assert.Single(host.ViewModel.Player!.Versions!.Alternatives);

        // And the column is closed again, which is the same fact said about the panels: a new
        // session starts with the picture at full width, because the panel that was open belonged
        // to the file that just left. So it is opened again, the way a person would.
        Assert.Equal(PlayerPanel.None, host.ViewModel.PlayerPanel);
        await OpenPlayerPanelAsync(host, PlayerPanel.Versions);

        // The next switch flushes the playhead before it decides, so the session has to have reached
        // the point it was opened at first: the engine answers 0 until the demuxer applies the start
        // position, and a flushed zero is below the resume floor, which is the policy answering
        // "start again" instead of asking. Measured on the resume scene: 0, 40, 40, 40, 41.
        // Confirming means the position moves too, and nothing else in the suite watched that it did.
        // Measured before the fix: the playhead read 0, 0, 0, 0, 1, 1, 1, 2 on a switch that had
        // stored 00:02:01 — the transferred second was written and then reopened over.
        await WaitForAsync(
            async () => await PlayheadSecondsAsync() >= 110,
            "confirming the switch carried the progress across and then opened the other version "
                + "somewhere else entirely");

        // ---- Cancel: the same question, refused. Its effect is the state, because refusing has no
        // in-flight half to wait for -- and it is pressed while a session is playing and another
        // version is there to switch to, so "nothing changed" is a claim about something.
        await PressAsync(
            host,
            "PlayerVersionsSwitchAction",
            () => afterConfirm!.IsVisible,
            "switching back never asked again, so there was no question left to refuse");

        var playingBeforeCancel = host.ViewModel.Player!.Player.MediaPath;
        await PressAsync(
            host,
            "VersionSwitchCancel",
            () => afterConfirm!.Chosen?.ToString() ?? "nothing",
            "refusing the switch never registered as an answer");

        await SettleAsync();
        Assert.Equal(VersionSwitchChoice.Cancel, afterConfirm!.Chosen);
        Assert.False(afterConfirm.IsVisible, "The question stayed on screen after it was refused.");
        Assert.Same(afterConfirm, host.ViewModel.Player!.VersionSwitch);
        Assert.Equal(playingBeforeCancel, host.ViewModel.Player!.Player.MediaPath);

        // ---- Start over: the same question again, answered by opening the other version from zero.
        // It goes last because zero is below the resume floor, so no question survives it.
        await PressAsync(
            host,
            "PlayerVersionsSwitchAction",
            () => afterConfirm.IsVisible,
            "the refused switch left the row unable to raise the question a third time");

        await PressAsync(
            host,
            "VersionSwitchRestart",
            () => host.ViewModel.Player?.Player.MediaPath ?? string.Empty,
            "starting the other version over never moved the session onto it");

        await WaitForAsync(
            () => Task.FromResult(
                host.ViewModel.Player?.Player.MediaPath.EndsWith("Theatrical.mp4", StringComparison.Ordinal)
                    == true),
            "starting over never opened the theatrical version");

        // Zero is the whole of what Start over means, and it is read from the engine rather than from
        // the position events, which say nothing until the demuxer has applied a start position.
        await WaitForAsync(
            async () => await PlayheadSecondsAsync() < 10,
            "starting over opened the other version somewhere other than its beginning");

        var stored = await watched.GetAsync(
            ContentKey.ForTitle(new TitleId(extendedFile)),
            TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.Zero, stored?.Position);

        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Batch 2e: the two ways out of a session the engine could not open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two are offered for <b>different failures</b>, and that is measured rather than assumed.
    /// <c>PlaybackDiagnosticsPolicy.RecoveryActionsFor</c> gives corrupted media
    /// <c>ChooseAnotherVersion</c> and <c>OpenExternally</c> — no retry, because reopening bytes that
    /// are still the same bytes would fail the same way — and gives a missing file <c>Retry</c>,
    /// which is the disk somebody plugged back in. Measured on 2026-08-17 with two bytes behind an
    /// approved extension: <c>corrupted=True canRetry=False canOpenExternally=True</c>. So the scene
    /// opens twice, and each press meets the failure that offers it.
    /// </para>
    /// <para>
    /// Handing a file over is the <b>ninth exit</b> the isolation rule covers: the real launcher
    /// starts a process with <c>UseShellExecute</c>, which would open the system's player on the
    /// machine running this. The composition picked the recorder because this run keeps its data
    /// somewhere of its own, and the probe is the line it wrote — verb first, so it can be told from
    /// the other handovers without parsing anything.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task A_session_that_will_not_open_is_handed_over_and_retried_with_the_mouse()
    {
        var sample = await RequireSampleAsync("walk-recovery.mp4", durationSeconds: 8);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var broken = Path.Combine(media, "Arrival.2016.mp4");
        var absent = Path.Combine(media, "Dune.2021.mp4");

        // Two bytes behind an approved extension: a container the library catalogues and the decoder
        // cannot open, which is what puts the recovery actions on screen at all.
        await File.WriteAllBytesAsync(broken, [0x00, 0x00], TestContext.Current.CancellationToken);

        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var brokenFile = await SeedMediaFileAsync(factory, media, broken, TimeSpan.FromSeconds(8));

        // Catalogued and then taken away, which is the ordinary way a person meets Retry: the row is
        // there and the disk holding it is not.
        await File.WriteAllBytesAsync(absent, [0x00, 0x00], TestContext.Current.CancellationToken);
        var absentFile = await SeedMediaFileAsync(factory, media, absent, TimeSpan.FromSeconds(8));
        File.Delete(absent);


        // Asserting this is not null is what proves the scene presses the isolated exit rather than
        // passing quietly against one that would have opened the system's player on whoever ran it.
        var handoff = new AppDataPaths(_dataRoot).SystemHandoffDirectory;
        Assert.NotNull(handoff);
        var record = Path.Combine(handoff!, RecordingSystemHandoff.FileName);

        using var host = ShowShell(height: 1400);
        IServiceProvider services = host.Application.Services;
        // A group behind the broken file, because that is what turns the third recovery action into
        // a button at all: corrupted media offers "choose another version" only when another version
        // exists to choose. The broken file is pinned as the group's preferred member on purpose:
        // a group changes which version a session opens, and on a busy hosted runner an unpinned
        // group let the open drift to the other member — measured 2026-08-23 as "stopped=True"
        // where corrupted should stand. The alternative is the absent file, described by hand
        // because the helper weighs files on disk and this one's whole part is not being there.
        await services.GetRequiredService<IMediaVersionGroupRepository>().SaveAsync(
            new MediaVersionGroup(
                new MediaVersionId(Guid.NewGuid()),
                $"title:{Guid.NewGuid():D}",
                [
                    Version(brokenFile, broken, 8),
                    new MediaVersion(
                        new MediaFileId(absentFile),
                        absent,
                        IsAvailable: false,
                        TimeSpan.FromSeconds(8),
                        Width: 320,
                        Height: 240,
                        IsHdr: false,
                        VideoCodec: "H264",
                        SizeBytes: 2),
                ],
                PreferredMediaFileId: new MediaFileId(brokenFile)),
            TestContext.Current.CancellationToken);

        // ---- Open with an external application: the file that cannot be decoded.
        await host.ViewModel.OpenPlayerAsync(
            new PlayDetailsRequest(new MediaFileId(brokenFile), StartPosition: null),
            TestContext.Current.CancellationToken);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.HasFailed == true),
            () => WhyItHasNotFailedYet(
                host.ViewModel.Player,
                "the two-byte file opened, so there was no failure to recover from"));
        Assert.True(
            host.ViewModel.Player!.Player.MediaWasCorrupted,
            "the two-byte file failed as something other than corrupted media, so the actions on "
                + "screen are not the ones this scene was written for.");

        await PressAsync(
            host,
            "PlayerRecoveryOpenExternally",
            () => ReadRecord(record),
            "handing the file to the system's own player wrote nothing down");

        await SettleAsync();
        Assert.Contains(
            $"{RecordingSystemHandoff.PlayExternallyVerb} {broken}",
            RecordedLines(record));

        // The third way out, a button since 2026-08-23: its flyout holds the same rows the side
        // column lists, so its effect is that it opens — the switch a row performs is measured by
        // the version-switch scene on those very rows. Closed by its own door; see the speed menu.
        await PressAsync(
            host,
            "PlayerRecoveryChooseAnotherVersion",
            () => Resolve(host, "PlayerRecoveryChooseAnotherVersion") is Button { Flyout.IsOpen: true },
            "clicking Choose another version never opened the list of versions");
        ((Button)Resolve(host, "PlayerRecoveryChooseAnotherVersion")).Flyout!.Hide();
        Dispatcher.UIThread.RunJobs();

        // ---- Retry: the file that was not there, put back between the two presses.
        await host.ViewModel.OpenPlayerAsync(
            new PlayDetailsRequest(new MediaFileId(absentFile), StartPosition: null),
            TestContext.Current.CancellationToken);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.FileWasNotFound == true),
            "a file that is not on disk opened anyway, so there was nothing to retry");

        // What makes the retry mean something: the second attempt has something to find. Without
        // this the press would be honest and the session would fail again, which proves the press
        // reached the button and nothing about what the button is for.
        File.Copy(sample, absent, overwrite: true);
        await PressAsync(
            host,
            "PlayerRecoveryRetry",
            () => host.ViewModel.Player?.Player.IsPlaying == true ? "playing" : "not playing",
            "retrying never opened the file that had come back");

        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the retried session never reached the playing state on the real engine");

        // And the scene ends here, with a video still playing, on purpose: closing the player first
        // is what every other scene does and what hid this. Measured on 2026-08-17, twice, the
        // moment a scene stopped short of the close — "ObjectDisposedException:
        // LibVlcMediaPlayerEngine" from the coordinator's own disposal, because ending the session's
        // hooks is not the same as stopping the session. Somebody who closes the window mid-film
        // takes this path, so the teardown has to survive it.
    }

    /// <summary>
    /// Batch 2e: the banner shown while something from outside the library is playing.
    /// </summary>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task A_file_from_outside_the_library_offers_its_folder_with_the_mouse()
    {
        var sample = await RequireSampleAsync("walk-loose.mp4", durationSeconds: 12);
        var media = Path.Combine(_dataRoot, "media");
        var outside = Path.Combine(_dataRoot, "outside");
        Directory.CreateDirectory(media);
        Directory.CreateDirectory(outside);
        var loose = Path.Combine(outside, "Arrival.2016.mp4");
        File.Copy(sample, loose);

        // A library that exists and does not contain this file, which is the whole situation the
        // banner is about.
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);

        using var host = ShowShell(height: 1400, activationPath: loose);
        host.Application.ConfigureWindow(host.Window);

        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.LooseFile?.IsLooseSession == true),
            "the activation never put a loose session on the screen");
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the loose session never reached the playing state on the real engine");

        var banner = host.ViewModel.Player!.LooseFile!;
        Assert.Equal("Arrival.2016.mp4", banner.DisplayName);
        Assert.Equal(outside, banner.FolderPath);

        Dispatcher.UIThread.RunJobs();

        // The banner is a card, not a sheet over the picture. Measured before the correction at
        // 1280x1400 over a 1280x1400 stage — and it carries a background, so it also swallowed every
        // click meant for the video behind it. Asserted rather than eyeballed, because this is the
        // third overlay to do it and the previous two were found the same way.
        var stage = host.Shell.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "PlayerStage");
        var surface = host.Shell.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "LooseFileSurface");
        Assert.True(
            surface.Bounds.Width < stage.Bounds.Width && surface.Bounds.Height < stage.Bounds.Height,
            $"The loose-file banner is {surface.Bounds.Size} over a {stage.Bounds.Size} stage, so it "
                + "is drawn as a sheet over the whole player rather than as the card it is.");

        // ---- Ask, refuse, ask again, agree. Cancel goes in the middle because confirming adds the
        // root and there is nothing left to refuse afterwards.
        await PressAsync(
            host,
            "LooseFileAddFolderAction",
            () => banner.IsAddFolderConfirmationPending,
            "adding the containing folder never asked first, and adding a root is not a shrug");

        await PressAsync(
            host,
            "LooseFileCancelAction",
            () => banner.IsAddFolderConfirmationPending,
            "refusing the confirmation never withdrew it");

        await PressAsync(
            host,
            "LooseFileAddFolderAction",
            () => banner.IsAddFolderConfirmationPending,
            "the refused confirmation left the banner unable to ask again");

        await PressAsync(
            host,
            "LooseFileConfirmAction",
            async () => await CountAsync(factory, "library_roots"),
            "confirming never added the folder the loose file lives in");

        await WaitForAsync(
            async () => await CountAsync(factory, "library_roots") == 2,
            "the confirmed folder never became a second root");
    }

    /// <summary>
    /// Batch 1: the film card's two remaining actions — returning to where you were, and the trailer
    /// that sits beside the film on disk.
    /// </summary>
    [AvaloniaFact(Timeout = 180_000)]
    public async Task The_film_card_resumes_restarts_and_plays_the_trailer_beside_it_with_the_mouse()
    {
        var feature = await RequireSampleAsync("walk-card.mp4", durationSeconds: 90);
        var short_ = await RequireSampleAsync("walk-card-trailer.mp4", durationSeconds: 8);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var film = Path.Combine(media, "Arrival.2016.mp4");

        // The sibling convention TrailerDiscoveryPolicy looks for: the film's own name plus the
        // suffix. No catalogue row and no version group — the card finds it by name on disk.
        var trailer = Path.Combine(media, "Arrival.2016-trailer.mp4");
        File.Copy(feature, film);
        File.Copy(short_, trailer);

        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var fileId = await SeedMediaFileAsync(factory, media, film, TimeSpan.FromSeconds(90));

        // The provider's trailer link too, so the row is the longest it can be when it is measured.
        // That link is pressed elsewhere; here it is width.
        await SeedTrailerKeyAsync(factory, new TitleId(fileId), "dQw4w9WgXcQ");

        using var host = ShowShell(height: 1200);
        IServiceProvider services = host.Application.Services;

        // Forty seconds into ninety: above the thirty-second resume floor and well before the end,
        // which is what makes the card offer to return at all.
        await services.GetRequiredService<IWatchStateRepository>().SaveAsync(
            new WatchState
            {
                Content = ContentKey.ForTitle(new TitleId(fileId)),
                Position = TimeSpan.FromSeconds(40),
                ObservedDuration = TimeSpan.FromSeconds(90),
                SourceMediaFileId = new MediaFileId(fileId),
                Status = WatchStatus.InProgress,
                IsManualOverride = false,
                StartedUtc = DateTimeOffset.UnixEpoch,
                UpdatedUtc = DateTimeOffset.UnixEpoch,
            },
            TestContext.Current.CancellationToken);

        async Task<long> PlayheadSecondsAsync()
        {
            var snapshot = await services.GetRequiredService<IMediaPlayerEngine>()
                .GetSnapshotAsync(TestContext.Current.CancellationToken);
            return (long)snapshot.Position.TotalSeconds;
        }

        async Task OpenCardAsync()
        {
            Navigate(host, AppRoute.Library);
            var library = host.ViewModel.Library;
            Assert.NotNull(library);
            await library!.LoadAsync(TestContext.Current.CancellationToken);
            await library.OpenDetailsAsync(
                Assert.Single(library.Items),
                TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
        }

        await OpenCardAsync();
        var card = host.ViewModel.Library!.MovieDetails;
        Assert.True(card.CanResume, "The card offered no way back, so there is nothing to press.");
        Assert.True(card.HasTrailer, "The card never found the trailer sitting beside the film.");

        // This row is the first to show Resume and the trailer at once, so it is also the longest it
        // has ever been — and it is a horizontal StackPanel with a free-width label between buttons,
        // which is the shape that has put a control outside the window six times. Measured here
        // rather than discovered as a red sixty seconds later.
        foreach (var anchor in new[]
        {
            card.PlayActionText,
            "MoviePlayAction",
            "MovieTrailerAction",
            "DetailsTrailerLinkAction",
        })
        {
            var control = Resolve(host, anchor);
            var corner = control.TranslatePoint(new Point(control.Bounds.Width, 0), host.Window);
            Assert.True(
                corner is { } point && point.X <= host.Window.Bounds.Width,
                $"{anchor} ends at x={corner?.X:F0} in a {host.Window.Bounds.Width:F0} px window, so "
                    + "the film card's action row draws outside it.");
        }

        // ---- Resume: back to the second that was stored, not to the beginning. The button says
        // «Continuar · 40:00» here, which is its accessible name too: one control whose words follow
        // what there is to open, so the anchor is read off the card rather than from a resource.
        await PressAsync(
            host,
            card.PlayActionText,
            () => host.ViewModel.Player?.Player.MediaPath ?? string.Empty,
            "returning to where you were never opened the film",
            recordAs: "{Binding PlayActionText}");
        await WaitForAsync(
            async () => await PlayheadSecondsAsync() >= 35,
            "the film opened, but not at the point the card offered to return to");

        // The same silence the hero's Continue earns: a card that named the minute has answered the
        // question the offer asks, and asking it again over a picture already playing is the offer
        // arguing with the button that opened it.
        Assert.Null(host.ViewModel.Player!.Resume);

        // ---- And the finding that came out of the version switch: with progress stored, starting
        // from the beginning has to actually start at the beginning. Until the requested position
        // began to win, the host recomputed it from storage and this would have resumed instead.
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
        await OpenCardAsync();
        Assert.True(card.CanResume, "The stored progress was lost, so 'from the start' proves nothing.");

        await PressAsync(
            host,
            "MoviePlayAction",
            () => host.ViewModel.Player?.Player.MediaPath ?? string.Empty,
            "playing from the start never opened the film");
        await SettleAsync();
        await SettleAsync();
        Assert.True(
            await PlayheadSecondsAsync() < 10,
            $"'Play from the start' opened at {await PlayheadSecondsAsync()} s with progress stored, "
                + "so it resumed instead of starting again.");

        // ---- The trailer: a file beside the film, opened as the loose session it is.
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
        await OpenCardAsync();

        await PressAsync(
            host,
            "MovieTrailerAction",
            () => host.ViewModel.Player?.LooseFile?.IsLooseSession == true ? "loose" : "none",
            "the trailer beside the film never opened");
        await WaitForAsync(
            () => Task.FromResult(
                host.ViewModel.Player?.Player.MediaPath.EndsWith("-trailer.mp4", StringComparison.Ordinal)
                    == true),
            "something opened, but it was not the trailer");

        // A trailer is not a catalogue row, which is the whole of LIB-014.
        Assert.Equal(1, await CountAsync(factory, "media_files"));
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Batch 1, the last one: the row a season lists, which is how a person starts an episode.
    /// </summary>
    /// <remarks>
    /// The row is told apart by its help text and not by its name, because every episode's button
    /// answers to the same accessible name by design — the season and episode label is what a screen
    /// reader reads out after it, and it is what a person uses to tell two rows apart as well.
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task An_episode_is_started_from_its_row_with_the_mouse()
    {
        var sample = await RequireSampleAsync("walk-episode.mp4", durationSeconds: 12);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var first = Path.Combine(media, "Chained.S01E01.mp4");
        var second = Path.Combine(media, "Chained.S01E02.mp4");
        var third = Path.Combine(media, "Chained.S02E01.mp4");
        File.Copy(sample, first);
        File.Copy(sample, second);
        File.Copy(sample, third);

        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var firstFile = await SeedMediaFileAsync(factory, media, first, TimeSpan.FromSeconds(12));
        var secondFile = await SeedMediaFileAsync(factory, media, second, TimeSpan.FromSeconds(12));
        var thirdFile = await SeedMediaFileAsync(factory, media, third, TimeSpan.FromSeconds(12));

        // Two seasons, because the picker is absent with one: a scene that pressed it against a
        // single-season series would be pressing a control the application deliberately does not draw.
        _ = await SeedSeriesAsync(factory, firstFile, secondFile, thirdFile);

        using var host = ShowShell(height: 1200);
        Navigate(host, AppRoute.Library);
        var library = host.ViewModel.Library;
        Assert.NotNull(library);
        await library!.LoadAsync(TestContext.Current.CancellationToken);

        var show = library.Items.FirstOrDefault(item => item.Item.Kind == CatalogTitleKind.Show);
        Assert.True(show is not null, "The seeded series never reached the library.");
        await library.OpenDetailsAsync(show!, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        // The seasons are pills since 2026-08-25, not a drop-down, and a pill's effect is on this
        // window rather than in a popup root of its own: what it changes is which season the card
        // lists. Pressed both ways on purpose — a chooser that has only ever been pushed forwards is
        // a chooser nobody has shown can be gone back through.
        var seasons = library.ShowDetails.Seasons;
        Assert.True(seasons.Count > 1, "The seeded series never produced a second season to choose.");
        await PressAsync(
            host,
            seasons[1].SeasonLabel,
            () => library.ShowDetails.SelectedSeason?.SeasonNumber ?? 0,
            "clicking the second season's pill never put that season on the card",
            recordAs: "{Binding SeasonLabel}");
        Assert.Equal(2, library.ShowDetails.SelectedSeason?.SeasonNumber);

        await PressAsync(
            host,
            seasons[0].SeasonLabel,
            () => library.ShowDetails.SelectedSeason?.SeasonNumber ?? 0,
            "clicking the first season's pill never brought that season back",
            recordAs: "{Binding SeasonLabel}");

        // The card opens on the first season, so the episode below is still the one this scene names.
        Assert.Equal(1, library.ShowDetails.SelectedSeason?.SeasonNumber);

        // And the banner's own button, which is the point of a series card: the episode it names is
        // the one the series is waiting on, and pressing it opens that episode rather than the first.
        Assert.True(
            library.ShowDetails.HasNextEpisode,
            "The series card offered no next episode, so its Continue could not be pressed.");
        var expected = library.ShowDetails.NextEpisode!.MediaFileId;
        await PressAsync(
            host,
            "HomeResumeAction",
            () => host.ViewModel.Player is not null,
            "clicking Continue on the series card never opened the episode it names",
            helpText: library.ShowDetails.NextEpisodeLabel);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the series card's Continue opened a session that never reached the playing state");
        Assert.NotNull(expected);
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        // The second episode, not the first: a season lists them in order, and pressing the one at
        // the top would be indistinguishable from a card that plays whatever it feels like.
        await PressAsync(
            host,
            "EpisodePlayAction",
            () => host.ViewModel.Player?.Player.MediaPath ?? string.Empty,
            "the episode row never started the episode it names",
            helpText: "S01E02");

        await WaitForAsync(
            () => Task.FromResult(
                host.ViewModel.Player?.Player.MediaPath.EndsWith("S01E02.mp4", StringComparison.Ordinal)
                    == true),
            "a session opened, but not the episode whose row was pressed");
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>One version of a title, described the way the catalogue describes one.</summary>
    private static MediaVersion Version(Guid fileId, string path, int durationSeconds) =>
        new(
            new MediaFileId(fileId),
            path,
            IsAvailable: true,
            TimeSpan.FromSeconds(durationSeconds),
            Width: 320,
            Height: 240,
            IsHdr: false,
            VideoCodec: "H264",
            SizeBytes: new FileInfo(path).Length);

    /// <summary>
    /// Batch 2d, first half: the offer made before a session starts, and the one made when it ends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both surfaces answer <b>once</b> and hide, so four controls need four sessions: a prompt that
    /// has been answered is not a prompt, and pressing its other button afterwards would be pressing
    /// something nobody can reach. That is why this scene opens the player four times rather than
    /// arranging the presses inside one.
    /// </para>
    /// <para>
    /// Resume is pressed from somewhere else on the timeline on purpose. The session already opens at
    /// the stored point — that is what the decision did — so pressing Resume where the playhead
    /// already is would ask for the position it already has, which is the rule that cost a
    /// measurement on the volume slider. The playhead is moved first, and then Resume brings it back.
    /// </para>
    /// <para>
    /// The playhead is read from the engine and the session is paused first, both for the reasons the
    /// marker scene measured: a probe fed by position events cannot see a seek while the engine is
    /// stopped, and a playing session moves the very thing the press is meant to move.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_resume_offer_and_the_next_episode_offer_are_answered_with_the_mouse()
    {
        var feature = await RequireSampleAsync("walk-resume.mp4", durationSeconds: 90);
        var episode = await RequireSampleAsync("walk-chain.mp4", durationSeconds: 3);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var film = Path.Combine(media, "Arrival.2016.mp4");
        var first = Path.Combine(media, "Chained.S01E01.mp4");
        var second = Path.Combine(media, "Chained.S01E02.mp4");
        File.Copy(feature, film);
        File.Copy(episode, first);
        File.Copy(episode, second);

        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var filmFile = await SeedMediaFileAsync(factory, media, film, TimeSpan.FromSeconds(90));
        var firstFile = await SeedMediaFileAsync(factory, media, first, TimeSpan.FromSeconds(3));
        var secondFile = await SeedMediaFileAsync(factory, media, second, TimeSpan.FromSeconds(3));
        _ = await SeedSeriesAsync(factory, firstFile, secondFile);

        using var host = ShowShell(height: 1400);
        // Typed rather than inferred, because a local function reads a captured variable at the
        // nullable state it had where the function was written, not where it is called.
        IServiceProvider services = host.Application.Services;
        var watched = services.GetRequiredService<IWatchStateRepository>();
        var content = ContentKey.ForTitle(new TitleId(filmFile));

        async Task<long> PlayheadSecondsAsync()
        {
            var snapshot = await services.GetRequiredService<IMediaPlayerEngine>()
                .GetSnapshotAsync(TestContext.Current.CancellationToken);
            return (long)snapshot.Position.TotalSeconds;
        }

        // Forty seconds into a ninety-second film, which is what a person left behind.
        async Task SeedProgressAsync() => await watched.SaveAsync(
            new WatchState
            {
                Content = content,
                Position = TimeSpan.FromSeconds(40),
                ObservedDuration = TimeSpan.FromSeconds(90),
                SourceMediaFileId = new MediaFileId(filmFile),
                Status = WatchStatus.InProgress,
                IsManualOverride = false,
                StartedUtc = DateTimeOffset.UnixEpoch,
                UpdatedUtc = DateTimeOffset.UnixEpoch,
            },
            TestContext.Current.CancellationToken);

        // ---- First session: the offer is refused, and the film starts over.
        await SeedProgressAsync();
        await OpenAndPlayAsync(host, filmFile);

        // The offer is raised after the session opens, so it is waited for rather than read — the
        // same lesson the playhead comment below spells out, which this line did not apply. On an
        // idle machine it is already visible when the open returns; under the load of the other 116
        // scenes it is not, and CI measured exactly that on 2026-08-18: pass 1 failed here, pass 2
        // passed all 117.
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Resume?.IsVisible == true),
            "The stored progress never raised the resume offer.");
        var resume = host.ViewModel.Player!.Resume;
        Assert.NotNull(resume);
        Assert.Equal("00:00:40", resume!.ResumePositionText);

        // The session opens where it was left, and it is waited for rather than read: the engine
        // answers 0 until the demuxer has applied the start position, measured here as
        // "0, 40, 40, 40, 40, 41, 41, 41" over two seconds. Nothing else in this repository checks
        // that end to end -- the wiring test asserts the request carries the position, against a
        // coordinator that never opens anything -- and reading too early is what made a working
        // Start over look like a press that did nothing.
        await WaitForAsync(
            async () => await PlayheadSecondsAsync() >= 39,
            "the session with stored progress never opened at the point it was left");

        await PauseWithTheSpaceBarAsync(host);

        await PressAsync(
            host,
            "ResumePromptRestart",
            PlayheadSecondsAsync,
            "clicking Start over never took the playhead back to the beginning");

        Assert.True(
            await PlayheadSecondsAsync() < 5,
            $"Start over left the playhead at {await PlayheadSecondsAsync()} s.");
        Assert.False(resume.IsVisible, "The offer stayed on screen after it had been answered.");
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);

        // ---- Second session: the offer is taken, from somewhere else on the timeline.
        await SeedProgressAsync();
        await OpenAndPlayAsync(host, filmFile);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Resume?.IsVisible == true),
            "The stored progress never raised the offer a second time.");
        var secondResume = host.ViewModel.Player!.Resume;
        Assert.NotNull(secondResume);
        await WaitForAsync(
            async () => await PlayheadSecondsAsync() >= 39,
            "the session with stored progress never opened at the point it was left, a second time");
        await PauseWithTheSpaceBarAsync(host);
        await services.GetRequiredService<ControlPlayback>()
            .SeekAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await WaitForAsync(
            async () => await PlayheadSecondsAsync() < 10,
            "the playhead never moved away from the stored point, so Resume would have had nothing to do");

        await PressAsync(
            host,
            "ResumePromptResume",
            PlayheadSecondsAsync,
            "clicking Resume never took the playhead back to the stored point");

        Assert.True(
            await PlayheadSecondsAsync() >= 39,
            $"Resume left the playhead at {await PlayheadSecondsAsync()} s rather than at the stored 40.");
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);

        // ---- Third session: an episode ends and the offer is refused.
        await OpenAndPlayAsync(host, firstFile);
        var overlay = host.ViewModel.Player!.NextEpisode;
        Assert.NotNull(overlay);
        await WaitForAsync(
            () => Task.FromResult(overlay!.IsVisible),
            "the end of the episode never raised the next-episode offer");

        await PressAsync(
            host,
            "NextEpisodeCancel",
            () => overlay!.IsVisible,
            "clicking Cancel never ended the offer that was counting down");

        // And refusing means refusing: the session is still the episode that ended, not the next one.
        await SettleAsync();
        Assert.EndsWith(
            "Chained.S01E01.mp4",
            host.ViewModel.Player?.Player.MediaPath ?? string.Empty,
            StringComparison.Ordinal);
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);

        // ---- Fourth session: the same end, accepted, which chains onto the next episode.
        await OpenAndPlayAsync(host, firstFile);
        var chained = host.ViewModel.Player!.NextEpisode;
        Assert.NotNull(chained);
        await WaitForAsync(
            () => Task.FromResult(chained!.IsVisible),
            "the end of the episode never raised the offer a second time");
        Assert.Equal("T1 E2", chained!.EpisodeLabel);

        await PressAsync(
            host,
            "NextEpisodePlayNow",
            () => host.ViewModel.Player?.Player.MediaPath ?? string.Empty,
            "clicking Play now never chained the session onto the next episode");

        await WaitForAsync(
            () => Task.FromResult(
                host.ViewModel.Player?.Player.MediaPath.EndsWith("Chained.S01E02.mp4", StringComparison.Ordinal)
                    == true),
            "accepting the offer never opened the second episode");

        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Pauses through the keyboard chain the transport scene walks: view → shared map → router →
    /// coordinator → engine.
    /// </summary>
    private static async Task PauseWithTheSpaceBarAsync(ShellHost host)
    {
        var view = host.Shell.GetVisualDescendants().OfType<PlayerView>().First();
        Assert.True(view.Focus(), "The player surface refused the focus.");
        host.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPaused == true),
            "the space bar never paused the playing session");
    }

    /// <summary>Opens one file and waits until the real engine is decoding it.</summary>
    private static async Task OpenAndPlayAsync(ShellHost host, Guid fileId)
    {
        await host.ViewModel.OpenPlayerAsync(
            new PlayDetailsRequest(new MediaFileId(fileId), StartPosition: null),
            TestContext.Current.CancellationToken);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the session never reached the playing state on the real engine");
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Batch 2c: the ranges of an episode — made by hand, offered as a skip, and decided when a
    /// detector proposed them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Seven controls over three surfaces that only exist during a session, so this scene plays real
    /// video from beginning to end of its own work. The detections are seeded <b>before</b> the
    /// player opens, because the review surface loads them while the session is being built: seeding
    /// them afterwards would fill a database nobody was going to read again.
    /// </para>
    /// <para>
    /// The skip offer is recomputed on the engine's position events and nowhere else, so the marker
    /// is saved while the session <b>plays</b> — that is the only thing that makes the offer appear,
    /// and it is how a person meets it. The session is paused only afterwards, because the probe for
    /// the skip is the playhead, and a playing session moves the playhead by itself: the click beside
    /// the control would "change" the very thing the press is meant to change.
    /// </para>
    /// <para>
    /// Every probe is the row in the database rather than the list on screen. A surface that removed
    /// a row from its own collection and stored nothing looks identical from the collection.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_markers_of_an_episode_are_made_skipped_and_decided_with_the_mouse()
    {
        var sample = await RequireSampleAsync("walk-markers.mp4", durationSeconds: 90);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var first = Path.Combine(media, "Marked.S01E01.mp4");
        var second = Path.Combine(media, "Marked.S01E02.mp4");
        File.Copy(sample, first);
        File.Copy(sample, second);

        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var firstFile = await SeedMediaFileAsync(factory, media, first, TimeSpan.FromSeconds(90));
        var secondFile = await SeedMediaFileAsync(factory, media, second, TimeSpan.FromSeconds(90));
        var showId = await SeedSeriesAsync(factory, firstFile, secondFile);

        using var host = ShowShell(height: 2000);
        var services = host.Application.Services;
        var detected = services.GetRequiredService<IDetectedMarkerRepository>();
        var manual = services.GetRequiredService<IIntroMarkerRepository>();
        var series = new SeriesId(showId);

        // Three proposals, one per decision, far enough from each other and from the manual range
        // that no press can be explained by an overlap.
        var proposals = new[]
        {
            SeedDetection(series, firstFile, MarkerKind.Recap, 50, 56),
            SeedDetection(series, firstFile, MarkerKind.Credits, 62, 70),
            SeedDetection(series, firstFile, MarkerKind.Intro, 76, 84),
        };
        foreach (var proposal in proposals)
        {
            await detected.SaveAsync(proposal, TestContext.Current.CancellationToken);
        }

        await host.ViewModel.OpenPlayerAsync(
            new PlayDetailsRequest(new MediaFileId(firstFile), StartPosition: null),
            TestContext.Current.CancellationToken);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the session never reached the playing state on the real engine");
        Dispatcher.UIThread.RunJobs();

        var surfaces = host.ViewModel.Player!;
        var editor = surfaces.Markers;
        var review = surfaces.DetectedReview;
        var skip = surfaces.Skip;
        Assert.NotNull(editor);
        Assert.NotNull(review);
        Assert.NotNull(skip);
        Assert.Equal(3, review!.Detections.Count);

        // What the series holds by hand, as a sentence: a probe is compared by value, and a fresh
        // list every read would report "it changed" for the click that must change nothing.
        async Task<string> ManualRangesAsync() => string.Join(
            ";",
            (await manual.GetForSeriesAsync(series, TestContext.Current.CancellationToken))
                .Select(marker => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{marker.Kind}:{marker.Start.TotalSeconds}-{marker.End.TotalSeconds}")));

        Assert.Equal(string.Empty, await ManualRangesAsync());

        await OpenPlayerPanelAsync(host, PlayerPanel.Markers);
        await PressAsync(
            host,
            "MarkerEditorKindLabel",
            () => Resolve(host, "MarkerEditorKindLabel") is ComboBox { IsDropDownOpen: true },
            "clicking the marker kind never opened the list of kinds");
        CloseDropDown(host);

        // The range is typed the way the two spinners take it — they are not command controls and
        // the walk does not count them — and the press that matters is Save.
        editor!.SelectedKind = MarkerKind.Intro;
        editor.StartSeconds = 0;
        editor.EndSeconds = 40;
        Dispatcher.UIThread.RunJobs();

        await PressAsync(
            host,
            "MarkerEditorSave",
            ManualRangesAsync,
            "clicking Save never wrote the range into the series");

        Assert.Equal("Intro:0-40", await ManualRangesAsync());

        // And it reaches the session that is running: the offer is recomputed from the engine's own
        // position events, so this is the application noticing the playhead is now inside a range.
        await WaitForAsync(
            () => Task.FromResult(skip!.IsVisible),
            "the range saved mid-session never surfaced the skip offer on the playhead");

        // Paused before the skip is pressed, through the same keyboard chain the transport scene
        // walks. A playing session moves the playhead on its own, and the playhead is the probe.
        var playerView = host.Shell.GetVisualDescendants().OfType<PlayerView>().First();
        Assert.True(playerView.Focus(), "The player surface refused the focus.");
        host.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPaused == true),
            "the space bar never paused the playing session");

        // The engine is asked where the playhead is, not the transport bar. The bar's position is fed
        // by the engine's position events, and a paused session raises none — so a seek that really
        // happened would be invisible in the surface's own copy. Measured here: the press landed on
        // the button (the impact chain named it) and the bar never moved. Whole seconds, because a
        // probe is compared by value and the ticks of a paused engine are nobody's business.
        var engine = services.GetRequiredService<IMediaPlayerEngine>();
        async Task<long> PlayheadSecondsAsync() =>
            (long)(await engine.GetSnapshotAsync(TestContext.Current.CancellationToken))
                .Position.TotalSeconds;

        await PressAsync(
            host,
            "SkipMarkerAccessibleName",
            PlayheadSecondsAsync,
            "clicking Skip never moved the playhead out of the range it was inside");

        Assert.True(
            await PlayheadSecondsAsync() >= 39,
            $"Skip left the playhead at {await PlayheadSecondsAsync()} s, still inside the range it "
                + "offered to skip.");

        // Deleting needs a row selected, and the list that selects it is not a command control.
        editor.SelectedMarker = Assert.Single(editor.Markers);
        Dispatcher.UIThread.RunJobs();

        await PressAsync(
            host,
            "MarkerEditorDelete",
            ManualRangesAsync,
            "clicking Delete never took the range out of the series");

        Assert.Equal(string.Empty, await ManualRangesAsync());

        // And the three decisions on what a detector proposed. Each is read back from its row: a
        // surface that changed its own copy and stored nothing looks identical on screen.
        review.Selected = review.Detections.Single(row => row.Id == proposals[0].Id);
        Dispatcher.UIThread.RunJobs();
        await OpenPlayerPanelAsync(host, PlayerPanel.Markers);
        await PressAsync(
            host,
            "DetectedMarkerReviewAccept",
            async () => (await detected.GetAsync(proposals[0].Id, TestContext.Current.CancellationToken))
                ?.UserCorrected == true,
            "clicking Accept never confirmed the proposal against the next detection run");

        review.Selected = review.Detections.Single(row => row.Id == proposals[1].Id);
        review.StartSeconds = 60;
        review.EndSeconds = 68;
        Dispatcher.UIThread.RunJobs();
        await PressAsync(
            host,
            "DetectedMarkerReviewCorrect",
            async () => (await detected.GetAsync(proposals[1].Id, TestContext.Current.CancellationToken))
                ?.End.TotalSeconds ?? 0,
            "clicking Correct never moved the proposal to the range that was typed");

        review.Selected = review.Detections.Single(row => row.Id == proposals[2].Id);
        Dispatcher.UIThread.RunJobs();
        await PressAsync(
            host,
            "DetectedMarkerReviewDelete",
            async () => (await detected.GetForFileAsync(
                new MediaFileId(firstFile),
                TestContext.Current.CancellationToken)).Count,
            "clicking Delete never removed the proposal from the episode");

        // The accepted one survived, the corrected one carries the typed range, and the deleted one
        // is gone: three decisions, three rows, read from the database.
        var remaining = await detected.GetForFileAsync(
            new MediaFileId(firstFile),
            TestContext.Current.CancellationToken);
        Assert.Equal(2, remaining.Count);
        Assert.True(remaining.Single(row => row.Id == proposals[0].Id).UserCorrected);
        Assert.Equal(TimeSpan.FromSeconds(68), remaining.Single(row => row.Id == proposals[1].Id).End);

        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>One proposal as a detector would have left it: never corrected, and confident.</summary>
    private static DetectedMarker SeedDetection(
        SeriesId series,
        Guid fileId,
        MarkerKind kind,
        int startSeconds,
        int endSeconds) =>
        new(
            Guid.NewGuid(),
            series,
            new MediaFileId(fileId),
            kind,
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(endSeconds),
            Confidence: 0.9,
            DetectorVersion: 1,
            UserCorrected: false);

    /// <summary>
    /// Batch 2b: how subtitles look, chosen in settings and kept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This surface lives in settings rather than over the picture, so it needs no video at all. Three
    /// sliders and a drop-down, and each is asked for a value it is not already at — a range control
    /// pressed at its centre usually asks for what it already has.
    /// </para>
    /// <para>
    /// The probe is <b>the stored preference</b>, not the view model. That is the rule this surface
    /// was breaking: nothing in the application saved this style, loaded it, or read it, so the whole
    /// effect of four controls was a field of one object that died with the window. Asserting on that
    /// field would have proved the field keeps what was put in it.
    /// </para>
    /// <para>
    /// What is still missing is written down rather than implied: the style reaches the database and
    /// not the picture. LibVLC takes its subtitle rendering from instance options, so applying a
    /// chosen style means building the engine with it — a separate piece of work, and one only a
    /// physical screen can confirm.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_subtitle_style_is_chosen_with_the_mouse_and_kept()
    {
        _ = await SeedRootAsync(Path.Combine(_dataRoot, "media"), ScanPolicy.Manual);

        using var host = ShowShell(height: 2000);
        Navigate(host, AppRoute.Settings);

        await PressAsync(
            host,
            "SubtitleStyleAccessibleName",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Estilo de subtítulos in the settings index never opened its section");
        Assert.Equal(SettingsSection.Subtitles, host.ViewModel.CurrentSettingsSection);

        var style = host.ViewModel.SubtitleStyle;
        Assert.NotNull(style);

        var preferences = host.Application.Services.GetRequiredService<IPlaybackPreferenceRepository>();

        // A style somebody chose on a previous run, waiting in the database. Storing it and then
        // starting the window is the whole round trip: the half that reads was as absent as the half
        // that writes, so a choice stored by this scene's presses would have come back to nobody.
        var chosen = SubtitleStyle.Create(
            fontSizePercent: 130,
            fontFamily: "Verdana",
            foregroundHex: SubtitleStyle.EngineDefault.ForegroundHex,
            backgroundHex: SubtitleStyle.EngineDefault.BackgroundHex,
            backgroundOpacity: 0.4,
            outlineThickness: 1.5);
        await preferences.SaveAsync(
            new PlaybackPreference
            {
                Scope = PreferenceScope.Global,
                ScopeKey = PlaybackPreference.GlobalKey,
                SubtitleStyle = chosen,
            },
            TestContext.Current.CancellationToken);

        // The startup a person gets, which is where the load lives. It also starts the watchers and
        // the two passes that read their own switches — both off by default, so nothing is contacted.
        host.Application.ConfigureWindow(host.Window);
        await WaitForAsync(
            () => Task.FromResult(style!.FontSizePercent == chosen.FontSizePercent),
            "the style stored before this window opened never reached the screen it belongs to");
        Assert.Equal(chosen.FontFamily, style!.FontFamily);
        Assert.Equal(chosen.OutlineThickness, style.OutlineThickness);

        // What the application stored, as a sentence: a probe is compared by value, and a record read
        // fresh every time would report "it changed" for the click that must change nothing.
        async Task<string> StoredAsync()
        {
            var stored = await preferences.GetAsync(
                PreferenceScope.Global,
                PlaybackPreference.GlobalKey,
                TestContext.Current.CancellationToken);
            return stored?.SubtitleStyle is { } saved
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"{saved.FontSizePercent}|{saved.FontFamily}|{saved.BackgroundOpacity}|{saved.OutlineThickness}")
                : "nothing stored";
        }

        await PressAsync(
            host,
            "SubtitleStyleSizeLabel",
            StoredAsync,
            "dragging the subtitle size never left a size stored anywhere");

        await PressAsync(
            host,
            "SubtitleStyleBackgroundOpacityLabel",
            StoredAsync,
            "dragging the background opacity never left one stored anywhere");

        await PressAsync(
            host,
            "SubtitleStyleOutlineLabel",
            StoredAsync,
            "dragging the outline thickness never left one stored anywhere");

        // The family is a drop-down, so its effect is that it opens: what is chosen inside lands in a
        // popup root of its own.
        await PressAsync(
            host,
            "SubtitleStyleFamilyLabel",
            () => Resolve(host, "SubtitleStyleFamilyLabel") is ComboBox { IsDropDownOpen: true },
            "clicking the subtitle family never opened the list of families");
        CloseDropDown(host);

        // The two colours are swatches now rather than fields to type six hexadecimal digits into,
        // so each of the twelve is pressed. The alpha is left alone by design — opacity has a slider
        // of its own on this page — which is why the probe is the whole colour and the assertion is
        // on its three channels.
        foreach (var ink in SubtitleStyleViewModel.ForegroundSwatches.Reverse())
        {
            if (style!.ForegroundHex.EndsWith(ink[1..], StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await PressAsync(
                host,
                ink,
                () => style.ForegroundHex,
                $"clicking the {ink} ink never changed what the subtitles are written in",
                recordAs: ink == SubtitleStyleViewModel.ForegroundSwatches[0] ? "SubtitleForegroundFirst" : null);
            Assert.EndsWith(ink[1..], style.ForegroundHex, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var ground in SubtitleStyleViewModel.BackgroundSwatches.Reverse())
        {
            if (style!.BackgroundHex.EndsWith(ground[1..], StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await PressAsync(
                host,
                ground,
                () => style.BackgroundHex,
                $"clicking the {ground} ground never changed what is under the subtitles",
                recordAs: ground == SubtitleStyleViewModel.BackgroundSwatches[0] ? "SubtitleBackgroundFirst" : null);
            Assert.EndsWith(ground[1..], style.BackgroundHex, StringComparison.OrdinalIgnoreCase);
        }

        // And the three that were dragged are the three that were stored, each away from what the
        // window opened holding. Read from the database rather than from the screen, because the
        // screen is the half that was already known to work.
        var stored = await preferences.GetAsync(
            PreferenceScope.Global,
            PlaybackPreference.GlobalKey,
            TestContext.Current.CancellationToken);
        Assert.NotNull(stored?.SubtitleStyle);
        Assert.NotEqual(chosen.FontSizePercent, stored!.SubtitleStyle!.FontSizePercent);
        Assert.NotEqual(chosen.BackgroundOpacity, stored.SubtitleStyle.BackgroundOpacity);
        Assert.NotEqual(chosen.OutlineThickness, stored.SubtitleStyle.OutlineThickness);

        // And the two colours are where the swatches left them, which is the last one pressed of
        // each row. This used to assert that the ink was untouched — it was the one field with no
        // control of its own, and now it has six and a picker.
        Assert.EndsWith(
            SubtitleStyleViewModel.ForegroundSwatches[0][1..],
            stored.SubtitleStyle.ForegroundHex,
            StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(
            SubtitleStyleViewModel.BackgroundSwatches[0][1..],
            stored.SubtitleStyle.BackgroundHex,
            StringComparison.OrdinalIgnoreCase);

        // And nothing outside the style was disturbed: it is one field of a row that also carries
        // the track choices, so saving it has to leave the rest as it was.
        Assert.Equal(chosen.FontFamily, stored.SubtitleStyle.FontFamily);
    }

    /// <summary>
    /// Batch 2a: the tracks a session offers, and where its sound goes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>None</b> of the five is a drop-down any more, and that changed what this scene can ask
    /// for. Until 2026-09-02 four of them were, and a drop-down's only observable effect is that it
    /// <b>opens</b> — what is chosen inside one lands in a popup root of its own, which is a
    /// separate top level and not this window's business. The three lists are rows of radios now, so
    /// what each press is asked for is the choice itself: which track is playing, which output the
    /// sound is going to.
    /// </para>
    /// <para>
    /// The media is a sample with two audio tracks and a subtitle track, played as an episode of a
    /// seeded series — which the fifth control requires: "remember for this series" is only a
    /// question worth asking when there is a series to remember it for.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_tracks_and_the_audio_output_are_chosen_with_the_mouse()
    {
        var sample = await RequireMultiTrackSampleAsync("walk-tracks.mkv", durationSeconds: 12);
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var first = Path.Combine(media, "Show.S01E01.mkv");
        var second = Path.Combine(media, "Show.S01E02.mkv");
        File.Copy(sample, first);
        File.Copy(sample, second);

        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        var firstFile = await SeedMediaFileAsync(factory, media, first, TimeSpan.FromSeconds(12));
        var secondFile = await SeedMediaFileAsync(factory, media, second, TimeSpan.FromSeconds(12));
        var showId = await SeedSeriesAsync(factory, firstFile, secondFile);

        using var host = ShowShell(height: 1200);
        await host.ViewModel.OpenPlayerAsync(
            new PlayDetailsRequest(new MediaFileId(firstFile), StartPosition: null),
            TestContext.Current.CancellationToken);
        await WaitForAsync(
            () => Task.FromResult(host.ViewModel.Player?.Player.IsPlaying == true),
            "the session never reached the playing state on the real engine");
        Dispatcher.UIThread.RunJobs();

        // On the real engine, and this is the only place it can be measured: the picture started, so
        // everything that is not the picture went away. The header, the rail and the title bar are
        // gone and so is the transport, which is what the requirement asks for and what the
        // prototype does not do.
        Assert.False(host.ViewModel.IsChromeRevealed);
        Assert.False(host.ViewModel.Player!.Player.AreControlsRevealed);

        // And a movement of the mouse brings it back, which is how a person gets the controls again
        // and how every scene from here on reaches a control at all.
        await RevealChromeAsync(host);
        Assert.True(host.ViewModel.Player!.Player.AreControlsRevealed);

        var tracks = host.ViewModel.Player!.Tracks;
        Assert.NotNull(tracks);

        // What the engine announced for this media, which is what the two lists are made of. The
        // subtitle list carries one more than the file does: the entry that turns them off.
        Assert.Equal(2, tracks!.AudioTracks.Count);
        Assert.Equal(2, tracks.SubtitleTracks.Count);

        // The two lists live under two pills now, which is the prototype's grouping: what somebody
        // is hearing is one subject and what they are reading is another. So each one is reached the
        // way a person reaches it — by opening the panel that holds it first.
        await OpenPlayerPanelAsync(host, PlayerPanel.Audio);

        // The one that is not a list, and the one an earlier batch found a defect in: it decides
        // whether the choice is stored for this file or for the whole show. It is drawn under the
        // audio half and only there — one setting governing the pair.
        //
        // It is pressed BEFORE the track and that order is the scene, not a tidy-up: since
        // 2026-09-02 the track row is a real choice rather than a drop-down being opened, so
        // pressing it writes a preference. Pressed the other way round the write would land under
        // the file, and the assertion below — that nothing is stored under the file — would be
        // measuring the scene's own first press.
        await PressAsync(
            host,
            "TrackSelectorRememberForSeries",
            () => tracks.RememberForSeries,
            "clicking Remember for this series never asked for the choice to apply to the series");
        Assert.True(tracks.RememberForSeries);

        // The two lists stopped being drop-downs on 2026-09-02 and became rows of radios, which is
        // what the prototype draws — and the press got stronger for it. A drop-down could only be
        // asked whether it opened; a row can be asked whether the track it names is the one now
        // playing, which is the thing a person came to the panel to do.
        //
        // The row pressed is the one that is NOT already chosen, because pressing the choice that is
        // already in force has no effect to observe.
        var otherAudio = tracks.AudioTracks.Single(option => !ReferenceEquals(option, tracks.SelectedAudio));
        await PressAsync(
            host,
            "TrackSelectorAudioLabel",
            () => tracks.SelectedAudio?.Display,
            "clicking an audio track never changed which one is playing",
            helpText: otherAudio.Display);

        // By what the row says and not by instance: applying a track goes to the real engine, and
        // the session rebuilds its lists from what the engine announces back — so the option that
        // ends up chosen is an equal option, not the same object. Measured 2026-09-02, with both
        // sides printing the identical track.
        Assert.Equal(otherAudio.Display, tracks.SelectedAudio?.Display);

        await OpenPlayerPanelAsync(host, PlayerPanel.Subtitles);
        var otherSubtitle = tracks.SubtitleTracks
            .Single(option => !ReferenceEquals(option, tracks.SelectedSubtitle));
        await PressAsync(
            host,
            "TrackSelectorSubtitleLabel",
            () => tracks.SelectedSubtitle?.Display,
            "clicking a subtitle track never changed which one is showing",
            helpText: otherSubtitle.Display);
        Assert.Equal(otherSubtitle.Display, tracks.SelectedSubtitle?.Display);

        // And what the box means, not only that it ticks. With it on, the choice is stored under the
        // show rather than under this episode's file, and the key it is stored under is the same one
        // the session reads back — which is the defect an earlier batch found: the reading side
        // asked for the series and the writing side had none, so the application could resolve a
        // preference nothing in it could ever write.
        //
        // What performs the write is the row that was pressed two blocks up. Until 2026-09-02 this
        // scene had to call ApplyAsync itself, because opening a drop-down chose nothing.
        var preferences = host.Application.Services.GetRequiredService<IPlaybackPreferenceRepository>();
        Assert.NotNull(await preferences.GetAsync(
            PreferenceScope.Series,
            showId.ToString("D"),
            TestContext.Current.CancellationToken));
        Assert.Null(await preferences.GetAsync(
            PreferenceScope.File,
            firstFile.ToString("D"),
            TestContext.Current.CancellationToken));

        var audio = host.ViewModel.Player!.AudioOutput;
        Assert.NotNull(audio);

        await OpenPlayerPanelAsync(host, PlayerPanel.Audio);

        // The output list is a row per endpoint now, and pressing one has an effect only when there
        // is a row that is not already the chosen one — which is a fact about the machine the walk
        // happens to run on. This developer machine offers several and a hosted runner may offer
        // one, and a scene that pressed only «when there are two» would put this control on
        // eng/walk-pending.txt on one machine and not the other. That is the shape of list this
        // repository already measured as impossible to get right on both, on 2026-09-02.
        //
        // So the scene supplies the second row itself instead of hoping for one, and it is pressed
        // on every machine. The handler is put aside while it does: choosing a made-up endpoint has
        // nothing to write to, and a walk must not leave somebody's sound pointing at it. Both are
        // put back below, and the choice ends where it started.
        var chosenBefore = audio.SelectedDevice;
        var handler = audio.SelectionHandler;
        audio.SelectionHandler = null;
        var seeded = new AudioOutputOption(
            new AudioOutputDevice(
                "walk-second-endpoint",
                "Segundo destino (paseo)",
                [AudioChannelLayout.Stereo],
                IsDefault: false,
                IsAvailable: true),
            "Segundo destino (paseo)",
            "2.0");
        audio.Devices.Add(seeded);
        Dispatcher.UIThread.RunJobs();

        // The seeded row is on the list before anything is pressed, and it is the only one this
        // scene added. Without saying so, the assertions below would hold on a machine with no
        // render endpoint at all by comparing one absence with another.
        Assert.Contains(seeded, audio.Devices);
        Assert.Single(audio.Devices, option => ReferenceEquals(option, seeded));
        var countBefore = audio.Devices.Count;
        try
        {
            await PressAsync(
                host,
                "AudioOutputDeviceLabel",
                () => audio.SelectedDevice?.Display,
                "clicking an output never changed where the sound is going",
                helpText: seeded.Display);
            Assert.Equal(seeded.Display, audio.SelectedDevice?.Display);
        }
        finally
        {
            audio.SelectedDevice = chosenBefore;
            _ = audio.Devices.Remove(seeded);
            audio.SelectionHandler = handler;
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(chosenBefore?.Display, audio.SelectedDevice?.Display);
        Assert.DoesNotContain(seeded, audio.Devices);
        Assert.Equal(countBefore - 1, audio.Devices.Count);

        // The channel layout stopped being a drop-down on 2026-09-02 and is three buttons, which is
        // what the prototype draws — and what the walk has to press, one by one, because each of
        // them is its own command control. Which of the three a given machine will take is the
        // driver's business, so what is asserted is that pressing one moves the choice, and the
        // press is skipped on a layout this machine's endpoint refuses.
        //
        // Pressing a layout writes a Windows setting, and this walk runs on whatever endpoint the
        // machine has. Stereo is pressed last and deliberately: it is what every endpoint carries,
        // so the scene leaves the machine on the layout it would have chosen anyway.
        // The three layout buttons are asserted and not pressed, and the reason is that no list of
        // pending controls can be right on both machines at once. Each is offered only where the
        // chosen endpoint's driver takes it: on this developer machine every physical endpoint
        // declares two channels, so stereo is pressable and the other two are not; on a hosted
        // runner there is no render endpoint at all, so none of the three is. The walk gate is
        // symmetrical — it fails a control that is pending and not listed, AND a listed one that
        // turns out to be pressed — so a list with stereo in it goes red here and a list without it
        // goes red there. Measured on 2026-09-02: 219 pressed here against 218 in CI.
        //
        // What is asserted instead is the correspondence in both directions, which is the half a
        // press could not check anyway: a button is enabled exactly when the driver takes its
        // layout. The day this walk runs on a machine with multichannel output, the three come off
        // eng/walk-pending.txt and this loop presses them.
        foreach (var (name, layout) in new[]
        {
            ("AudioLayoutStereo", AudioChannelLayout.Stereo),
            ("AudioLayoutSurround51", AudioChannelLayout.Surround51),
            ("AudioLayoutSurround71", AudioChannelLayout.Surround71),
        })
        {
            var control = Resolve(host, name);
            Assert.Equal(audio.IsLayoutAvailable(layout), control.IsEnabled);
        }

        // The layouts are the application's own list rather than the machine's, so this one is the
        // same everywhere and can be asserted on.
        Assert.NotEmpty(AudioOutputViewModel.Layouts);

        // The fourth pill, which has no controls of its own: what the decoder and the display agreed
        // on is read, not chosen. It is pressed anyway because the pill itself is a control, and a
        // panel nobody in this suite ever opens is a panel nobody has ever seen drawn.
        Assert.True(host.ViewModel.HasVideoPanel);
        await OpenPlayerPanelAsync(host, PlayerPanel.Video);

        // And the column's own «×», which is the second way to close a panel. Asserted on the column
        // going away rather than on the button: what it is for is the 320 px, not the click.
        await PressAsync(
            host,
            "PlayerPanelClose",
            () => Task.FromResult(host.ViewModel.IsPlayerPanelOpen),
            "pressing the panel's close never gave the column's width back to the picture");
        Assert.False(host.ViewModel.IsPlayerPanelOpen);
        Assert.Equal(PlayerPanel.None, host.ViewModel.PlayerPanel);

        // Closed the way every scene with a session closes one, and the way a person does before
        // leaving. It is not tidying up: a shutdown that still holds a session takes the coordinator
        // through an engine the container has already let go of, which is a finding of its own and is
        // written down as one rather than hidden here.
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Closes whichever drop-down is open, the way a person dismisses one.
    /// </summary>
    /// <remarks>
    /// A popup is a top level of its own, so a click beside a control while one is open is a click
    /// into somebody else's window. Every press that opens one closes it before the next.
    /// </remarks>
    private static void CloseDropDown(ShellHost host)
    {
        host.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Batch 6b: the copy that is stopped while it is still copying.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cancel exists only while a copy runs — <c>IsEnabled="{Binding IsRunning}"</c> and a command
    /// that can only execute then — so the scene has to give the application enough to copy for a
    /// press to land inside it. That is seeding rather than a hook: what makes a copy take time is a
    /// library with things in it, which is the same thing that makes it take time for a person.
    /// </para>
    /// <para>
    /// Both levers were measured on this machine before this was written, timing
    /// <c>CreateBackup</c>. A catalogue of 1,000 / 10,000 / 50,000 rows answers in 51 / 147 /
    /// 1,159 ms; personal artwork of 2,000 / 4,000 / 8,000 files of 100 KiB, in 1,694 / 3,059 /
    /// 5,892 ms. The cost per file outweighs the cost per megabyte, so artwork is the lever and the
    /// count matters more than the size: 6,000 files of 50 KiB answer in 3,944 ms for 293 MB of
    /// seeding, where 6,000 of 100 KiB buy 4,377 ms for twice the disk. 50 KiB is what a 300×450
    /// poster weighs, and 6,000 files is 3,000 identified titles with a poster and a backdrop each.
    /// </para>
    /// <para>
    /// What is <em>not</em> done is making the copy slow from the composition. In the updater the
    /// slow part is the harness's own source and the product is untouched by it; here there is no
    /// such thing to make slow, so a hook would be changing the product in order to test it.
    /// </para>
    /// <para>
    /// The status is the probe for the same reason it is on the updater's Cancel: there is no moment
    /// between pressing it and the copy stopping for a probe to be satisfied by. What a status cannot
    /// say is that nothing was left behind, so the backups folder is asked afterwards — a run that
    /// published a copy and then said "cancelled" would look identical from the screen.
    /// </para>
    /// <para>
    /// The seeding numbers above are still what buys the window, but the scene no longer <em>asserts</em>
    /// on a clock. It used to compare the two presses against the measured copy duration, which is a
    /// number from one machine: a hosted runner failed it on 2026-08-19 with 4736 ms of presses
    /// against a copy calibrated at 3944 ms, and nothing was wrong. The question the clock was
    /// standing in for — did the copy end by itself before Cancel was pressed — is answered directly
    /// by watching every status the surface passes through, because a copy that finished says
    /// <c>BackupStatusDone</c>.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_copy_still_running_is_cancelled_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        var film = Path.Combine(media, "Arrival.2016.mp4");
        await File.WriteAllBytesAsync(film, [0x41, 0x50], TestContext.Current.CancellationToken);
        var factory = await SeedRootAsync(media, ScanPolicy.Manual);
        await SeedMediaFileAsync(factory, media, film, TimeSpan.FromMinutes(116));

        // The artwork of a library somebody has used for years, and the reason there is a window at
        // all. See the remarks for the ladder it came from and for why the count is the lever rather
        // than the size.
        var paths = new AppDataPaths(_dataRoot);
        await SeedPersonalArtworkAsync(paths, files: 6_000, kibibytes: 50);

        // Dot-prefixed folders are the store's own temporaries — its staging and the restore's — so
        // what this counts is copies somebody could restore.
        int StoredCopies() => Directory.Exists(paths.BackupsDirectory)
            ? Directory.GetDirectories(paths.BackupsDirectory)
                .Count(directory => !Path.GetFileName(directory).StartsWith('.'))
            : 0;

        using var host = ShowShell(height: 1400);
        Navigate(host, AppRoute.Settings);
        await PressAsync(
            host,
            "NavigationBackups",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Copias in the settings index never opened its section");
        Assert.Equal(SettingsSection.Backups, host.ViewModel.CurrentSettingsSection);
        var backups = host.ViewModel.Backups;
        Assert.NotNull(backups);

        // Every status this surface passes through, from before the copy starts. This is the scene's
        // net and it replaced a stopwatch on 2026-08-19: the stopwatch compared the two presses
        // against a duration measured on one machine, so on a slower runner it failed a run where
        // nothing was wrong — 4736 ms of presses against a copy calibrated at 3944 ms. What it was
        // really asking is whether the copy finished on its own before Cancel was pressed, and that
        // is not an inference from a clock: BackupStatusDone is the surface saying so out loud. A
        // press that changed the status only because the copy had ended would leave Done in here,
        // and no machine is fast or slow enough to change that.
        var statuses = new List<string>();
        backups!.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(BackupViewModel.StatusKey))
            {
                statuses.Add(backups.StatusKey);
            }
        };

        var window = Stopwatch.StartNew();
        await PressAsync(
            host,
            "BackupCreateCopyLabel",
            () => backups.StatusKey,
            "clicking Create a backup now never started the copy this scene is here to stop");

        Assert.Equal("BackupStatusRunning", backups.StatusKey);
        Assert.True(backups.IsRunning, "The copy was not running, so there was nothing to cancel.");

        await PressAsync(
            host,
            "BackupCancelLabel",
            () => backups.StatusKey,
            "clicking Cancel never stopped the copy that was running");

        // The copy never finished on its own, so Cancel is what stopped it. The whole route is in
        // both complaints, and the elapsed time with it: when this does fail, the first thing worth
        // knowing is where the surface went and how long it had to get there.
        var route = $"{string.Join(" → ", statuses)} in {window.ElapsedMilliseconds} ms";
        Assert.True(
            !statuses.Contains("BackupStatusDone", StringComparer.Ordinal),
            $"The surface went {route}, so the copy finished on its own and Cancel was pressed on a "
                + "screen with nothing running. Seed more artwork.");
        Assert.True(
            statuses.Contains("BackupStatusCancelled", StringComparer.Ordinal),
            $"The surface went {route}, and never reached the cancelled state, so pressing Cancel "
                + "did not stop the copy.");

        Assert.Equal("BackupStatusCancelled", backups.StatusKey);

        // And nothing was published. The staging folder is discarded on the way out, so a cancelled
        // copy leaves the backups folder exactly as it found it.
        Assert.Equal(0, StoredCopies());
    }

    /// <summary>
    /// The personal artwork a long-used library holds, written straight to disk.
    /// </summary>
    /// <remarks>
    /// Written rather than catalogued on purpose: what a copy walks is the folder, not the rows, so
    /// this is the smallest seeding that produces the time the scene needs. The bytes differ per file
    /// only in their first four, which is enough for the hashes the manifest records to differ while
    /// the seeding stays one buffer.
    /// </remarks>
    private static async Task SeedPersonalArtworkAsync(AppDataPaths paths, int files, int kibibytes)
    {
        Directory.CreateDirectory(paths.PersonalArtworkDirectory);
        var image = new byte[1024 * kibibytes];
        Random.Shared.NextBytes(image);
        for (var index = 0; index < files; index++)
        {
            BitConverter.TryWriteBytes(image, index);
            await File.WriteAllBytesAsync(
                Path.Combine(
                    paths.PersonalArtworkDirectory,
                    string.Create(CultureInfo.InvariantCulture, $"poster-{index}.jpg")),
                image,
                TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Batch 7a: the permission to look for updates, and the look itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The switch is the only one of the updater's five controls that needs nothing staged, and it is
    /// the one whose effect is furthest from the screen: a check is a connection, so the answer to
    /// "may it look on its own?" has to survive the window closing. The probe therefore reads the
    /// stored setting rather than the box, and the file it lives in is read afterwards — a switch
    /// that only moved its own bool would look identical from the view model.
    /// </para>
    /// <para>
    /// Nothing is contacted by either press. The automatic pass runs from <c>ConfigureWindow</c>,
    /// which this scene does not call, and the source a run with a data root of its own is built with
    /// reads its own handover folder — which is what makes Check pressable at all. Asking GitHub what
    /// it has published, from whichever machine happens to run the suite, is not something a test may
    /// do.
    /// </para>
    /// <para>
    /// The manifest is written after the shell is up, on purpose: the source reads it per question
    /// rather than at construction, so a run that has nothing to install and one that has something
    /// are the same application at two moments rather than two applications.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_updater_is_allowed_to_look_and_then_asked_to_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        _ = await SeedRootAsync(media, ScanPolicy.Manual);

        using var host = ShowShell(height: 2000);
        Navigate(host, AppRoute.Settings);

        await PressAsync(
            host,
            "UpdateTitle",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Actualización in the settings index never opened its section");
        Assert.Equal(SettingsSection.Updates, host.ViewModel.CurrentSettingsSection);

        var updates = host.ViewModel.Updates;
        Assert.NotNull(updates);
        var settings = host.Application.Services.GetRequiredService<IUpdateSettings>();
        Assert.False(
            settings.AutomaticCheckEnabled,
            "An installation nobody has asked starts with automatic checks off, and this one did not.");

        await PressAsync(
            host,
            "UpdateAutomaticCheckLabel",
            () => settings.AutomaticCheckEnabled,
            "clicking the automatic-check box never changed whether the application may look on its own");

        Assert.True(updates!.AutomaticCheckEnabled);

        // And it reached the file, which is what makes it an answer rather than a state of this
        // window: the next launch reads this and nothing else.
        var stored = await File.ReadAllTextAsync(
            new AppDataPaths(_dataRoot).SettingsPath,
            TestContext.Current.CancellationToken);
        Assert.Contains("updates.automaticCheckEnabled", stored, StringComparison.Ordinal);

        // Now there is something to find. The version is far past anything this build could carry,
        // because what is being measured is the press and not the comparison — UpdatePolicy has its
        // own tests for which versions are newer than which.
        await SeedUpdateManifestAsync();

        await PressAsync(
            host,
            "UpdateCheckLabel",
            () => updates.StatusKey,
            "clicking Check for updates never asked anything");

        Assert.Equal("UpdateStatusOffered", updates.StatusKey);
        Assert.Equal("999.0.0", updates.OfferedVersion);

        // The download is the product's own: what changes for this run is where the bytes come from,
        // so the hash and the size the manifest declared are checked against what actually arrived,
        // and the file is staged under .partial until they match.
        //
        // The probe counts staged packages rather than reading the status, and that is the third
        // time this shape has cost a measurement: a status turns to "downloading" the instant the
        // button is pressed, so a probe watching it is satisfied by the press having *started*
        // something. The staging folder only holds a package once the hash and the size agree.
        var staging = Path.Combine(_dataRoot, "updates");
        int StagedPackages() => Directory.Exists(staging)
            ? Directory.GetFiles(staging, "*.msix", SearchOption.AllDirectories).Length
            : 0;

        await PressAsync(
            host,
            "UpdateDownloadLabel",
            StagedPackages,
            "clicking Download never fetched the package this run was offered");

        // And the screen is asked what it settled on only once it has settled. The disk changes
        // before the screen does, so reading the status straight after the effect is asserting on a
        // race — measured here, and before this on backups and on the privacy report.
        await SettledUpdateAsync(updates, "UpdateStatusReady", "downloading");
        var staged = Directory.GetFiles(staging, "*.msix", SearchOption.AllDirectories);
        Assert.Single(staged);
        Assert.Empty(Directory.GetFiles(staging, "*.partial", SearchOption.AllDirectories));

        // And the confirmation, which is the only thing in this application that constructs a
        // consent. What it hands over is read back from the record rather than from the screen: a
        // screen can say "handed to Windows" with nothing having left.
        var handoffRecord = Path.Combine(
            new AppDataPaths(_dataRoot).SystemHandoffDirectory!,
            RecordingSystemHandoff.FileName);
        await PressAsync(
            host,
            "UpdateInstallLabel",
            () => ReadRecord(handoffRecord),
            "clicking Install never handed the package anywhere");

        await SettledUpdateAsync(updates, "UpdateStatusHandedToWindows", "installing");
        Assert.Equal(
            $"{RecordingSystemHandoff.OpenPackageVerb} {staged[0]}",
            RecordedLines(handoffRecord)[^1]);
    }

    /// <summary>
    /// Batch 7c: the fetch that is still arriving, stopped with the mouse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cancel is the one control on this screen that does not exist on an idle one:
    /// <c>IsEnabled="{Binding IsBusy}"</c> and a command that can only execute while busy, so it can
    /// be pressed only during one of the three steps. With the package on the disk beside it the whole
    /// download finishes in milliseconds, so this scene declares how slowly the transport is to answer
    /// and presses inside that window.
    /// </para>
    /// <para>
    /// The wait is the harness's, on the same line everything else on this surface is drawn on: what
    /// an isolated run replaces is where the bytes come from, and a source is entitled to be slow.
    /// Nothing was added to the product to make this pressable — the cancellation travels the real
    /// path, from the token the view model created into the transport's wait, out as
    /// <see cref="OperationCanceledException"/> and into the status the screen reads.
    /// </para>
    /// <para>
    /// Its own scene rather than a step in the one before it: a transport that answers slowly would
    /// change what Download and Install measure, and those are already green.
    /// </para>
    /// <para>
    /// Here the status is the probe, and it is the one place on this surface where that is safe —
    /// there is no moment between pressing Cancel and the fetch stopping for a probe to be satisfied
    /// by, which is the race that cost three measurements elsewhere. What a status cannot say is that
    /// nothing arrived, so the staging folder is asked afterwards.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Batch: the refusal grammar, pressed. A guardian's no is the system working, and §4 gives it
    /// the reason as the headline and the rule's identifier behind a fold — so the scene serves a
    /// release this machine cannot run, watches the refusal arrive with the reason named, and
    /// unfolds the technical detail with the mouse.
    /// </summary>
    /// <remarks>
    /// The manifest's runtime is an architecture nobody ships, which makes the refusal
    /// <c>WrongRuntime</c>: the one rejection a scene can cause without touching the hash pipeline.
    /// Nothing is contacted — the source reads this run's own handover folder, like every updater
    /// scene. The fold's effect is its own state: what unfolds is a TextBlock in the same region,
    /// not a popup, so the probe reads the toggle it pressed.
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task A_refused_release_names_its_rule_and_unfolds_the_detail_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        _ = await SeedRootAsync(media, ScanPolicy.Manual);

        using var host = ShowShell(height: 2000);
        Navigate(host, AppRoute.Settings);

        await PressAsync(
            host,
            "UpdateTitle",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Actualización in the settings index never opened its section");

        var updates = host.ViewModel.Updates;
        Assert.NotNull(updates);

        // A release for a machine that does not exist: parseable, complete, and refused by the
        // policy for its runtime alone.
        var handoff = new AppDataPaths(_dataRoot).SystemHandoffDirectory;
        Assert.NotNull(handoff);
        Directory.CreateDirectory(handoff!);
        await File.WriteAllTextAsync(
            Path.Combine(handoff!, HandoffUpdateManifest.FileName),
            """
            {
              "version": "999.0.0",
              "runtime": "alien-arch",
              "url": "https://updates.handoff.invalid/apreelume-999.0.0.msix",
              "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "sizeInBytes": 64,
              "summaryEs": "Lo que cambia en esta versión.",
              "summaryEn": "What changed in this version.",
              "packageFile": "apreelume-999.0.0.msix"
            }
            """,
            TestContext.Current.CancellationToken);

        await PressAsync(
            host,
            "UpdateCheckLabel",
            () => updates!.StatusKey,
            "clicking Check for updates never asked anything");

        Assert.Equal("UpdateStatusUnusableRelease", updates!.StatusKey);
        Assert.Equal("UpdateRefusedWrongRuntime", updates.DetailKey);
        Assert.True(updates.IsStatusRejection, "The refusal never wore its own grammar.");

        await PressAsync(
            host,
            "UpdateRejectionDetailAction",
            () => Resolve(host, "UpdateRejectionDetailAction") is Avalonia.Controls.Primitives.ToggleButton { IsChecked: true },
            "clicking the technical-detail fold never unfolded it");

        Assert.Equal("WrongRuntime", updates.RejectionCode);
    }

    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_fetch_still_arriving_is_cancelled_with_the_mouse()
    {
        var media = Path.Combine(_dataRoot, "media");
        Directory.CreateDirectory(media);
        _ = await SeedRootAsync(media, ScanPolicy.Manual);

        using var host = ShowShell(height: 2000);
        Navigate(host, AppRoute.Settings);

        await PressAsync(
            host,
            "UpdateTitle",
            () => host.ViewModel.CurrentSettingsSection,
            "clicking Actualización in the settings index never opened its section");
        Assert.Equal(SettingsSection.Updates, host.ViewModel.CurrentSettingsSection);

        var updates = host.ViewModel.Updates;
        Assert.NotNull(updates);

        // How long the transport holds the answer, and therefore the window both presses below have
        // to happen inside. Measured rather than chosen: the two presses spend 950 ms of it here, and
        // the window also has to hold the harness's own retry budget — PressAsync presses up to eight
        // times, a settle apart, which is another 2400 ms of real time. 950 + 2400 is what 3000 would
        // not have held, and a run that has already cancelled abandons the rest of the wait, so a
        // window with room in it costs nothing.
        const int ServeDelayMilliseconds = 5000;
        await SeedUpdateManifestAsync(ServeDelayMilliseconds);

        await PressAsync(
            host,
            "UpdateCheckLabel",
            () => updates!.StatusKey,
            "clicking Check for updates never asked anything");

        Assert.Equal("UpdateStatusOffered", updates!.StatusKey);

        // Started, and deliberately not finished. A status that turns the instant a button is pressed
        // is exactly the wrong probe for a press that has to land — and exactly the right one for a
        // press whose whole point is that something is now in flight.
        var window = Stopwatch.StartNew();
        await PressAsync(
            host,
            "UpdateDownloadLabel",
            () => updates.StatusKey,
            "clicking Download never started the fetch this scene is here to stop");

        Assert.Equal("UpdateStatusDownloading", updates.StatusKey);
        Assert.True(updates.IsBusy, "The fetch was not running, so there was nothing to cancel.");

        await PressAsync(
            host,
            "UpdateCancelLabel",
            () => updates.StatusKey,
            "clicking Cancel never stopped the fetch that was still arriving");

        // The window, spent. It is an upper bound — it starts before the press that starts the fetch,
        // so it counts the click beside Download as well — and it is asserted rather than only
        // written down, because a scene that presses after the fetch has finished measures the
        // control's absence and would say so far less clearly.
        Assert.True(
            window.ElapsedMilliseconds < ServeDelayMilliseconds,
            $"Both presses took {window.ElapsedMilliseconds} ms of a {ServeDelayMilliseconds} ms "
                + "window, so the fetch had already finished and Cancel was pressed on a screen with "
                + "nothing running. Declare a longer wait in the manifest.");

        Assert.Equal("UpdateStatusCancelled", updates.StatusKey);

        // And nothing arrived. The status says the fetch stopped; only the folder says that stopping
        // it left no package behind.
        var staging = Path.Combine(_dataRoot, "updates");
        Assert.Empty(Directory.Exists(staging)
            ? Directory.GetFiles(staging, "*.msix", SearchOption.AllDirectories)
            : []);
    }

    /// <summary>
    /// Reads a handover record while the application may still be writing to it.
    /// </summary>
    /// <remarks>
    /// Sharing the write is not caution, it is the fix for a measured failure: the record is appended
    /// to from whichever thread the effect lands on, and a probe opening it the ordinary way lost the
    /// race on the second pass of the accessibility gate with "the process cannot access the file".
    /// A probe that reads a file the application writes has to share the write, or it is measuring
    /// the race rather than the effect.
    /// </remarks>
    private static string ReadRecord(string path)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (IOException)
        {
            // Still being written this instant. "Nothing yet" is the honest answer, and PressAsync
            // asks again.
            return string.Empty;
        }
    }

    /// <summary>The same record, as the lines it holds.</summary>
    private static string[] RecordedLines(string path) =>
        ReadRecord(path).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Waits for the updater to stop working and says what it stopped as.
    /// </summary>
    /// <remarks>
    /// The same shape as the backup surface's, and for the same reason: the effect lands before the
    /// screen does, so the outcome is waited for rather than read straight after the press. Third
    /// appearance of this race in this repository.
    /// </remarks>
    private static async Task SettledUpdateAsync(UpdateViewModel updates, string expected, string what)
    {
        await WaitForAsync(
            () => Task.FromResult(!updates.IsBusy),
            $"The updater was still {what} a minute after the effect had landed.");
        Assert.Equal(expected, updates.StatusKey);
    }

    /// <summary>
    /// Writes the release a run with a data root of its own is offered, in the folder that run says
    /// it uses for handovers.
    /// </summary>
    /// <param name="serveDelayMilliseconds">
    /// How slowly the transport is to answer, for a scene that has to press something while the fetch
    /// is still in flight. Zero leaves the field out altogether, which is the shape every other scene
    /// uses and the one a manifest written by anything else would have.
    /// </param>
    /// <remarks>
    /// The address is data rather than source, and that is the rule rather than a convenience: a test
    /// walks <c>src/</c> for anything shaped like a host and fails on one the network purpose
    /// registry does not declare. Nothing is contacted either way — no manifest here names a place
    /// this application would connect to.
    /// </remarks>
    private async Task SeedUpdateManifestAsync(int serveDelayMilliseconds = 0)
    {
        var handoff = new AppDataPaths(_dataRoot).SystemHandoffDirectory;
        Assert.NotNull(handoff);
        Directory.CreateDirectory(handoff!);

        // A package with bytes in it, and a manifest describing exactly those bytes. The hash and
        // the size are computed here rather than declared, because the download checks what arrives
        // against what was promised: a manifest that promised something else would be measuring the
        // verification instead of the button.
        const string PackageFile = "apreelume-999.0.0.msix";
        var package = Path.Combine(handoff!, PackageFile);
        var bytes = System.Text.Encoding.UTF8.GetBytes(
            "a package this run would install, if this run installed anything");
        await File.WriteAllBytesAsync(package, bytes, TestContext.Current.CancellationToken);
        var sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))
            .ToLowerInvariant();

        var serveDelay = serveDelayMilliseconds > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $",{Environment.NewLine}  \"serveDelayMilliseconds\": {serveDelayMilliseconds}")
            : string.Empty;

        await File.WriteAllTextAsync(
            Path.Combine(handoff!, HandoffUpdateManifest.FileName),
            string.Create(
                CultureInfo.InvariantCulture,
                $$"""
                {
                  "version": "999.0.0",
                  "runtime": "{{System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}}",
                  "url": "https://updates.handoff.invalid/{{PackageFile}}",
                  "sha256": "{{sha256}}",
                  "sizeInBytes": {{bytes.Length}},
                  "summaryEs": "Lo que cambia en esta versión.",
                  "summaryEn": "What changed in this version.",
                  "packageFile": "{{PackageFile}}"{{serveDelay}}
                }
                """),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Batch 7b: the screen that appears when the library will not open, pressed with the mouse, and
    /// what each press would have handed to Windows read back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No route leads here, and that is not an oversight: <c>CreateShell</c> builds this screen only
    /// when the startup's answer is a refusal, so the scene has to seed a database that cannot be
    /// used and let the application decide. Which is also why the shell's own mount cannot be used —
    /// it asserts the settled content is the shell, and here it is deliberately not.
    /// </para>
    /// <para>
    /// Both controls were uncoverable for the same reason and it is the reason the isolation rule
    /// exists: one opened a real Explorer window on whichever machine was measuring, and the other
    /// called shutdown on the process doing the measuring. The second is the harder shape — under a
    /// headless harness there is no desktop lifetime, so it did nothing at all, which is exactly what
    /// a broken control also does.
    /// </para>
    /// <para>
    /// The probe is the handover record as text. Text and not a list, because a probe is compared by
    /// value and a fresh array every read would report "it changed" for the click that must change
    /// nothing.
    /// </para>
    /// </remarks>
    [AvaloniaFact(Timeout = 120_000)]
    public async Task The_recovery_screen_is_pressed_when_the_library_will_not_open()
    {
        // A database that is not a database. What the migration says about it is the application's
        // to decide; the scene only makes the file unusable, which is what a person meets.
        Directory.CreateDirectory(_dataRoot);
        var paths = new AppDataPaths(_dataRoot);
        await File.WriteAllTextAsync(
            paths.DatabasePath,
            "this file is not a database",
            TestContext.Current.CancellationToken);

        // And a copy for the screen to offer, so the folder the first press shows is a folder a copy
        // is really in rather than the place one would have been.
        await File.WriteAllTextAsync(
            $"{paths.DatabasePath}.pre-migration-20260817T000000Z.bak",
            "a copy of a library",
            TestContext.Current.CancellationToken);

        // Where this run says it puts what it would have handed to Windows. Asserting it is not null
        // is what proves this scene tests the isolated exit rather than passing quietly against one
        // that would have opened a window on whoever ran it.
        var handoff = paths.SystemHandoffDirectory;
        Assert.NotNull(handoff);
        var record = Path.Combine(handoff!, RecordingSystemHandoff.FileName);

        using var host = ShowRecovery(height: 1000);

        await PressAsync(
            host,
            "RecoveryOpenBackupFolder",
            () => ReadRecord(record),
            "clicking Open the backup folder never said which folder it would have shown");

        // The folder is the one the copies are in, which is the application's answer and not a path
        // this scene composed.
        Assert.Equal(
            [$"{RecordingSystemHandoff.OpenFolderVerb} {paths.DataRoot}"],
            RecordedLines(record));

        await PressAsync(
            host,
            "RecoveryExit",
            () => ReadRecord(record),
            "clicking Exit never asked for anything, which is what it did before this rule existed");

        // Both handovers, in the order they were pressed. The second line is the whole of what
        // leaving does — an application that ended here would have taken the suite with it.
        Assert.Equal(
            [
                $"{RecordingSystemHandoff.OpenFolderVerb} {paths.DataRoot}",
                RecordingSystemHandoff.ExitVerb,
            ],
            RecordedLines(record));
    }

    /// <summary>
    /// Waits for a backup operation to finish and says what it finished as.
    /// </summary>
    /// <remarks>
    /// The disk changes before the screen does: a copy is published, and the status key is set on the
    /// continuation that runs afterwards. A probe reading the folder is therefore satisfied while the
    /// screen still says running, so the outcome has to be waited for — reading it straight after the
    /// press would be asserting on a race.
    /// </remarks>
    private static async Task SettledAsync(BackupViewModel backups, string expected, string what)
    {
        await WaitForAsync(
            () => Task.FromResult(!backups.IsRunning),
            $"The screen was still {what} a minute after the disk said it was done.");
        Assert.Equal(expected, backups.StatusKey);
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
    /// <summary>
    /// Opens one of the player column's panels by pressing the pill that heads it, as a person does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The column was a tab strip until the pills took over the switching, and the walk is what
    /// measured the cost of each move: when the five panels stopped being stacked, four scenes went
    /// red at once saying a control "matched 0 controls on screen" — a panel that is not open is not
    /// in the tree, so a click has nowhere to land. That is the whole point of pressing with a mouse,
    /// and it is the same reason this exists now that the column also starts closed.
    /// </para>
    /// <para>
    /// A pill <b>is</b> a command control, unlike the tab it replaced, so this records one in the
    /// ledger rather than quietly putting a panel on screen — which is what keeps the five of them
    /// out of eng/walk-pending.txt. A panel already open is left alone: pressing its pill again is
    /// what closes it, and a press that undid the scene's own setup would prove nothing.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Brings the chrome back the way a person does, and says so when it does not come.
    /// </summary>
    /// <remarks>
    /// The application takes everything but the picture away the moment a file starts playing, and
    /// gives it back on a movement of the mouse or a key. Every scene that presses something on the
    /// header of a running session goes through here, so the reveal is a measured gesture rather
    /// than a state somebody set from the outside.
    /// </remarks>
    private static async Task RevealChromeAsync(ShellHost host)
    {
        if (host.ViewModel.IsChromeRevealed)
        {
            return;
        }

        host.Window.MouseMove(
            new Point(host.Window.Bounds.Width / 2, host.Window.Bounds.Height / 2),
            RawInputModifiers.None);
        await SettleAsync();
        Assert.True(
            host.ViewModel.IsChromeRevealed,
            "moving the mouse never brought the chrome back, so nothing on the header is reachable.");
    }

    private static async Task OpenPlayerPanelAsync(ShellHost host, PlayerPanel panel)
    {
        if (host.ViewModel.PlayerPanel == panel)
        {
            return;
        }

        // A pill cannot be pressed while the chrome is away, and that is not an obstacle to work
        // around — it is the requirement: a session that is playing shows the picture and nothing
        // else until somebody moves the mouse. So the mouse is moved, exactly as a person moves it,
        // and the reveal is asserted rather than assumed.
        await RevealChromeAsync(host);

        var pill = panel switch
        {
            PlayerPanel.Audio => "PlayerPanelAudio",
            PlayerPanel.Subtitles => "PlayerPanelSubtitles",
            PlayerPanel.Video => "PlayerPanelVideo",
            PlayerPanel.Markers => "PlayerPanelMarkers",
            PlayerPanel.Lessons => "PlayerLessonsPanelTitle",
            _ => "PlayerVersionsTitle",
        };

        await PressAsync(
            host,
            pill,
            () => Task.FromResult(host.ViewModel.PlayerPanel),
            $"pressing the {pill} pill never opened the panel behind it");
        Assert.Equal(panel, host.ViewModel.PlayerPanel);
    }

    private static Control Resolve(ShellHost host, string anchor, string? helpText = null)
    {
        var expected = Avalonia.Application.Current!.TryFindResource(anchor, out var resolved) && resolved is string text
            ? text
            : anchor;
        var matches = Reachable(host)
            .Where(candidate =>
                candidate.Name == anchor || AutomationProperties.GetName(candidate) == expected)
            .Where(candidate => candidate.IsEffectivelyVisible)
            .Where(candidate => helpText is null || AutomationProperties.GetHelpText(candidate) == helpText)
            .ToArray();

        // A name shared between a command and the region it leads to is not an ambiguity a person
        // has: the rail's Home button and the Home surface are both called "Inicio", and a screen
        // reader tells them apart by role. So when more than one thing answers to a name, the
        // command controls are what a click can mean — and only if that leaves exactly one, because
        // two buttons with one name is a real defect and this scene must not hide it.
        if (matches.Length > 1)
        {
            var commands = matches.Where(IsCommandControl).ToArray();
            if (commands.Length == 1)
            {
                return commands[0];
            }
        }

        Assert.True(
            matches.Length == 1,
            $"{anchor}{(helpText is null ? string.Empty : $" [{helpText}]")} matched {matches.Length} "
                + $"controls on screen ({string.Join(", ", matches.Select(match => match.GetType().Name))}); "
                + "a click needs exactly one.");
        return matches[0];
    }

    /// <summary>
    /// Everything a click can reach: the shell, and any window the shell has opened.
    /// </summary>
    /// <remarks>
    /// Until 2026-08-19 this was the shell alone, and it was enough because the application had one
    /// window. The mini player is the second, and a control inside it is a control the walk could
    /// look straight past — which reads as "the application does not declare it" rather than as
    /// "the harness cannot see it".
    /// </remarks>
    private static IEnumerable<Control> Reachable(ShellHost host) =>
        host.Shell.GetVisualDescendants()
            .OfType<Control>()
            .Concat(SecondaryWindows(host).SelectMany(window => window.GetVisualDescendants().OfType<Control>()));

    /// <summary>
    /// The windows the shell has opened beside its own, found through the surface they hold.
    /// </summary>
    /// <remarks>
    /// Through the stage rather than through the application's window list: the stage is named, the
    /// shell keeps its name scope wherever the stage travels, and asking it is the same question a
    /// person answers by looking — "where is the picture right now".
    /// </remarks>
    private static IEnumerable<Window> SecondaryWindows(ShellHost host)
    {
        var stage = (host.Shell as UserControl)?.FindControl<Panel>("PlayerStage");
        return stage is null
            ? []
            : stage.GetVisualAncestors()
                .OfType<Window>()
                .Where(window => !ReferenceEquals(window, host.Window))
                .Take(1);
    }

    /// <summary>The window a click on this control has to be aimed at.</summary>
    private static Window RootOf(ShellHost host, Control control) =>
        control.GetVisualAncestors().OfType<Window>().FirstOrDefault() ?? host.Window;

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
        var window = RootOf(host, control);
        var scrollers = control.GetVisualAncestors().OfType<ScrollViewer>().ToArray();
        foreach (var scroller in scrollers)
        {
            scroller.Offset = scroller.Offset.WithY(0);
        }

        // <b>InvalidateMeasure and not UpdateLayout alone</b>, and this is the thirteenth cause of a
        // red that only ever appeared in CI, finally put where the rule is rather than where it hurt.
        //
        // UpdateLayout runs the layout pass only if the tree is dirty, and measured over a full walk
        // on 2026-09-02 the window reported IsMeasureValid and IsArrangeValid on <b>all 250</b>
        // beside-clicks — and forcing a pass anyway moved a control's rectangle in five of them. So a
        // tree that calls itself clean is not a tree whose descendants' geometry is current, and the
        // difference is invisible to any caller that only asks.
        //
        // It lived in one scene from 2026-08-28 (`Expected: Embedded, Actual: Fullscreen`, when the
        // beside-click landed on the mode button next door) and in BesidePoint from 2026-09-01, when
        // the same red came back from the press after that one: pressing «back to 1×» REMOVES that
        // button from the row, so the row recomposes again. Both were the line somebody remembered.
        // Here it covers every press, because Click goes through Reveal too — and the ordinary press
        // was reading a control's centre with exactly the staleness the beside-click was protected
        // from.
        //
        // <b>`window` and not `host.Window`</b>: with the mini player on screen those are two
        // different windows, and settling the shell to then read the other one's Bounds leaves the
        // staleness this is here to remove.
        //
        // <b>It is not reproducible on this machine</b> — the case alone and the whole suite have run
        // green here every time, across all three dates — so what confirms the red is CI's second
        // pass. What is measurable here is the mechanism above, and that is what was measured.
        window.InvalidateMeasure();
        window.UpdateLayout();
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
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            // BringIntoView stops at the nearest edge, which leaves a control at the very bottom of a
            // nested viewer still clipped. The wheel keeps going, so this does too.
            foreach (var scroller in scrollers.Where(s => s.Offset.Y < s.Extent.Height - s.Viewport.Height))
            {
                scroller.Offset = scroller.Offset.WithY(Math.Min(
                    scroller.Offset.Y + 120,
                    Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height)));
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }
        }

        return scrollers;
    }

    /// <summary>
    /// Whether this is a thing a person presses, using the same list of element types the coverage
    /// gate counts in eng/check-walk-coverage.ps1. A surface or a region may carry a name too.
    /// </summary>
    private static bool IsCommandControl(Control control) =>
        control is Button or CheckBox or ComboBox or Slider or Avalonia.Controls.Primitives.ToggleButton;

    /// <summary>Whether the control's middle is somewhere a click can actually land.</summary>
    /// <remarks>
    /// <para>
    /// Inside the window, <b>and</b> inside every scroller between it and the window. Until
    /// 2026-08-22 this asked the window alone, and that made it blind rather than wrong: a scroller
    /// clips its content, so a control can sit well inside the window and be cut off by the viewer it
    /// lives in. <c>Reveal</c> asks this before deciding whether to scroll, so a "yes" here meant it
    /// scrolled nothing and the press went to whatever the clip left behind.
    /// </para>
    /// <para>
    /// Measured the day it was found, in the walk's 1600x1000 window: Review versions sat at y=939
    /// with a height of 36 inside a viewer whose viewport ended at 952. Its middle - 957 - was inside
    /// the window and 5 px outside the viewer, so eight presses reached the shell's own Grid while
    /// this function kept answering that the button was fine where it was.
    /// </para>
    /// </remarks>
    private static bool Fits(ShellHost host, Control control) =>
        RootOf(host, control) is { } window
        && control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
            window) is { } centre
        && centre.X >= 0
        && centre.Y >= 0
        && centre.X < window.Bounds.Width
        && centre.Y < window.Bounds.Height
        && control.GetVisualAncestors().OfType<ScrollViewer>().All(scroller => IsInside(scroller, centre, window));

    /// <summary>Whether a window point falls inside what this scroller is actually showing.</summary>
    private static bool IsInside(ScrollViewer scroller, Point centre, Visual window) =>
        scroller.TranslatePoint(new Point(0, 0), window) is not { } corner
        || (centre.X >= corner.X
            && centre.Y >= corner.Y
            && centre.X <= corner.X + scroller.Viewport.Width
            && centre.Y <= corner.Y + scroller.Viewport.Height);

    /// <summary>The control's own rectangle in window coordinates.</summary>
    /// <remarks>
    /// The whole of it and not its middle, which is the difference between a control that can be
    /// pressed and one that merely has a centre somewhere legal. Measured on 2026-08-25: a density
    /// pill 36 px tall resting at y=964 in a 1000 px window had its centre at 982 — inside by every
    /// arithmetic — and eight presses reached the scroller behind it, because the half of it below
    /// the fold was the half the viewer had stopped drawing.
    /// </remarks>
    private static Rect Whole(Control control, Point corner) => new(corner, control.Bounds.Size);

    /// <summary>Whether a window point falls inside what this scroller is actually showing.</summary>


    /// <summary>
    /// Presses a control with the mouse, at its centre in window coordinates, the way a person with a
    /// pointing device does.
    /// </summary>
    private static Point Click(ShellHost host, Control control)
    {
        var window = RootOf(host, control);
        var scrollers = Reveal(host, control);

        // The middle, except on a range control, where the middle is usually where the value already
        // is: the volume slider runs from 0 to 200 and starts at 100, so pressing its centre asks for
        // exactly the level already playing and nothing changes. A quarter along asks for something
        // else, which is what pressing a slider is for. Measured on 2026-08-15, after a press that
        // did reach the control and still moved nothing.
        var alongX = control is Slider ? 0.25 : 0.5;
        var centre = control.TranslatePoint(
            new Point(control.Bounds.Width * alongX, control.Bounds.Height / 2),
            window);

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
                && centre.Value.X < window.Bounds.Width
                && centre.Value.Y < window.Bounds.Height,
            $"{Describe(control)} sits at {centre.Value} and the window is "
                + $"{window.Bounds.Width}x{window.Bounds.Height}, so the press would land "
                + "outside it. Scrollers between it and the window: "
                + (scrollers.Length == 0
                    ? "none, so nothing here could have moved it."
                    : string.Join(
                        ", ",
                        scrollers.Select(s =>
                            $"offset {s.Offset.Y:F0}, viewport {s.Viewport.Height:F0}, "
                            + $"extent {s.Extent.Height:F0}"))));

        window.MouseMove(centre.Value, RawInputModifiers.None);
        window.MouseDown(centre.Value, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(centre.Value, MouseButton.Left, RawInputModifiers.None);
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
    private static string DescribeChainAt(ShellHost host, Control control, Point point)
    {
        if (RootOf(host, control).InputHitTest(point) is not Visual hit)
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
        var window = RootOf(host, control);
        var beside = BesidePoint(host, control);
        window.MouseMove(beside, RawInputModifiers.None);
        window.MouseDown(beside, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(beside, MouseButton.Left, RawInputModifiers.None);
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
    /// <para>
    /// A selectable row counts as occupied too, and that one cost a measurement on 2026-08-16. The
    /// review inbox puts its Accept button directly under a list of candidate cards, so the control
    /// click landed on the last card and <em>selected</em> it — and Accept then decided that
    /// candidate rather than the one the walk had clicked. The scene still went green on "one
    /// candidate is now accepted"; it was only asking <b>which</b> that caught it. A card is not a
    /// <c>Button</c>, so listing the command types alone was not enough.
    /// </para>
    /// </remarks>
    private static Point BesidePoint(ShellHost host, Control control)
    {
        var window = RootOf(host, control);
        Reveal(host, control);

        // Every number below comes from geometry: the centre, the rectangles of the controls that
        // could take the click, and the offsets between them. A press that changed which controls
        // sit in a row leaves all three describing the row as it was, so the point chosen to be
        // "beside" everything lands on a neighbour that has since moved under it.
        //
        // The settling that answers that is in Reveal, called on the line above, and it got there by
        // measurement rather than by reasoning — see the comment there. This function carried its own
        // copy from 2026-09-01 until 2026-09-02, and over 250 beside-clicks of a full walk that copy
        // moved geometry in five of them; with Reveal forcing the pass it moves it in none.
        var centre = control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
            window);
        Assert.True(centre.HasValue, $"{Describe(control)} has no position in the window.");

        // The controls that could take the click are the ones in the same window, which is not the
        // same set as the shell's once a second window is on screen.
        var occupied = window.GetVisualDescendants()
            .OfType<Control>()
            .Where(candidate => candidate is Button or ComboBox or Slider or ListBoxItem && candidate.IsEffectivelyVisible)
            .Select(candidate => candidate.TranslatePoint(default, window) is { } origin
                ? new Rect(origin, candidate.Bounds.Size)
                : (Rect?)null)
            .OfType<Rect>()
            .ToArray();

        var step = new Vector(Math.Max(control.Bounds.Width, 8), Math.Max(control.Bounds.Height, 8));
        var refused = new List<string>();
        foreach (var offset in new[]
        {
            new Vector(0, -step.Y), new Vector(0, step.Y),
            new Vector(-step.X, 0), new Vector(step.X, 0),
            new Vector(0, -step.Y * 2), new Vector(0, step.Y * 2),
            new Vector(-step.X, -step.Y), new Vector(step.X, step.Y),
            new Vector(-step.X / 2, -step.Y), new Vector(-step.X / 2, step.Y),
        })
        {
            var candidate = centre!.Value + offset;
            if (candidate.X < 0
                || candidate.Y < 0
                || candidate.X >= window.Bounds.Width
                || candidate.Y >= window.Bounds.Height)
            {
                refused.Add($"{candidate} is outside the window");
                continue;
            }

            if (occupied.FirstOrDefault(rect => rect.Contains(candidate)) is { Width: > 0 } taken)
            {
                refused.Add($"{candidate} is inside {taken}");
                continue;
            }

            return candidate;
        }

        // What was tried and what took it, because "surrounded" on its own leaves the next person to
        // re-measure a layout the harness had already measured.
        Assert.Fail(
            $"{Describe(control)} at {centre} sized {control.Bounds.Size} is surrounded by other "
                + "command controls, so there is nowhere to put the click that proves the press did "
                + $"the work. Tried: {string.Join("; ", refused)}.");
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

        // Which view this control belongs to is settled now, while it is still on the tree: a control
        // that removes its own row by working — the inbox's Confirm is one — has no view left to be
        // recorded against by the time the effect arrives.
        var view = WalkLedger.ViewOf(control);
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
        var waits = 0;
        while (attempts < 8 && EqualityComparer<T>.Default.Equals(await probe(), before))
        {
            await SettleAsync();
            if (!EqualityComparer<T>.Default.Equals(await probe(), before))
            {
                break;
            }

            // Whether to wait, press again, or stop pressing lives in WalkPressPolicy, where the two
            // reasons not to repeat a press can be measured without a slow runner: a control whose
            // own work is in flight is correctly disabled, and a control that removes itself by
            // working — the version-switch answers close the question they answer — is simply gone.
            var step = WalkPressPolicy.Next(
                control.IsEffectivelyVisible,
                control.IsEffectivelyEnabled,
                waits);
            if (step == PressStep.Wait)
            {
                waits++;
                continue;
            }

            if (step == PressStep.StopPressing)
            {
                break;
            }

            pressed = Click(host, control);
            attempts++;
        }

        await WaitForAsync(
            async () => !EqualityComparer<T>.Default.Equals(await probe(), before),
            $"{complaint}. {attempts} presses, the last at {pressed}, where a click reaches "
                + $"{DescribeChainAt(host, control, pressed)}.");
        WalkLedger.Record(view, recordAs ?? anchor);
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
    private static Task WaitForAsync(Func<Task<bool>> condition, string complaint) =>
        WaitForAsync(condition, () => complaint);

    /// <summary>
    /// The same wait, with the complaint written when it is needed instead of before.
    /// </summary>
    /// <remarks>
    /// A condition of the shape <c>Player?.Player.HasFailed == true</c> reads false in two very
    /// different situations — the session opened and did not fail, and there is no session at all —
    /// and a complaint fixed in advance can only describe one of them. It described the first while
    /// the second is what a null hands back, so a red said "the file opened" about a run where
    /// nothing ever opened. Deferring the text lets the probe look at what it found.
    /// </remarks>
    private static async Task WaitForAsync(Func<Task<bool>> condition, Func<string> complaint)
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

        Assert.Fail(complaint());
    }

    /// <summary>
    /// Why a session that was expected to fail has not, told apart from never having existed.
    /// </summary>
    /// <remarks>
    /// Written for the one scene that has gone intermittently red twice — 2026-08-19, in the full
    /// suite both times, passing alone both times — where the sixty-second deadline rules slowness
    /// out and the old wording ruled nothing in.
    /// </remarks>
    private static string WhyItHasNotFailedYet(PlayerSurfaces? surfaces, string opened) =>
        surfaces is null
            ? "no player session reached the screen at all, so there was never anything that could "
                + "fail. That is not the same as the file opening, and it is what a null Player "
                + "hands back to a condition written with ?. — the two readings this message used "
                + "to run together."
            : $"{opened} — idle={surfaces.Player.IsIdle} opening={surfaces.Player.IsOpening} "
                + $"playing={surfaces.Player.IsPlaying} stopped={surfaces.Player.IsStopped} "
                + $"failed={surfaces.Player.HasFailed} path={surfaces.Player.MediaPath}";

    /// <summary>
    /// The courses destination, pressed with the mouse (CRS-001..CRS-005): marking a folder, opening
    /// a course, carrying on with its thread, and marking one lesson watched by hand.
    /// </summary>
    /// <remarks>
    /// The mark is the one press whose effect is a row rather than a surface, and it is the one that
    /// matters most here: it is what says a lesson's progress is PLY-008's progress. It is asserted
    /// on the watch state the store holds, not on the glyph, because a glyph would prove the row
    /// redrew itself and nothing about what was written down.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_courses_destination_marks_a_folder_and_carries_on_with_a_course()
    {
        var mediaRoot = Path.Combine(_dataRoot, "cursos");
        Directory.CreateDirectory(Path.Combine(mediaRoot, "Composicion", "01 - Modulo uno"));

        // A second course folder beside the first, so pointing at one leaves exactly one neighbour
        // for «Hemos encontrado {0} carpetas más. ¿Son todas cursos?» to ask about.
        Directory.CreateDirectory(Path.Combine(mediaRoot, "Modelado"));
        File.WriteAllBytes(Path.Combine(mediaRoot, "Modelado", "01 - Intro.mp4"), [0]);
        var factory = await SeedRootAsync(mediaRoot, ScanPolicy.Manual);

        var first = Path.Combine(mediaRoot, "Composicion", "01 - Modulo uno", "01 - Intro.mp4");
        var second = Path.Combine(mediaRoot, "Composicion", "01 - Modulo uno", "02 - El nodo.mp4");
        File.WriteAllBytes(first, [0]);
        File.WriteAllBytes(second, [0]);
        var firstFile = await SeedMediaFileAsync(factory, mediaRoot, first, TimeSpan.FromMinutes(10));
        var secondFile = await SeedMediaFileAsync(factory, mediaRoot, second, TimeSpan.FromMinutes(10));

        var roots = await new LibraryRootRepository(factory).ListAsync(TestContext.Current.CancellationToken);
        var rootId = roots.Single(candidate => candidate.Path == mediaRoot).Id;
        var courseId = new CourseId(Guid.NewGuid());
        var firstLesson = new LessonId(Guid.NewGuid());
        await new CourseRepository(factory).SaveAsync(
            new Course(courseId, rootId, "Composicion", "Composicion", DateTimeOffset.UnixEpoch, null),
            [
                new Lesson(
                    firstLesson,
                    CourseId: default,
                    new MediaFileId(firstFile),
                    "Modulo uno",
                    new LessonOrdinal(1, null),
                    new LessonOrdinal(1, null),
                    "01 - Intro",
                    "Intro",
                    "Composicion/01 - Modulo uno/01 - Intro.mp4"),
                new Lesson(
                    new LessonId(Guid.NewGuid()),
                    CourseId: default,
                    new MediaFileId(secondFile),
                    "Modulo uno",
                    new LessonOrdinal(1, null),
                    new LessonOrdinal(2, null),
                    "02 - El nodo",
                    "El nodo",
                    "Composicion/01 - Modulo uno/02 - El nodo.mp4"),
            ],
            TestContext.Current.CancellationToken);

        using var host = ShowShell(height: 1600);
        Navigate(host, AppRoute.Courses);
        var grid = host.ViewModel.Courses;
        Assert.NotNull(grid);
        await grid!.LoadAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        // Marking a folder opens the one door the shell already owns, and the scrim closes it again.
        await PressAsync(
            host,
            "CoursesMarkFolderAction",
            () => host.ViewModel.IsAddingRoot,
            "clicking Marcar una carpeta como curso never opened the add-media dialog");
        Assert.True(host.ViewModel.IsAddingRoot);
        await PressAsync(
            host,
            "AddRootCancelAction",
            () => host.ViewModel.IsAddingRoot,
            "clicking Cancelar never put the add-media dialog away");
        Assert.False(host.ViewModel.IsAddingRoot);

        // The card's second button opens the course under the grid without starting anything.
        var card = Assert.Single(grid.Cards);
        await PressAsync(
            host,
            card.AccessibleName,
            () => host.ViewModel.HasCourseDetails,
            "clicking a course card never opened the course under the grid",
            recordAs: "{Binding AccessibleName}");
        Assert.True(host.ViewModel.HasCourseDetails);
        var details = host.ViewModel.CourseDetails;
        Assert.NotNull(details);
        Dispatcher.UIThread.RunJobs();

        // Marking a lesson watched by hand, asserted on the store rather than on the glyph: what
        // this press claims is that a lesson's progress is the progress PLY-008 already keeps.
        var watchStates = new WatchStateRepository(factory);
        var key = CourseProgressKey.For(courseId, firstLesson);
        var row = details!.Modules[0].Lessons[0];
        await PressAsync(
            host,
            row.MarkAccessibleName,
            async () => (await watchStates.GetAsync(key, TestContext.Current.CancellationToken))?.Status,
            "clicking Marcar como vista never wrote the lesson down as watched",
            recordAs: "{Binding MarkAccessibleName}");
        var stored = await watchStates.GetAsync(key, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(WatchStatus.Watched, stored!.Status);
        Assert.True(stored.IsManualOverride);

        // The thread's own button, and one lesson's play button: both open a session, which is the
        // effect that proves them. The thread moved to the second lesson when the first was marked,
        // so this is also the assertion that the thread is read and not remembered.
        Dispatcher.UIThread.RunJobs();
        await PressAsync(
            host,
            details.ThreadActionText,
            () => host.ViewModel.IsPlayerVisible,
            "clicking Retomar el hilo never opened the lesson it points at",
            recordAs: "{Binding ThreadActionText}");
        Assert.True(host.ViewModel.IsPlayerVisible);
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);

        Navigate(host, AppRoute.Courses);
        Dispatcher.UIThread.RunJobs();
        var playable = details.Modules[0].Lessons[1];
        await PressAsync(
            host,
            playable.AccessibleName,
            () => host.ViewModel.IsPlayerVisible,
            "clicking a lesson never opened it",
            recordAs: "{Binding AccessibleName}");
        Assert.True(host.ViewModel.IsPlayerVisible);
        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);

        // And the card's first button, which is the one that carries straight on: it opens the
        // course and starts the thread in one press.
        Navigate(host, AppRoute.Courses);
        await grid.LoadAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        await PressAsync(
            host,
            grid.Cards[0].ActionText,
            () => host.ViewModel.IsPlayerVisible,
            "clicking Continuar on a course card never started the lesson the thread points at",
            recordAs: "{Binding ActionText}");
        Assert.True(host.ViewModel.IsPlayerVisible);

        // The player's «Lecciones» panel (CRS-004), which only exists because the session that just
        // opened is a lesson. Two things are asserted that no unit test can see: that the pill is
        // there at all — the shell asked the catalogue whether this file is a lesson and got an
        // answer — and that a row inside the column is reachable with a real mouse.
        Assert.True(host.ViewModel.HasLessonsPanel);
        await OpenPlayerPanelAsync(host, PlayerPanel.Lessons);
        var lessonsPanel = host.ViewModel.Player!.Lessons;
        Assert.NotNull(lessonsPanel);
        var playing = lessonsPanel!.LessonId;

        // The row that is not the one playing, because pressing the current one would open the same
        // lesson and prove nothing about the press landing.
        var otherRow = lessonsPanel.Modules
            .SelectMany(module => module.Lessons)
            .First(row => row.Id != playing);
        await PressAsync(
            host,
            otherRow.AccessibleName,
            () => host.ViewModel.Player?.Lessons?.LessonId,
            "clicking a row in the player's Lecciones panel never moved the session to that lesson",
            recordAs: "{Binding AccessibleName}");
        Assert.Equal(otherRow.Id, host.ViewModel.Player?.Lessons?.LessonId);

        await host.ViewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);

        // And the gesture that makes any of the above possible (CRS-001, ADR-0006 amendment 1): the
        // dialog's course half, the folder pointed at, and the neighbours answered for. Left until
        // last because it writes courses, and everything above counts them.
        Navigate(host, AppRoute.Courses);
        await PressAsync(
            host,
            "CoursesMarkFolderAction",
            () => host.ViewModel.IsAddingRoot,
            "reopening the add-media dialog for the course half never worked");
        var marking = host.ViewModel.MarkCourse;
        Assert.NotNull(marking);

        await PressAsync(
            host,
            "AddAsCourseOption",
            () => marking!.IsCourse,
            "clicking Curso (carpeta de lecciones) never put the dialog on its course half");
        Assert.True(marking!.IsCourse);
        Assert.Equal("AddCourseTitle", marking.TitleKey);

        // The path goes in the one box the dialog has, the same box the root half types into.
        var onboardingForm = host.ViewModel.Onboarding;
        Assert.NotNull(onboardingForm);
        onboardingForm!.Path = Path.Combine(mediaRoot, "Composicion");
        Dispatcher.UIThread.RunJobs();

        // The probe is what the pass came back with, and NOT the number of courses: this folder is
        // already a course here, so marking it again is an upsert and the count is identical before
        // and after. A probe that cannot move is a press that proves nothing.
        var courses = new CourseRepository(factory);
        await PressAsync(
            host,
            marking.ConfirmKey,
            () => marking.MarkedTitle,
            "clicking Marcar como curso never came back with the course it marked",
            recordAs: DialogAction);
        Assert.False(marking.HasFailure, $"marking answered {marking.FailureKey}");
        Assert.Equal("Composicion", marking.MarkedTitle);

        // One neighbour at the derived depth, asked about rather than claimed.
        Assert.Equal(1, marking.NeighbourCount);
        Assert.True(marking.IsAskingAboutNeighbours);
        Dispatcher.UIThread.RunJobs();
        await PressAsync(
            host,
            "AddCourseNeighboursConfirmAction",
            async () => (await courses.ListAsync(TestContext.Current.CancellationToken)).Count,
            "answering that the neighbours are courses never marked them");
        Assert.False(marking.IsAskingAboutNeighbours);
        Assert.Equal(2, (await courses.ListAsync(TestContext.Current.CancellationToken)).Count);

        // Marking again with nothing new leaves the question up, and «Sólo esta» is the way out of
        // it: the one answer that has to change nothing.
        onboardingForm.Path = Path.Combine(mediaRoot, "Composicion");
        Dispatcher.UIThread.RunJobs();
        await PressAsync(
            host,
            marking.ConfirmKey,
            () => marking.NeighbourCount,
            "marking the same folder again never asked about its neighbour",
            recordAs: DialogAction);
        Assert.True(marking.IsAskingAboutNeighbours);
        await PressAsync(
            host,
            "AddCourseNeighboursDeclineAction",
            () => marking.IsAskingAboutNeighbours,
            "answering «Sólo esta» never put the question away");
        Assert.False(marking.IsAskingAboutNeighbours);
        Assert.Equal(2, (await courses.ListAsync(TestContext.Current.CancellationToken)).Count);

        // Back to the root half, which is where the dialog opens.
        await PressAsync(
            host,
            "AddAsRootOption",
            () => marking.IsCourse,
            "clicking Raíz de medios never put the dialog back on its root half");
        Assert.False(marking.IsCourse);
        await PressAsync(
            host,
            "AddRootCancelAction",
            () => host.ViewModel.IsAddingRoot,
            "clicking Cancelar never put the dialog away");
        Assert.False(host.ViewModel.IsAddingRoot);
    }

    /// <summary>
    /// How the add dialog's one action is recorded. Its accessible name is a binding, because what
    /// the button says follows the chosen half — «Añadir carpeta» or «Marcar como curso» — so the
    /// inventory in eng/check-walk-coverage.ps1 knows it by the binding it is declared with, the way
    /// it already knows the two controls named by their own data.
    /// </summary>
    private const string DialogAction =
        "{Binding MarkCourse.ConfirmKey, Converter={StaticResource DialogResourceKey}}";

    private static void Navigate(ShellHost host, AppRoute route)
    {
        host.ViewModel.NavigateCommand.Execute(route);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The catalogue as a first run finds it: migrated, and holding nothing.</summary>
    private async Task<SqliteConnectionFactory> MigrateCatalogueAsync()
    {
        Directory.CreateDirectory(_dataRoot);
        var factory = new SqliteConnectionFactory(new AppDataPaths(_dataRoot).DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        return factory;
    }

    private async Task<SqliteConnectionFactory> SeedRootAsync(string mediaRoot, ScanPolicy policy)
    {
        var factory = await MigrateCatalogueAsync();
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
    /// <param name="secondSeasonFile">
    /// Given, the series gets a second season with one episode in it. Only the scene that presses the
    /// season picker asks for one: with a single season the picker is absent by design, so a seed that
    /// always had two would hide the state the rest of the scenes are in.
    /// </param>
    private static async Task<Guid> SeedSeriesAsync(
        SqliteConnectionFactory factory,
        Guid firstFile,
        Guid secondFile,
        Guid? secondSeasonFile = null)
    {
        var showId = Guid.NewGuid();
        var firstEpisode = Guid.NewGuid();
        var secondEpisode = Guid.NewGuid();
        var seasonTwoEpisode = Guid.NewGuid();
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

        if (secondSeasonFile is { } seasonTwoFile)
        {
            await using var second = connection.CreateCommand();
            second.CommandText = """
                INSERT INTO seasons (show_id, season_number, title) VALUES ($show, 2, 'T2');
                INSERT INTO episodes (id, show_id, season_number, episode_number, absolute_number,
                                      title, sort_order, is_available)
                VALUES ($e3, $show, 2, 1, 3, 'E3', 3, 1);
                INSERT INTO episode_media (episode_id, media_file_id) VALUES ($e3, $f3);
                """;
            second.Parameters.AddWithValue("$show", showId.ToString("D"));
            second.Parameters.AddWithValue("$e3", seasonTwoEpisode.ToString("D"));
            second.Parameters.AddWithValue("$f3", seasonTwoFile.ToString("D"));
            await second.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

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

    /// <param name="activationPath">
    /// The file Explorer handed over, for the surface that only exists because of one. It is set
    /// before the shell is created because that is when the startup reads it — and the window has to
    /// be configured afterwards, because <c>ConfigureWindow</c> is where the activation is read and
    /// nowhere else.
    /// </param>
    private ShellHost ShowShell(int height = 1000, string? activationPath = null)
    {
        var (application, window, settled) = Mount(height, activationPath);

        // ARQ-005: the shell arrives after the database is ready, not with it, so the walk waits
        // for it. The wait names what stood in its place if it never comes.
        var shell = Assert.IsType<ShellView>(settled);
        return new ShellHost(
            application,
            window,
            shell,
            Assert.IsType<ShellViewModel>(shell.DataContext),
            _teardownFailures.Add);
    }

    /// <summary>
    /// The screen the shell cannot lead to: the one the startup puts up instead of the shell when
    /// the database refuses to open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is mounted the same way and by the same call — <c>CreateShell</c> decides between the two
    /// itself, and which one it decided is read rather than asked for. The seeding is the scene's,
    /// because what makes this screen appear is a database on disk that cannot be used.
    /// </para>
    /// <para>
    /// The host is the same record with no shell view model in it, which is what it costs: five uses
    /// of <c>host.Shell</c> only ever walk the visual tree, so the type widens to <c>Control</c>, and
    /// the sixty-seven uses of <c>host.ViewModel</c> keep the property they always had.
    /// </para>
    /// </remarks>
    private ShellHost ShowRecovery(int height = 1000)
    {
        var (application, window, settled) = Mount(height);

        var recovery = Assert.IsType<DatabaseRecoveryView>(settled);
        return new ShellHost(application, window, recovery, null, _teardownFailures.Add);
    }

    /// <summary>
    /// Builds the application, shows what it hands the window, and waits for the startup to settle.
    /// </summary>
    private (ApplicationHost Application, Window Window, Control Settled) Mount(
        int height,
        string? activationPath = null)
    {
        Assert.NotNull(Avalonia.Application.Current);
        ApSolutions.LocalMedia.Presentation.App.ApplyLanguage(
            Avalonia.Application.Current,
            CultureInfo.GetCultureInfo("es-ES"));
        Directory.CreateDirectory(_dataRoot);
        var application = ApplicationHost.Create(new AppDataPaths(_dataRoot));
        application.PendingActivationPath = activationPath;

        // The window shows what the application put in it — the container — rather than the shell
        // lifted out of it. Lifting it out left the whole tree attached to a container that was never
        // shown, and a Button only evaluates its Command once it is on the logical tree: every
        // command-bound button in the shell reported itself disabled, so no click could reach one.
        // Buttons wired with Click= were unaffected, which is why nothing noticed until this walk
        // tried to press one.
        var created = application.CreateShell();
        var window = new Window { Width = 1600, Height = height, Content = created };
        window.Show();

        var settled = AssembledStartup.FinalContent(created);
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
        return (application, window, settled);
    }

    private sealed record ShellHost(
        ApplicationHost Application,
        Window Window,
        Control Shell,
        ShellViewModel? ShellModel,
        Action<Exception> ReportTeardownFailure) : IDisposable
    {
        /// <summary>
        /// The shell's view model, for the scenes that have a shell at all.
        /// </summary>
        public ShellViewModel ViewModel => ShellModel ?? throw new InvalidOperationException(
            "This scene mounted the recovery screen, which stands in the shell's place and has no "
                + "shell view model to ask.");

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
        await EncodeAsync(
            encoder!,
            $"-hide_banner -loglevel error -nostdin -y " +
                $"-f lavfi -i testsrc2=size=320x240:rate=15:duration={durationSeconds} " +
                $"-f lavfi -i sine=frequency=440:duration={durationSeconds} " +
                $"-c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac -b:a 64k -shortest " +
                $"\"{destination}\"");
        return destination;
    }

    /// <summary>
    /// One sample with more than one of what a person can choose between: two audio tracks in two
    /// languages, and a subtitle track.
    /// </summary>
    /// <remarks>
    /// The track surface exists to choose among tracks, so a sample with one of each would put an
    /// empty list on screen and a press on it would prove nothing about the list. The languages are
    /// declared as metadata because that is what the surface shows: a track describes itself by its
    /// language and its channels, never by its position.
    /// </remarks>
    private static async Task<string> RequireMultiTrackSampleAsync(string name, int durationSeconds)
    {
        var encoder = FindEncoder();
        Assert.SkipWhen(
            encoder is null,
            "ffmpeg was not found. Set FFMPEG_PATH or install ffmpeg to generate the walk's media.");
        var stem = string.Create(
            CultureInfo.InvariantCulture,
            $"{Path.GetFileNameWithoutExtension(name)}-{durationSeconds}s");
        var directory = Path.Combine(RepositoryLayout.Root, "artifacts", "test-media", "walk");
        var destination = Path.Combine(directory, $"{stem}{Path.GetExtension(name)}");
        if (File.Exists(destination) && new FileInfo(destination).Length > 0)
        {
            return destination;
        }

        Directory.CreateDirectory(directory);
        var subtitles = Path.Combine(directory, $"{stem}.srt");
        await File.WriteAllTextAsync(
            subtitles,
            $"1{Environment.NewLine}00:00:00,000 --> 00:00:0{Math.Min(durationSeconds, 9)},000"
                + $"{Environment.NewLine}A line, so the track carries something.{Environment.NewLine}",
            TestContext.Current.CancellationToken);

        await EncodeAsync(
            encoder!,
            $"-hide_banner -loglevel error -nostdin -y " +
                $"-f lavfi -i testsrc2=size=320x240:rate=15:duration={durationSeconds} " +
                $"-f lavfi -i sine=frequency=440:duration={durationSeconds} " +
                $"-f lavfi -i sine=frequency=880:duration={durationSeconds} " +
                $"-i \"{subtitles}\" " +
                $"-map 0:v -map 1:a -map 2:a -map 3:s " +
                $"-c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac -b:a 64k -c:s srt " +
                $"-metadata:s:a:0 language=eng -metadata:s:a:1 language=spa " +
                $"-metadata:s:s:0 language=eng -shortest " +
                $"\"{destination}\"");
        return destination;
    }

    private static async Task EncodeAsync(string encoder, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(encoder, arguments)
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
