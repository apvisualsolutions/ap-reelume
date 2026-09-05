// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Metadata;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Review;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// The shell as an assembly rather than as a screen: what it was handed, what it asks for, and when
/// it says the answer changed. Every surface here already had its own suite; none of them had anyone
/// to ask for it.
/// </summary>
public sealed class ShellAssemblyTests
{
    private static readonly TitleId Title = new(new Guid("11111111-1111-1111-1111-111111111111"));
    private static readonly MediaFileId MediaFile = new(Title.Value);

    [AvaloniaFact]
    public void The_long_lived_surfaces_arrive_built_and_the_shell_says_it_has_them()
    {
        var shell = new ShellViewModel(new NavigationService(), FullSurfaces());

        Assert.True(shell.HasOnboarding);
        Assert.True(shell.HasReviewInbox);
        Assert.True(shell.HasScanSettings);
        Assert.True(shell.HasShortcuts);
        Assert.True(shell.HasSubtitleStyle);
        Assert.NotNull(shell.Onboarding);
        Assert.NotNull(shell.ReviewInbox);
        Assert.NotNull(shell.ScanSettings);
        Assert.NotNull(shell.Shortcuts);
        Assert.NotNull(shell.SubtitleStyle);
    }

    /// <summary>
    /// The add dialog's two halves share one path box, so they also have to share the kind the root
    /// half detects from it (CRS-001).
    /// </summary>
    /// <remarks>
    /// Reading it once when the shell is built would freeze it at <c>Local</c>, which is what every
    /// path that is not a fixed drive would then be catalogued as. And opening the dialog has to
    /// clear both halves, or it opens on the last course's notice and on a question about the
    /// neighbours of a folder nobody is looking at any more.
    /// </remarks>
    [AvaloniaFact]
    public void Opening_the_add_dialog_clears_both_halves_and_the_kind_follows_the_path()
    {
        var surfaces = FullSurfaces();
        var shell = new ShellViewModel(new NavigationService(), surfaces);

        Assert.True(shell.HasMarkCourse);
        Assert.NotNull(shell.MarkCourse);
        Assert.Equal(RootKind.Local, shell.MarkCourse!.Kind);

        shell.Onboarding!.SelectKindCommand.Execute(RootKind.Unc);
        Assert.Equal(RootKind.Unc, shell.MarkCourse.Kind);

        shell.MarkCourse.SelectKindCommand.Execute("course");
        Assert.True(shell.MarkCourse.IsCourse);

        shell.BeginAddMedia();
        Assert.True(shell.IsAddingRoot);
        Assert.False(shell.MarkCourse.IsCourse);
    }

