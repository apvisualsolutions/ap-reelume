// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Privacy;
using ApSolutions.LocalMedia.Domain.Backup;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Backup;
using ApSolutions.LocalMedia.Presentation.Catalog;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Review;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Presentation.Show;
using ApSolutions.LocalMedia.Presentation.Theme;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// One stop of the approved journey, built from scratch so every run is deterministic.
/// <para>
/// <paramref name="IsPage"/> separates a screen a person navigates to from a component another screen
/// embeds. Only a page owns a heading; a heading inside an embedded component would clutter the
/// reader's heading list with entries that lead nowhere.
/// </para>
/// </summary>
public sealed record JourneySurface(
    int Step,
    string StepId,
    string Surface,
    Func<Control> Build,
    bool IsPage = true);

/// <summary>
/// The canonical journey the MVP gate audits: first run, add a root, search, review, open details,
/// play, control, resume, favourite, backup, settings.
/// <para>
/// Every surface is constructed here rather than in each test, so all six audit suites look at
/// exactly the same application and a defect found by one is reproducible from the others.
/// </para>
/// </summary>
public static class CanonicalJourney
{
    private static readonly TitleId MovieId = new(CreateGuid(21));
    private static readonly TitleId ShowId = new(CreateGuid(22));
    private static readonly EpisodeId EpisodeId = new(CreateGuid(23));
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The eleven journey steps, in the order a person walks them.</summary>
    public static IReadOnlyList<JourneySurface> Surfaces { get; } =
    [
        new(1, "first-run", nameof(ShellView), () => BuildShell(AppRoute.Home)),
        new(2, "add-root", nameof(RootOnboardingView), BuildOnboarding),
        new(3, "search", nameof(LibraryView), BuildLibrary),
        new(4, "review", nameof(ReviewInboxView), () => new ReviewInboxView()),
        new(5, "details", nameof(MovieDetailsView), BuildMovieDetails),
        new(5, "details", nameof(ShowDetailsView), BuildShowDetails),
        new(6, "play", nameof(PlayerView), BuildPlayer, IsPage: false),
        new(7, "control", nameof(TransportControlsView), BuildTransport, IsPage: false),
        new(8, "resume", nameof(ResumePromptView), BuildResumePrompt, IsPage: false),
        new(8, "resume", nameof(HomeView), BuildHome),
        new(9, "favourite", nameof(PersonalActionsView), BuildPersonalActions, IsPage: false),
        new(10, "backup", nameof(ShellView), () => BuildShell(AppRoute.Settings, SettingsSection.Backups)),
        new(10, "backup", nameof(BackupView), BuildBackup),
        new(10, "backup", nameof(RestoreWizardView), BuildRestoreWizard),
        new(11, "settings", nameof(AppearanceSettingsView), BuildAppearanceSettings),
        new(11, "settings", nameof(RecommendationSettingsView), BuildRecommendationSettings),
        new(11, "settings", nameof(ScanSettingsView), BuildScanSettings),
        new(11, "settings", nameof(SubtitleStyleView), BuildSubtitleStyle),
        new(11, "settings", nameof(ShortcutSettingsView), BuildShortcutSettings),
        new(11, "settings", nameof(PrivacySettingsView), BuildPrivacySettings),
    ];

    /// <summary>Shows one surface in a window sized like the reference laptop viewport.</summary>
    public static SurfaceHost Show(
        JourneySurface surface,
        string cultureName = "es-ES",
        double width = 1366,
        double height = 768,
        double scale = 1.0,
        ThemeVariant? theme = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ApplyLanguage(cultureName);
        Assert.NotNull(Avalonia.Application.Current);
        Avalonia.Application.Current.RequestedThemeVariant = theme ?? ThemeVariant.Light;

        var view = surface.Build();
        var window = new Window
        {
            Width = width / scale,
            Height = height / scale,
            Content = view,
        };
        window.SetRenderScaling(scale);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
        return new SurfaceHost(window, view);
    }

    public static void ApplyLanguage(string cultureName)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo(cultureName));
    }

    private static ShellView BuildShell(AppRoute route, SettingsSection? section = null)
    {
        var navigation = new NavigationService();
        var viewModel = new ShellViewModel(
            navigation,
            BuildAppearanceSettingsViewModel(),
            BuildLibraryViewModel(),
            BuildHomeViewModel(),
            new RecommendationSettingsViewModel(new StubRecommendationSettings()),
            lifecycleSettings: null,
            BuildBackupViewModel(),
            BuildRestoreWizardViewModel());
        navigation.Navigate(route);
        if (section is { } chosen)
        {
            viewModel.CurrentSettingsSection = chosen;
        }

        return new ShellView { DataContext = viewModel };
    }

    private static BackupView BuildBackup() => new BackupView { DataContext = BuildBackupViewModel() };

    /// <summary>
    /// The privacy screen with diagnostics switched on and a preview showing, because the preview box
    /// and the export button are surface a person meets and an empty screen would not be audited.
    /// </summary>
    private static PrivacySettingsView BuildPrivacySettings()
    {
        var viewModel = new PrivacySettingsViewModel(
            new StubPrivacySettings(),
            new StubDiagnosticsBuilder(),
            () => new DiagnosticsInputs(
                "1.0.0",
                "10.0.26200",
                "10.0.0",
                "es-ES",
                HardwareAccelerationAvailable: true,
                HdrDisplayPresent: true,
                AudioEndpointCount: 2,
                LibraryItemCount: 12,
                RootCount: 1,
                Errors: [],
                History: [],
                SearchTerms: []),
            (_, _, _) => Task.FromResult<string?>(null),
            () => Noon,
            [
                new NetworkPurpose(
                    "TmdbMetadataProvider",
                    "api.themoviedb.org",
                    "Fetches the metadata a person explicitly asked to identify or refresh.",
                    RequiresConsent: true),
            ]);
        viewModel.DiagnosticsEnabled = true;
        viewModel.PreviewCommand.Execute(null);
        return new PrivacySettingsView { DataContext = viewModel };
    }

    /// <summary>
    /// The wizard after a dry run, because an empty wizard has nothing to audit: the rows, the finding
    /// list, and the confirmation only exist once an archive has been looked at.
    /// </summary>
    private static RestoreWizardView BuildRestoreWizard()
    {
        var viewModel = BuildRestoreWizardViewModel();
        viewModel.PreviewAsync(CancellationToken.None).GetAwaiter().GetResult();
        return new RestoreWizardView { DataContext = viewModel };
    }

    private static RestoreWizardViewModel BuildRestoreWizardViewModel() => new(
        (_, _, _) => Task.FromResult(new RestorePreview(
            new BackupManifest(BackupManifest.CurrentFormatVersion, "1.0.0", Noon, "0", null, [], []),
            [],
            [new RootRemapDecision("D:\\media", "D:\\media", RootRemapStatus.Missing)],
            RequiredBytes: 1024,
            AvailableBytes: 8192,
            MediaFileCount: 12,
            PathChangeCount: 0)),
        (_, _, _, _) => Task.FromResult(new RestoreResult(
            false,
            new RestorePreview(null, [], [], 0, 0, 0, 0),
            null,
            "not in an audit")),
        _ => Task.FromResult<string?>("D:\\exports\\library.zip"));

    /// <summary>
    /// The backup screen in its resting state. Auditing it mid-run would measure a progress bar the
    /// double drives rather than the surface a person actually meets when they open the destination.
    /// </summary>
    private static BackupViewModel BuildBackupViewModel() => new(
        (_, _) => Task.FromResult(new BackupResult(
            new BackupCopy("copy", Noon, IsValid: true),
            new BackupManifest(BackupManifest.CurrentFormatVersion, "1.0.0", Noon, "0", null, [], []),
            [])),
        (path, _, _) => Task.FromResult(new ExportResult(
            path,
            new BackupManifest(BackupManifest.CurrentFormatVersion, "1.0.0", Noon, "0", null, [], []),
            [])),
        _ => Task.FromResult<string?>(null));

    private static RootOnboardingView BuildOnboarding() => new RootOnboardingView
    {
        DataContext = new RootOnboardingViewModel(
            new ApSolutions.LocalMedia.Application.Discovery.AddLibraryRoot(
                new StubRootRepository(),
                new StubPathNormalizer())),
    };

    private static LibraryView BuildLibrary()
    {
        var viewModel = BuildLibraryViewModel();
        viewModel.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        return new LibraryView { DataContext = viewModel };
    }

    private static MovieDetailsView BuildMovieDetails() => new MovieDetailsView
    {
        DataContext = BuildMovieDetailsViewModel(),
    };

    private static ShowDetailsView BuildShowDetails() => new ShowDetailsView
    {
        DataContext = BuildShowDetailsViewModel(),
    };

    /// <summary>
    /// The player as the journey reaches it: with a session already playing. An idle player has
    /// nothing to operate, so auditing that state would measure the double rather than the step.
    /// </summary>
    private static PlayerView BuildPlayer()
    {
        var viewModel = new PlayerViewModel(new StubCoordinator());
        viewModel.ApplySessionState(PlaybackState.Playing, failure: null);
        return new PlayerView { DataContext = viewModel };
    }

    private static TransportControlsView BuildTransport() => new TransportControlsView
    {
        DataContext = new TransportControlsViewModel(new ControlPlayback(new StubEngine())),
    };

    private static ResumePromptView BuildResumePrompt() => new ResumePromptView
    {
        DataContext = new ResumePromptViewModel(
            new ResumeDecision(ResumeChoice.Resume, TimeSpan.FromMinutes(40))),
    };

    private static HomeView BuildHome() => new HomeView { DataContext = BuildHomeViewModel() };

    /// <summary>
    /// The personal marks wired the way the host wires them: the surface asks, something records the
    /// change, and the recorded state comes back. Without the round trip the buttons look inert and an
    /// audit would be measuring the double instead of the application.
    /// </summary>
    private static PersonalActionsView BuildPersonalActions()
    {
        var state = PersonalState.Empty(ContentKey.ForTitle(MovieId)).WithFavorite(true).WithRating(7);
        PersonalActionsViewModel? viewModel = null;
        viewModel = new PersonalActionsViewModel(request =>
        {
            state = request.Kind switch
            {
                PersonalActionKind.ToggleFavorite => state.WithFavorite(!state.IsFavorite),
                PersonalActionKind.ToggleWatchLater => state.WithWatchLater(!state.IsWatchLater),
                _ => state.WithRating(request.Rating),
            };
            viewModel!.Apply(state);
            return Task.CompletedTask;
        });
        viewModel.Apply(state);
        return new PersonalActionsView { DataContext = viewModel };
    }

    private static AppearanceSettingsView BuildAppearanceSettings() => new AppearanceSettingsView
    {
        DataContext = BuildAppearanceSettingsViewModel(),
    };

    private static RecommendationSettingsView BuildRecommendationSettings() => new RecommendationSettingsView
    {
        DataContext = new RecommendationSettingsViewModel(new StubRecommendationSettings()),
    };

    private static ScanSettingsView BuildScanSettings() => new ScanSettingsView
    {
        DataContext = new ScanSettingsViewModel(),
    };

    private static SubtitleStyleView BuildSubtitleStyle()
    {
        var viewModel = new SubtitleStyleViewModel(new StubPreferenceRepository());
        viewModel.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        return new SubtitleStyleView { DataContext = viewModel };
    }

    private static ShortcutSettingsView BuildShortcutSettings() => new ShortcutSettingsView
    {
        DataContext = new ShortcutSettingsViewModel(new ShortcutMap()),
    };

    private static AppearanceSettingsViewModel BuildAppearanceSettingsViewModel() =>
        new(new StubThemeService());

    private static HomeViewModel BuildHomeViewModel()
    {
        var viewModel = new HomeViewModel(
            new GetHome(new StubHomeReadModel()),
            new NavigationService(),
            onResume: null,
            new RecommendationsViewModel(
                new GetRecommendations(new StubRecommendationReadModel()),
                new StubRecommendationSettings()));
        viewModel.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        return viewModel;
    }

    private static LibraryViewModel BuildLibraryViewModel() => new(
        new StubCatalogQueryService(),
        BuildMovieDetailsViewModel(),
        BuildShowDetailsViewModel());

    private static MovieDetailsViewModel BuildMovieDetailsViewModel()
    {
        var viewModel = new MovieDetailsViewModel();
        viewModel.Apply(
            Item(MovieId, CatalogTitleKind.Movie, "Arrival", isAvailable: true),
            State(ContentKey.ForTitle(MovieId), TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(116)),
            new MediaVersionGroup(
                new MediaVersionId(CreateGuid(34)),
                ContentKey.ForTitle(MovieId).Value,
                [
                    new MediaVersion(
                        new MediaFileId(CreateGuid(31)),
                        @"root\a.mkv",
                        true,
                        TimeSpan.FromMinutes(116),
                        3840,
                        2160,
                        true,
                        "HEVC",
                        90),
                    new MediaVersion(
                        new MediaFileId(CreateGuid(32)),
                        @"root\b.mkv",
                        false,
                        TimeSpan.FromMinutes(116),
                        1920,
                        1080,
                        false,
                        "H264",
                        40),
                ],
                new MediaFileId(CreateGuid(31))),
            PersonalState.Empty(ContentKey.ForTitle(MovieId)).WithFavorite(true).WithRating(7));
        return viewModel;
    }

    private static ShowDetailsViewModel BuildShowDetailsViewModel()
    {
        var viewModel = new ShowDetailsViewModel();
        var episodes = new[]
        {
            Episode(101, season: 1, number: 1, isAvailable: true, hasFile: true),
            Episode(102, season: 1, number: 2, isAvailable: true, hasFile: true),
            Episode(103, season: 1, number: 3, isAvailable: false, hasFile: true),
        };
        viewModel.Apply(
            Item(ShowId, CatalogTitleKind.Show, "Crónicas", isAvailable: true),
            episodes,
            new Dictionary<ContentKey, WatchState>
            {
                [ContentKey.ForEpisode(ShowId, episodes[0].Id)] = State(
                    ContentKey.ForEpisode(ShowId, episodes[0].Id),
                    TimeSpan.FromMinutes(45),
                    TimeSpan.FromMinutes(50),
                    WatchStatus.Watched),
            },
            PersonalState.Empty(ContentKey.ForTitle(ShowId)).WithWatchLater(true));
        return viewModel;
    }

    private static CatalogItem Item(TitleId id, CatalogTitleKind kind, string title, bool isAvailable) => new(
        id,
        kind,
        title,
        2016,
        isAvailable,
        HasProgress: true,
        IsPersonal: false,
        Noon,
        Noon);

    private static WatchState State(
        ContentKey content,
        TimeSpan position,
        TimeSpan duration,
        WatchStatus status = WatchStatus.InProgress) => new()
        {
            Content = content,
            Position = position,
            ObservedDuration = duration,
            SourceMediaFileId = new MediaFileId(CreateGuid(41)),
            Status = status,
            IsManualOverride = false,
            StartedUtc = Noon.AddHours(-1),
            UpdatedUtc = Noon,
        };

    private static EpisodeSequenceEntry Episode(
        int seed,
        int season,
        int number,
        bool isAvailable,
        bool hasFile) => new(
        new EpisodeId(CreateGuid(seed)),
        ShowId,
        season,
        number,
        hasFile ? new MediaFileId(CreateGuid(seed + 500)) : null,
        hasFile ? $@"root\s{season:D2}e{number:D2}.mkv" : null,
        isAvailable);

    private static Guid CreateGuid(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    /// <summary>A shown surface that closes its window when the test is done with it.</summary>
    public sealed class SurfaceHost(Window window, Control view) : IDisposable
    {
        public Window Window { get; } = window;

        public Control View { get; } = view;

        public void Dispose() => Window.Close();
    }

    private sealed class StubCatalogQueryService : ICatalogQueryService
    {
        public Task<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CatalogPage(
                [
                    Item(MovieId, CatalogTitleKind.Movie, "Arrival", isAvailable: true),
                    Item(ShowId, CatalogTitleKind.Show, "Crónicas", isAvailable: false),
                ],
                null));
        }
    }

    private sealed class StubHomeReadModel : IHomeReadModel
    {
        public Task<IReadOnlyList<HomeProgressEntry>> ReadProgressAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<HomeProgressEntry>>(
            [
                new HomeProgressEntry(
                    ContentKey.ForEpisode(ShowId, EpisodeId),
                    ShowId,
                    CatalogTitleKind.Show,
                    "Crónicas",
                    SeasonNumber: 1,
                    EpisodeNumber: 2,
                    EpisodeTitle: "Ned",
                    TimeSpan.FromMinutes(10),
                    TimeSpan.FromMinutes(50),
                    WatchStatus.InProgress,
                    IsAvailable: true,
                    Noon),
                new HomeProgressEntry(
                    ContentKey.ForTitle(MovieId),
                    MovieId,
                    CatalogTitleKind.Movie,
                    "Arrival",
                    SeasonNumber: null,
                    EpisodeNumber: null,
                    EpisodeTitle: null,
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromMinutes(90),
                    WatchStatus.InProgress,
                    IsAvailable: false,
                    Noon.AddHours(-2)),
            ]);
        }

        public Task<IReadOnlyList<RecentlyAddedItem>> ReadRecentlyAddedAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<RecentlyAddedItem>>(
            [
                new RecentlyAddedItem(MovieId, CatalogTitleKind.Movie, "Arrival", 2016, true, Noon),
            ]);
        }

        public Task<LibrarySummary> ReadLibrarySummaryAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new LibrarySummary(4, 2, 1));
        }
    }

    private sealed class StubRecommendationReadModel : IRecommendationReadModel
    {
        public Task<RecommendationTaste> ReadTasteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(RecommendationTaste.Empty);
        }

        public Task<IReadOnlyList<RecommendationCandidate>> ReadCandidatesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<RecommendationCandidate>>(
            [
                new RecommendationCandidate(MovieId, ["scifi"], ["Amy"], 2016, true, false, null),
                new RecommendationCandidate(ShowId, ["drama"], ["Sean"], 2011, true, false, null),
            ]);
        }
    }

    private sealed class StubRecommendationSettings : IRecommendationSettings
    {
        public bool IsEnabled { get; private set; } = true;

        public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;
    }

    private sealed class StubPrivacySettings : IPrivacySettings
    {
        public DiagnosticsConsent Current { get; private set; } = new(IsGranted: false, GrantedUtc: null);

        public void Save(DiagnosticsConsent consent) => Current = consent;
    }

    /// <summary>
    /// The audit needs a payload on screen, not a real machine reading. It produces the same closed
    /// report shape the application does, with fixed values.
    /// </summary>
    private sealed class StubDiagnosticsBuilder : IDiagnosticsBuilder
    {
        public DiagnosticsReport? Build(DiagnosticsConsent consent, DiagnosticsInputs inputs) =>
            consent.IsGranted
                ? new DiagnosticsReport(
                    DiagnosticsReport.CurrentFormatVersion,
                    "2026-08-03",
                    inputs.AppVersion,
                    inputs.WindowsVersion,
                    inputs.RuntimeVersion,
                    inputs.Locale,
                    new DiagnosticsCapabilities(true, true, "2-5"),
                    [],
                    [new DiagnosticsCount("libraryItems", "6-20")])
                : null;
    }

    private sealed class StubThemeService : IThemeService
    {
        public ThemePreference CurrentPreference => ThemePreference.System;

        public ThemeVariant PlayerThemeVariant => ThemeVariant.Dark;

        public bool AnimationsEnabled => true;

        public TimeSpan MotionDuration => TimeSpan.FromMilliseconds(150);

        public void Apply(ThemePreference preference)
        {
        }

        public bool TryApplyBackdrop(Window window) => false;
    }

    private sealed class StubPreferenceRepository : IPlaybackPreferenceRepository
    {
        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<PlaybackPreference?>(null);
        }

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class StubRootRepository : ApSolutions.LocalMedia.Domain.Discovery.ILibraryRootRepository
    {
        public Task<ApSolutions.LocalMedia.Domain.Discovery.LibraryRoot?> GetAsync(
            LibraryRootId id,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ApSolutions.LocalMedia.Domain.Discovery.LibraryRoot?>(null);
        }

        public Task<IReadOnlyList<ApSolutions.LocalMedia.Domain.Discovery.LibraryRoot>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ApSolutions.LocalMedia.Domain.Discovery.LibraryRoot>>([]);
        }

        public Task AddAsync(
            ApSolutions.LocalMedia.Domain.Discovery.LibraryRoot root,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class StubPathNormalizer : ApSolutions.LocalMedia.Application.Discovery.IPathNormalizer
    {
        public string NormalizeAndValidate(
            string path,
            ApSolutions.LocalMedia.Domain.Discovery.RootKind kind) => path;
    }

    private sealed class StubCoordinator : IPlaybackSessionCoordinator
    {
        public PlaybackSession? ActiveSession => null;

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PlaybackSession(Guid.Empty, request.MediaFileId, request.Path));
        }

        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class StubEngine : IMediaPlayerEngine
    {
        public PlaybackState State => PlaybackState.Playing;

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
            Task.FromResult(PlaybackSnapshot.Create(
                PlaybackState.Playing,
                TimeSpan.FromMinutes(10),
                TimeSpan.FromMinutes(50),
                []));

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