    /// <summary>A composition with no course store leaves the dialog its root half and nothing else.</summary>
    [AvaloniaFact]
    public void A_shell_with_no_course_half_says_so_rather_than_offering_one()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());

        Assert.False(shell.HasMarkCourse);
        Assert.Null(shell.MarkCourse);

        // And opening the dialog with neither half is not a crash: it is a dialog with nothing in it.
        shell.BeginAddMedia();
        Assert.True(shell.IsAddingRoot);
    }

    [AvaloniaFact]
    public void A_shell_that_was_handed_nothing_still_runs()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());

        Assert.False(shell.HasOnboarding);
        Assert.False(shell.HasReviewInbox);
        Assert.False(shell.IsReviewVisible);
        Assert.False(shell.IsPlayerVisible);
        Assert.True(shell.IsPrimaryContentVisible);
    }

    [AvaloniaFact]
    public void The_review_route_shows_the_inbox_instead_of_the_welcome_card()
    {
        var navigation = new NavigationService();
        var shell = new ShellViewModel(navigation, FullSurfaces());

        navigation.Navigate(AppRoute.Review);

        Assert.True(shell.IsReviewVisible);
        Assert.False(shell.IsPrimaryContentVisible);
    }

    /// <summary>
    /// The route the service is born on never raises Navigated, so nobody else will ever feed the
    /// surface it shows: a Home that waits for a navigation that already happened starts empty and
    /// stays empty until somebody leaves and comes back.
    /// </summary>
    [AvaloniaFact]
    public void The_route_the_shell_is_born_on_feeds_its_surface_without_a_navigation()
    {
        var home = new HomeViewModel(new GetHome(new StubHomeReadModel(
        [
            new HomeProgressEntry(
                ContentKey.ForTitle(Title),
                Title,
                CatalogTitleKind.Movie,
                "Arrival",
                SeasonNumber: null,
                EpisodeNumber: null,
                EpisodeTitle: null,
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(90),
                WatchStatus.InProgress,
                IsAvailable: true,
                DateTimeOffset.UnixEpoch),
        ])));

        _ = new ShellViewModel(new NavigationService(), FullSurfaces() with { Home = home });

        Assert.True(home.HasInProgress);
        Assert.True(home.HasResume);
    }

    /// <summary>
    /// A button bound to a command asks once and waits to be told. Choosing a title is what makes the
    /// three card actions possible, so choosing one has to raise the event.
    /// </summary>
    [AvaloniaFact]
    public void Opening_a_title_tells_the_card_actions_they_became_possible()
    {
        var library = BuildLibrary();
        var shell = new ShellViewModel(new NavigationService(), FullSurfaces() with { Library = library });
        var notifications = 0;
        shell.EditMetadataCommand.CanExecuteChanged += (_, _) => notifications++;
        Assert.False(shell.EditMetadataCommand.CanExecute(null));

        library.OpenDetails(library.Items[0]);

        Assert.True(shell.EditMetadataCommand.CanExecute(null));
        Assert.True(shell.PreviewRenameCommand.CanExecute(null));
        Assert.True(shell.ReviewDuplicatesCommand.CanExecute(null));
        Assert.True(notifications > 0);
    }

    [AvaloniaFact]
    public async Task Asking_for_the_metadata_editor_puts_it_on_the_shell()
    {
        var library = BuildLibrary();
        var shell = new ShellViewModel(new NavigationService(), FullSurfaces() with { Library = library });
        library.OpenDetails(library.Items[0]);
        Assert.False(shell.HasMetadataEditor);

        await shell.OpenMetadataEditorAsync(TestContext.Current.CancellationToken);

        Assert.True(shell.HasMetadataEditor);
        Assert.NotNull(shell.MetadataEditor);
    }

    [AvaloniaFact]
    public async Task Asking_for_a_rename_preview_puts_it_on_the_shell_and_renames_nothing()
    {
        var library = BuildLibrary();
        var shell = new ShellViewModel(new NavigationService(), FullSurfaces() with { Library = library });
        library.OpenDetails(library.Items[0]);

        await shell.OpenRenamePreviewAsync(TestContext.Current.CancellationToken);

        Assert.True(shell.HasRename);
        Assert.NotNull(shell.Rename);
        Assert.False(shell.Rename!.IsConfirmed);
    }

    /// <summary>
    /// Comparing two copies lands on the duplicates destination — the rail's own door since
    /// 2026-08-23 — whichever of the two ways in was used.
    /// </summary>
    [AvaloniaFact]
    public async Task Asking_for_the_versions_of_a_title_moves_to_the_duplicates_destination()
    {
        var navigation = new NavigationService();
        var library = BuildLibrary();
        var shell = new ShellViewModel(navigation, FullSurfaces() with { Library = library });
        library.OpenDetails(library.Items[0]);

        await shell.OpenDuplicatesAsync(TestContext.Current.CancellationToken);

        Assert.True(shell.HasDuplicates);
        Assert.Equal(AppRoute.Duplicates, navigation.CurrentRoute);
    }

    [AvaloniaFact]
    public async Task A_title_with_a_single_version_produces_no_comparison_and_stays_where_it_is()
    {
        var navigation = new NavigationService();
        var library = BuildLibrary();
        var shell = new ShellViewModel(
            navigation,
            FullSurfaces() with
            {
                Library = library,
                OpenDuplicates = (_, _) => Task.FromResult<DuplicateReviewViewModel?>(null),
            });
        library.OpenDetails(library.Items[0]);

        await shell.OpenDuplicatesAsync(TestContext.Current.CancellationToken);

        Assert.False(shell.HasDuplicates);
        Assert.Equal(AppRoute.Home, navigation.CurrentRoute);
    }

    [AvaloniaFact]
    public async Task Playing_from_a_card_shows_the_session_and_everything_it_carries()
    {
        var shell = new ShellViewModel(new NavigationService(), FullSurfaces());
        Assert.False(shell.IsPlayerVisible);

        await shell.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        Assert.True(shell.IsPlayerVisible);
        Assert.True(shell.HasTracks);
        Assert.True(shell.HasAudioOutput);
        Assert.True(shell.HasMarkers);
        Assert.True(shell.HasResumePrompt);
        Assert.False(shell.IsPrimaryContentVisible);
    }

    [AvaloniaFact]
    public async Task Closing_the_session_stops_the_media_and_returns_to_the_embedded_mode()
    {
        var stops = 0;
        var shell = new ShellViewModel(
            new NavigationService(),
            FullSurfaces() with
            {
                ClosePlayer = _ =>
                {
                    stops++;
                    return Task.CompletedTask;
                },
            });
        await shell.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        await shell.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackMode.Mini, shell.PlaybackMode);

        await shell.ClosePlayerAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, stops);
        Assert.False(shell.IsPlayerVisible);
        Assert.Equal(PlaybackMode.Embedded, shell.PlaybackMode);
    }

    [AvaloniaFact]
    public async Task Asking_for_a_mode_twice_returns_to_the_embedded_one()
    {
        var shell = new ShellViewModel(new NavigationService(), FullSurfaces());
        await shell.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        await shell.TogglePlaybackModeAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackMode.Fullscreen, shell.PlaybackMode);

        await shell.TogglePlaybackModeAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackMode.Embedded, shell.PlaybackMode);
    }

    [AvaloniaFact]
    public async Task Without_a_session_a_mode_change_does_nothing()
    {
        var shell = new ShellViewModel(new NavigationService(), FullSurfaces());

        await shell.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);

        Assert.Equal(PlaybackMode.Embedded, shell.PlaybackMode);
        Assert.False(shell.ToggleMiniPlayerCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task A_shell_with_no_way_to_play_refuses_quietly_instead_of_failing()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());

        await shell.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        await shell.ClosePlayerAsync(TestContext.Current.CancellationToken);
        await shell.OpenMetadataEditorAsync(TestContext.Current.CancellationToken);
        await shell.OpenRenamePreviewAsync(TestContext.Current.CancellationToken);
        await shell.OpenDuplicatesAsync(TestContext.Current.CancellationToken);

        Assert.False(shell.IsPlayerVisible);
        Assert.False(shell.HasMetadataEditor);
        Assert.False(shell.HasRename);
        Assert.False(shell.HasDuplicates);
    }

    /// <summary>
    /// The commands the buttons are bound to do what the methods do. A command that only looks right
    /// is the defect the whole increment exists to stop.
    /// </summary>
    [AvaloniaFact]
    public async Task Every_command_a_button_is_bound_to_reaches_its_surface()
    {
        using var opened = new SemaphoreSlim(0, 6);
        var library = BuildLibrary();
        var shell = new ShellViewModel(
            new NavigationService(),
            FullSurfaces() with
            {
                Library = library,
                OpenMetadataEditor = (titleId, token) =>
                {
                    _ = opened.Release();
                    return Task.FromResult<MetadataEditorViewModel?>(BuildEditor(titleId));
                },
                OpenRename = (titleId, token) =>
                {
                    _ = opened.Release();
                    return Task.FromResult<RenamePreviewViewModel?>(BuildRename());
                },
                OpenDuplicates = (titleId, token) =>
                {
                    _ = opened.Release();
                    return Task.FromResult<DuplicateReviewViewModel?>(BuildDuplicates(titleId));
                },
                OpenPlayer = (request, token) =>
                {
                    _ = opened.Release();
                    return Task.FromResult<PlayerSurfaces?>(BuildPlayer());
                },
                ClosePlayer = token =>
                {
                    _ = opened.Release();
                    return Task.CompletedTask;
                },
                ChangePlaybackMode = (mode, token) =>
                {
                    _ = opened.Release();
                    return Task.FromResult(mode);
                },
            });
        library.OpenDetails(library.Items[0]);

        shell.EditMetadataCommand.Execute(null);
        await opened.WaitAsync(TestContext.Current.CancellationToken);
        shell.PreviewRenameCommand.Execute(null);
        await opened.WaitAsync(TestContext.Current.CancellationToken);
        shell.ReviewDuplicatesCommand.Execute(null);
        await opened.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(shell.HasMetadataEditor);
        Assert.True(shell.HasRename);
        Assert.True(shell.HasDuplicates);

        await shell.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        await opened.WaitAsync(TestContext.Current.CancellationToken);
        shell.ToggleMiniPlayerCommand.Execute(null);
        await opened.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackMode.Mini, shell.PlaybackMode);

        shell.ToggleFullscreenCommand.Execute(null);
        await opened.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(PlaybackMode.Fullscreen, shell.PlaybackMode);

        shell.ClosePlayerCommand.Execute(null);
        await opened.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(shell.IsPlayerVisible);
    }

    /// <summary>
    /// Consenting to the first scan has to start one. The surface asked the question and recorded the
    /// answer; until something acted on it, a new install added a folder and stayed empty forever.
    /// </summary>
    [AvaloniaFact]
    public async Task Consenting_to_the_first_scan_starts_it_and_reloads_the_library()
    {
        using var scanned = new SemaphoreSlim(0, 1);
        var scannedRoots = new List<LibraryRootId>();
        var roots = new RecordingRoots();
        var onboarding = new RootOnboardingViewModel(new AddLibraryRoot(roots, new StubNormalizer()))
        {
            Path = "R:\\media",
        };
        var library = BuildLibrary();
        var shell = new ShellViewModel(
            new NavigationService(),
            FullSurfaces() with
            {
                Library = library,
                Onboarding = onboarding,
                StartScan = (rootId, token) =>
                {
                    scannedRoots.Add(rootId);
                    _ = scanned.Release();
                    return Task.CompletedTask;
                },
            });

        await onboarding.AddAsync(TestContext.Current.CancellationToken);
        Assert.True(onboarding.InitialScanConsentRequired);
        Assert.Empty(scannedRoots);

        onboarding.GrantInitialScanConsent();
        await scanned.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal([roots.Added!.Id], scannedRoots);
        Assert.NotNull(shell.Library);
    }

    /// <summary>
    /// The rail's «Añadir medios» opens the add-root dialog over whichever route the shell is on
    /// <b>and</b> empties the form, which is the half that makes it more than a shortcut.
    /// </summary>
    /// <remarks>
    /// Asserted in C# rather than through the view, and that is on purpose: what the button pays for
    /// is three leftovers being cleared, and none of the three is a thing a screenshot shows. It is
    /// set up as the state somebody actually leaves behind — a folder added, so the path is still
    /// typed in and the catalogue would refuse it a second time. The shell stays on its route: the
    /// dialog floats, it does not navigate.
    /// </remarks>
    [AvaloniaFact]
    public async Task Add_media_opens_the_folder_surface_with_the_form_cleared()
    {
        var roots = new RecordingRoots();
        var onboarding = new RootOnboardingViewModel(new AddLibraryRoot(roots, new StubNormalizer()))
        {
            Path = "R:\\media",
        };
        var shell = new ShellViewModel(
            new NavigationService(),
            new ShellSurfaces { Onboarding = onboarding });

        // The state somebody is left in after adding one folder and then being refused a second.
        await onboarding.AddAsync(TestContext.Current.CancellationToken);
        onboarding.Path = "R:\\media";
        await onboarding.AddAsync(TestContext.Current.CancellationToken);
        Assert.Equal("RootAddDuplicate", onboarding.FailureKey);
        Assert.NotEqual(AppRoute.Library, shell.CurrentRoute);

        Assert.True(shell.AddMediaCommand.CanExecute(null));
        Assert.False(shell.IsAddingRoot);
        shell.AddMediaCommand.Execute(null);

        Assert.True(shell.IsAddingRoot);
        Assert.NotEqual(AppRoute.Library, shell.CurrentRoute);
        Assert.Equal(string.Empty, onboarding.Path);
        Assert.Null(onboarding.FailureKey);
        Assert.False(onboarding.HasFailure);
        Assert.Equal(RootKind.Local, onboarding.SelectedKind);

        // And the folder that was added is still in the catalogue: clearing the form is not undoing
        // the work, which is the one way this could have been read wrong.
        Assert.NotNull(roots.Added);
    }

    /// <summary>
    /// A half-answered removal does not survive into the next add, and the shell without the surface
    /// offers the button disabled rather than offering nothing.
    /// </summary>
    [AvaloniaFact]
    public async Task Add_media_calls_off_a_pending_removal_and_needs_the_surface_to_be_offered_at_all()
    {
        var roots = new RecordingRoots();
        var onboarding = new RootOnboardingViewModel(
            new AddLibraryRoot(roots, new StubNormalizer()),
            removeLibraryRoot: null,
            roots)
        {
            Path = "R:\\media",
        };
        var shell = new ShellViewModel(
            new NavigationService(),
            new ShellSurfaces { Onboarding = onboarding });

        await onboarding.AddAsync(TestContext.Current.CancellationToken);
        await onboarding.RefreshRootsAsync(TestContext.Current.CancellationToken);
        onboarding.RequestRemoveCommand.Execute(Assert.Single(onboarding.Roots));
        Assert.True(onboarding.IsConfirmingRemoval);

        shell.AddMediaCommand.Execute(null);

        Assert.False(onboarding.IsConfirmingRemoval);
        Assert.Equal(string.Empty, onboarding.PendingRemovalPath);

        // Without the surface there is nothing for the button to open, so it says so instead of
        // pressing into nothing.
        var bare = new ShellViewModel(new NavigationService(), new ShellSurfaces());
        Assert.False(bare.AddMediaCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task A_folder_added_with_no_way_to_scan_it_simply_waits()
    {
        var roots = new RecordingRoots();
        var onboarding = new RootOnboardingViewModel(new AddLibraryRoot(roots, new StubNormalizer()))
        {
            Path = "R:\\media",
        };
        var shell = new ShellViewModel(
            new NavigationService(),
            new ShellSurfaces { Onboarding = onboarding });

        await onboarding.AddAsync(TestContext.Current.CancellationToken);
        onboarding.GrantInitialScanConsent();

        Assert.True(onboarding.CanStartInitialScan);
        Assert.True(shell.HasOnboarding);
    }

    /// <summary>
    /// A file the library found but nobody identified opens on the single-title card, because that is
    /// the one with a way to play it. Sending it to the series card leaves a season list it does not
    /// have and no play action at all — which is what walking the real application found.
    /// </summary>
    [AvaloniaFact]
    public async Task An_unidentified_file_opens_on_the_card_that_can_play_it()
    {
        var library = new LibraryViewModel(new StubCatalog(CatalogTitleKind.Unidentified));
        await library.LoadAsync(TestContext.Current.CancellationToken);

        library.OpenDetails(library.Items[0]);

        Assert.True(library.IsMovieDetails);
        Assert.False(library.IsShowDetails);
    }

    [AvaloniaFact]
    public async Task A_series_still_opens_on_the_series_card()
    {
        var library = new LibraryViewModel(new StubCatalog(CatalogTitleKind.Show));
        await library.LoadAsync(TestContext.Current.CancellationToken);

        library.OpenDetails(library.Items[0]);

        Assert.True(library.IsShowDetails);
        Assert.False(library.IsMovieDetails);
    }

    [AvaloniaFact]
    public void A_command_that_cannot_run_does_nothing_when_it_is_pressed_anyway()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());

        shell.EditMetadataCommand.Execute(null);
        shell.ClosePlayerCommand.Execute(null);

        Assert.False(shell.HasMetadataEditor);
        Assert.False(shell.IsPlayerVisible);
    }

    /// <summary>
    /// Six since 2026-08-30, when Courses became a destination of its own (CRS-003). The list is
    /// asserted in order rather than by count: the rail is written by hand in the AXAML, and a route
    /// that entered the enum somewhere the rail does not draw it would still pass a count.
    /// </summary>
    [AvaloniaFact]
    public void The_shell_lists_the_six_approved_destinations_and_nothing_else()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());

        Assert.Equal(
            [
                AppRoute.Home,
                AppRoute.Library,
                AppRoute.Courses,
                AppRoute.Review,
                AppRoute.Duplicates,
                AppRoute.Settings,
            ],
            shell.Routes);
        Assert.False(shell.NavigateCommand.CanExecute("Library"));
        Assert.True(shell.NavigateCommand.CanExecute(AppRoute.Library));
    }

    [AvaloniaFact]
    public void The_old_constructors_still_describe_the_same_shell()
    {
        var navigation = new NavigationService();
        var shell = new ShellViewModel(navigation, appearanceSettings: null, library: BuildLibrary());

        Assert.NotNull(shell.Library);
        Assert.False(shell.HasOnboarding);
        Assert.Equal(AppRoute.Home, shell.CurrentRoute);
    }

    /// <summary>
    /// The same surfaces, for the suite that measures the editor page.
    /// </summary>
    /// <remarks>
    /// Shared rather than copied, because what the page is about is the two tools arriving the way
    /// the application hands them over — a second builder would drift from this one and then measure
    /// its own drift.
    /// </remarks>
    internal static ShellSurfaces EditorSurfaces() => FullSurfaces();

    private static ShellSurfaces FullSurfaces() => new()
    {
        Library = BuildLibrary(),
        Onboarding = new RootOnboardingViewModel(new AddLibraryRoot(new StubRoots(), new StubNormalizer())),
        MarkCourse = new MarkCourseViewModel(),
        ReviewInbox = new ReviewInboxViewModel(
            new GetReviewInbox(new StubCandidates()),
            new ResolveMatch(new StubCandidates(), new StubEvents(), SilentIdentification.Create()),
            new RejectMatch(new StubCandidates(), new StubEvents())),
        ScanSettings = new ScanSettingsViewModel(),
        Shortcuts = new ShortcutSettingsViewModel(new ShortcutMap()),
        SubtitleStyle = new SubtitleStyleViewModel(new StubPreferences()),
        OpenMetadataEditor = (titleId, _) => Task.FromResult<MetadataEditorViewModel?>(BuildEditor(titleId)),
        OpenRename = (_, _) => Task.FromResult<RenamePreviewViewModel?>(BuildRename()),
        OpenDuplicates = (titleId, _) => Task.FromResult<DuplicateReviewViewModel?>(BuildDuplicates(titleId)),
        OpenPlayer = (_, _) => Task.FromResult<PlayerSurfaces?>(BuildPlayer()),
        ClosePlayer = _ => Task.CompletedTask,
        ChangePlaybackMode = (mode, _) => Task.FromResult(mode),
    };

    private static LibraryViewModel BuildLibrary()
    {
        var library = new LibraryViewModel(new StubCatalog());
        library.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        return library;
    }

    private static MetadataEditorViewModel BuildEditor(TitleId titleId) => new(
        new CatalogMetadata(
            titleId,
            new EditableMetadata("Título", null, null, null, [], null, null, null, new HashSet<MetadataField>()),
            Revision: 0),
        new UpdateMetadata(new StubMetadata()),
        SilentIdentification.Refresh(new StubMetadata()),
        new ArtworkPickerViewModel());

    private static RenamePreviewViewModel BuildRename() => new(
        new PreviewRename(new RenamePolicy()).Execute(new PreviewRenameCommand(
            "R:\\media",
            [new RenameRequest("R:\\media\\a.mkv", "b.mkv")])),
        new ExecuteRename(new StubRenamer()),
        new UndoRename(new StubRenamer()));

    private static DuplicateReviewViewModel BuildDuplicates(TitleId titleId)
    {
        var group = new MediaVersionGroup(
            new MediaVersionId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            ContentKey.ForTitle(titleId).Value,
            [
                new MediaVersion(MediaFile, "R:\\media\\a.mkv", true, null, 3840, 2160, true, "HEVC", 90),
                new MediaVersion(
                    new MediaFileId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                    "R:\\media\\b.mkv",
                    true,
                    null,
                    1920,
                    1080,
                    false,
                    "H264",
                    40),
            ],
            MediaFile);
        return new DuplicateReviewViewModel(
            group,
            new MediaVersionSelectionPolicy(),
            new SetPreferredVersion(new StubGroups(group)),
            new MediaVersionPreferences(PreferHdr: true));
    }

    private static PlayerSurfaces BuildPlayer() => new()
    {
        Player = new PlayerViewModel(new StubCoordinator()),
        Tracks = new TrackSelectorViewModel(new SelectTrack(new StubEngine(), new StubPreferences()), "file"),
        AudioOutput = new AudioOutputViewModel(new StubAudio()),
        Markers = new MarkerEditorViewModel(
            (kind, start, end, markerId) => new SaveManualMarker(new StubMarkers()).ExecuteAsync(
                new SaveManualMarkerCommand(new SeriesId(Title.Value), kind, start, end, null, markerId)),
            markerId => new DeleteManualMarker(new StubMarkers()).ExecuteAsync(markerId)),
        Resume = new ResumePromptViewModel(
            new ResumeDecision(ResumeChoice.Resume, TimeSpan.FromMinutes(10))),
        Skip = new SkipMarkerViewModel(),
        NextEpisode = new NextEpisodeViewModel(),
        VersionSwitch = new VersionSwitchViewModel(),
        VideoStatus = new VideoStatusViewModel(),
        LooseFile = new LooseFileViewModel(),
    };

    private sealed class StubHomeReadModel(HomeProgressEntry[] entries) : IHomeReadModel
    {
        public Task<IReadOnlyList<HomeProgressEntry>> ReadProgressAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<HomeProgressEntry>>(entries);
        }

        public Task<IReadOnlyList<RecentlyAddedItem>> ReadRecentlyAddedAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<RecentlyAddedItem>>([]);
        }

        public Task<LibrarySummary> ReadLibrarySummaryAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new LibrarySummary(1, 0, 0));
        }
    }

    private sealed class StubCatalog(CatalogTitleKind kind = CatalogTitleKind.Movie) : ICatalogQueryService
    {
        public Task<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CatalogPage(
                [
                    new CatalogItem(
                        Title,
                        kind,
                        "Título",
                        2016,
                        IsAvailable: true,
                        HasProgress: false,
                        IsPersonal: false,
                        DateTimeOffset.UnixEpoch,
                        DateTimeOffset.UnixEpoch),
                ],
                null));
        }
    }

    /// <summary>
    /// The dialog's shell half: open puts the veil up and hides the first-run form, cancel is only
    /// an offer while it is up, and a folder accepted closes it without being asked.
    /// </summary>
    [AvaloniaFact]
    public async Task The_add_dialog_opens_cancels_and_closes_itself_on_a_successful_add()
    {
        var roots = new RecordingRoots();
        var onboarding = new RootOnboardingViewModel(
            new AddLibraryRoot(roots, new StubNormalizer()), new RemoveLibraryRoot(roots), roots);
        var shell = new ShellViewModel(
            new NavigationService(),
            new ShellSurfaces { Onboarding = onboarding });

        // Nothing yet: no folders means the first run shows, and cancel has nothing to close.
        Assert.True(shell.ShowsOnboarding);
        Assert.False(shell.CancelAddMediaCommand.CanExecute(null));

        shell.AddMediaCommand.Execute(null);
        Assert.True(shell.IsAddingRoot);
        Assert.False(shell.ShowsOnboarding);
        Assert.True(shell.CancelAddMediaCommand.CanExecute(null));

        shell.CancelAddMediaCommand.Execute(null);
        Assert.False(shell.IsAddingRoot);
        Assert.True(shell.ShowsOnboarding);

        // Reopened, and this time the folder is accepted: the dialog closes itself, and what is
        // owed next - the consent - keeps the first-run surface on duty.
        shell.AddMediaCommand.Execute(null);
        onboarding.Path = "R:\\media";
        await onboarding.AddAsync(TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 100 && shell.IsAddingRoot; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.False(shell.IsAddingRoot, "A successful add left the dialog open.");
        Assert.True(onboarding.InitialScanConsentRequired);
        Assert.True(shell.ShowsOnboarding);
    }

    private sealed class StubRoots : ILibraryRootRepository
    {
        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<LibraryRoot?>(null);

        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibraryRoot>>([]);

        public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAvailabilityAsync(
            LibraryRootId id,
            RootAvailability availability,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// The folder list is drawn on Settings and it used to be refreshed only on Library, so a drive
    /// unplugged while somebody sat in Settings kept saying it was connected until they walked
    /// through the Library and came back.
    /// </summary>
    /// <remarks>
    /// Asserted on the read reaching the repository rather than on a word on screen: what broke was
    /// that nothing asked, and a test that only looked at the row would pass on a list refreshed by
    /// the previous route.
    /// </remarks>
    [AvaloniaFact]
    public async Task Walking_into_settings_reads_the_folder_list_again()
    {
        var navigation = new NavigationService();
        var roots = new CountingRoots();
        var onboarding = new RootOnboardingViewModel(
            new AddLibraryRoot(roots, new StubNormalizer()),
            removeLibraryRoot: null,
            roots);
        _ = new ShellViewModel(navigation, new ShellSurfaces { Onboarding = onboarding });
        var before = roots.Reads;

        navigation.Navigate(AppRoute.Settings);
        for (var attempt = 0; attempt < 100 && roots.Reads == before; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.True(
            roots.Reads > before,
            "walking into Settings never asked the repository for the folder list, so the list on "
            + "screen is whatever the last route left there.");
    }

    private sealed class StubNormalizer : IPathNormalizer
    {
        public string NormalizeAndValidate(string path, RootKind kind) => path;
    }

    private sealed class CountingRoots : ILibraryRootRepository
    {
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<LibraryRoot?>(null);

        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default)
        {
            _ = Interlocked.Increment(ref _reads);
            return Task.FromResult<IReadOnlyList<LibraryRoot>>([]);
        }

        public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAvailabilityAsync(
            LibraryRootId id,
            RootAvailability availability,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingRoots : ILibraryRootRepository
    {
        public LibraryRoot? Added { get; private set; }

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Added);

        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibraryRoot>>(Added is null ? [] : [Added]);

        public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default)
        {
            Added = root;
            return Task.CompletedTask;
        }

        public Task SetAvailabilityAsync(
            LibraryRootId id,
            RootAvailability availability,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubCandidates : IMatchCandidateRepository
    {
        public Task ReplaceForMediaFileAsync(
            MediaFileId mediaFileId,
            IReadOnlyList<MatchCandidate> candidates,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<MatchCandidate>> GetForMediaFileAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MatchCandidate>>([]);

        public Task<IReadOnlyList<MatchCandidate>> ListForReviewAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MatchCandidate>>([]);

        public Task<MatchDecisionWriteResult> TrySetReviewStateAsync(
            MediaFileId mediaFileId,
            CandidateId candidateId,
            int expectedRevision,
            ReviewState reviewState,
            bool lockDecision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MatchDecisionWriteResult(MatchDecisionWriteOutcome.Applied, null));
    }

    private sealed class StubEvents : ApSolutions.LocalMedia.Application.Events.IApplicationEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
            where TEvent : notnull => Task.CompletedTask;
    }

    private sealed class StubMetadata : ICatalogMetadataRepository
    {
        public Task<CatalogMetadata?> GetAsync(TitleId titleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogMetadata?>(null);

        public Task<MetadataWriteResult> TrySaveAsync(
            CatalogMetadata catalog,
            int expectedRevision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MetadataWriteResult(MetadataWriteOutcome.Applied, catalog));

        public Task<IReadOnlyList<CatalogMetadata>> ListStaleAsync(
            DateTimeOffset staleBefore,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogMetadata>>([]);
    }

    private sealed class StubGroups(MediaVersionGroup group) : IMediaVersionGroupRepository
    {
        public Task<MediaVersionGroup?> FindByContentKeyAsync(
            string contentKey,
            CancellationToken cancellationToken = default) => Task.FromResult<MediaVersionGroup?>(group);

        public Task<MediaVersionGroup?> FindByIdAsync(
            MediaVersionId groupId,
            CancellationToken cancellationToken = default) => Task.FromResult<MediaVersionGroup?>(group);

        public Task<MediaVersionGroup?> FindByMemberAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) => Task.FromResult<MediaVersionGroup?>(group);

        public Task SaveAsync(MediaVersionGroup value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubRenamer : ISafeFileRenamer
    {
        public Task<RenameExecutionResult> ExecuteAsync(
            RenamePlan plan,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenameExecutionResult(RenameExecutionOutcome.Succeeded, plan));

        public Task<RenameExecutionResult> UndoAsync(
            RenamePlan plan,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenameExecutionResult(RenameExecutionOutcome.Undone, plan));

        public Task<IReadOnlyList<RenameAuditEntry>> GetAuditLogAsync(
            Guid planId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RenameAuditEntry>>([]);
    }

    private sealed class StubPreferences : IPlaybackPreferenceRepository
    {
        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) => Task.FromResult<PlaybackPreference?>(null);

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMarkers : IIntroMarkerRepository
    {
        public Task<IReadOnlyList<IntroMarker>> GetForSeriesAsync(
            SeriesId seriesId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IntroMarker>>([]);

        public Task<IntroMarker?> GetAsync(Guid markerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IntroMarker?>(null);

        public Task SaveAsync(IntroMarker marker, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid markerId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubAudio : IAudioDeviceCatalog
    {
        public Task<IReadOnlyList<AudioOutputDevice>> GetOutputsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AudioOutputDevice>>([]);
    }

    private sealed class StubCoordinator : IPlaybackSessionCoordinator
    {
        public PlaybackSession? ActiveSession => null;

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PlaybackSession(Guid.Empty, request.MediaFileId, request.Path));

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubEngine : IMediaPlayerEngine
    {
        public PlaybackState State => PlaybackState.Idle;

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PlaybackFailureEventArgs>? Failure
        {
            add { }
            remove { }
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PlayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(PlaybackState.Idle, TimeSpan.Zero, null, []));

        public Task SelectTrackAsync(
            MediaTrackKind kind,
            string? trackId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<MediaTrack> AddExternalSubtitleAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaTrack(path, MediaTrackKind.Subtitle));

        public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
