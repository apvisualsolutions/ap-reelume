// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Backup;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Metadata;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Review;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Updates;

namespace ApSolutions.LocalMedia.Presentation.Shell;

public sealed class ShellViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<AppRoute> ApprovedRoutes = Enum.GetValues<AppRoute>();
    private readonly INavigationService _navigationService;
    private readonly IReadOnlyList<AppRoute> _routes;
    private readonly ShellSurfaces _surfaces;
    private readonly AsyncRelayCommand _editMetadata;
    private readonly AsyncRelayCommand _previewRename;
    private readonly AsyncRelayCommand _reviewDuplicates;
    private readonly AsyncRelayCommand _closePlayer;
    private readonly AsyncRelayCommand _toggleMini;
    private readonly AsyncRelayCommand _toggleFullscreen;
    private MetadataEditorViewModel? _metadataEditor;
    private RenamePreviewViewModel? _rename;
    private DuplicateReviewViewModel? _duplicates;
    private PlayerSurfaces? _player;
    private PlaybackMode _playbackMode = PlaybackMode.Embedded;

    public ShellViewModel(INavigationService navigationService)
        : this(navigationService, appearanceSettings: null, library: null)
    {
    }

    public ShellViewModel(
        INavigationService navigationService,
        AppearanceSettingsViewModel? appearanceSettings)
        : this(navigationService, appearanceSettings, library: null)
    {
    }

    public ShellViewModel(
        INavigationService navigationService,
        AppearanceSettingsViewModel? appearanceSettings,
        LibraryViewModel? library)
        : this(navigationService, appearanceSettings, library, home: null)
    {
    }

    public ShellViewModel(
        INavigationService navigationService,
        AppearanceSettingsViewModel? appearanceSettings,
        LibraryViewModel? library,
        HomeViewModel? home)
        : this(navigationService, appearanceSettings, library, home, recommendationSettings: null)
    {
    }

    public ShellViewModel(
        INavigationService navigationService,
        AppearanceSettingsViewModel? appearanceSettings,
        LibraryViewModel? library,
        HomeViewModel? home,
        RecommendationSettingsViewModel? recommendationSettings)
        : this(navigationService, appearanceSettings, library, home, recommendationSettings, lifecycleSettings: null)
    {
    }

    public ShellViewModel(
        INavigationService navigationService,
        AppearanceSettingsViewModel? appearanceSettings,
        LibraryViewModel? library,
        HomeViewModel? home,
        RecommendationSettingsViewModel? recommendationSettings,
        LifecycleSettingsViewModel? lifecycleSettings)
        : this(
            navigationService,
            appearanceSettings,
            library,
            home,
            recommendationSettings,
            lifecycleSettings,
            backups: null,
            restore: null,
            privacySettings: null)
    {
    }

    public ShellViewModel(
        INavigationService navigationService,
        AppearanceSettingsViewModel? appearanceSettings,
        LibraryViewModel? library,
        HomeViewModel? home,
        RecommendationSettingsViewModel? recommendationSettings,
        LifecycleSettingsViewModel? lifecycleSettings,
        BackupViewModel? backups,
        RestoreWizardViewModel? restore)
        : this(
            navigationService,
            appearanceSettings,
            library,
            home,
            recommendationSettings,
            lifecycleSettings,
            backups,
            restore,
            privacySettings: null)
    {
    }

    public ShellViewModel(
        INavigationService navigationService,
        AppearanceSettingsViewModel? appearanceSettings,
        LibraryViewModel? library,
        HomeViewModel? home,
        RecommendationSettingsViewModel? recommendationSettings,
        LifecycleSettingsViewModel? lifecycleSettings,
        BackupViewModel? backups,
        RestoreWizardViewModel? restore,
        PrivacySettingsViewModel? privacySettings)
        : this(
            navigationService,
            new ShellSurfaces
            {
                AppearanceSettings = appearanceSettings,
                Library = library,
                Home = home,
                RecommendationSettings = recommendationSettings,
                LifecycleSettings = lifecycleSettings,
                Backups = backups,
                Restore = restore,
                PrivacySettings = privacySettings,
            })
    {
    }

    /// <summary>
    /// The shell as the application actually composes it. Everything the product declares arrives
    /// here, either built or as the request that builds it, because a surface the shell was never
    /// handed is a surface nobody can open.
    /// </summary>
    public ShellViewModel(INavigationService navigationService, ShellSurfaces surfaces)
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
        _routes = ApprovedRoutes;
        _navigationService.Navigated += OnNavigated;
        NavigateCommand = new RouteNavigationCommand(_navigationService);
        _editMetadata = new AsyncRelayCommand(
            () => OpenMetadataEditorAsync(CancellationToken.None),
            () => _surfaces.OpenMetadataEditor is not null && SelectedTitleId is not null);
        _previewRename = new AsyncRelayCommand(
            () => OpenRenamePreviewAsync(CancellationToken.None),
            () => _surfaces.OpenRename is not null && SelectedTitleId is not null);
        _reviewDuplicates = new AsyncRelayCommand(
            () => OpenDuplicatesAsync(CancellationToken.None),
            () => _surfaces.OpenDuplicates is not null && SelectedTitleId is not null);
        _closePlayer = new AsyncRelayCommand(
            () => ClosePlayerAsync(CancellationToken.None),
            () => _player is not null);
        _toggleMini = new AsyncRelayCommand(
            () => TogglePlaybackModeAsync(PlaybackMode.Mini, CancellationToken.None),
            () => _player is not null && _surfaces.ChangePlaybackMode is not null);
        _toggleFullscreen = new AsyncRelayCommand(
            () => TogglePlaybackModeAsync(PlaybackMode.Fullscreen, CancellationToken.None),
            () => _player is not null && _surfaces.ChangePlaybackMode is not null);

        if (Library is { } libraryViewModel)
        {
            // A button bound to a command asks once and then waits to be told. Choosing a title is
            // what makes these three possible, so the choice has to raise the event itself.
            libraryViewModel.PropertyChanged += OnLibraryChanged;
        }

        if (Onboarding is { } onboarding)
        {
            onboarding.PropertyChanged += OnOnboardingChanged;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppRoute CurrentRoute => _navigationService.CurrentRoute;

    public IReadOnlyList<AppRoute> Routes => _routes;

    public ICommand NavigateCommand { get; }

    /// <summary>Opens the metadata editor of the title the library has open.</summary>
    public ICommand EditMetadataCommand => _editMetadata;

    /// <summary>Previews a rename of the open title. Asking for the preview renames nothing.</summary>
    public ICommand PreviewRenameCommand => _previewRename;

    /// <summary>Shows every version of the open title, under Review, where corrections belong.</summary>
    public ICommand ReviewDuplicatesCommand => _reviewDuplicates;

    public ICommand ClosePlayerCommand => _closePlayer;

    /// <summary>Moves the running session into the always-on-top mini window, and back.</summary>
    public ICommand ToggleMiniPlayerCommand => _toggleMini;

    public ICommand ToggleFullscreenCommand => _toggleFullscreen;

    public AppearanceSettingsViewModel? AppearanceSettings => _surfaces.AppearanceSettings;

    public LibraryViewModel? Library => _surfaces.Library;

    public HomeViewModel? Home => _surfaces.Home;

    public RecommendationSettingsViewModel? RecommendationSettings => _surfaces.RecommendationSettings;

    public LifecycleSettingsViewModel? LifecycleSettings => _surfaces.LifecycleSettings;

    public BackupViewModel? Backups => _surfaces.Backups;

    public RestoreWizardViewModel? Restore => _surfaces.Restore;

    public PrivacySettingsViewModel? PrivacySettings => _surfaces.PrivacySettings;

    public UpdateViewModel? Updates => _surfaces.Updates;

    public RootOnboardingViewModel? Onboarding => _surfaces.Onboarding;

    public ReviewInboxViewModel? ReviewInbox => _surfaces.ReviewInbox;

    public ScanSettingsViewModel? ScanSettings => _surfaces.ScanSettings;

    public ShortcutSettingsViewModel? Shortcuts => _surfaces.Shortcuts;

    public SubtitleStyleViewModel? SubtitleStyle => _surfaces.SubtitleStyle;

    public SegmentDetectionSettingsViewModel? SegmentDetection => _surfaces.SegmentDetection;

    public MetadataEditorViewModel? MetadataEditor
    {
        get => _metadataEditor;
        private set
        {
            if (SetField(ref _metadataEditor, value))
            {
                OnPropertyChanged(nameof(HasMetadataEditor));
            }
        }
    }

    public RenamePreviewViewModel? Rename
    {
        get => _rename;
        private set
        {
            if (SetField(ref _rename, value))
            {
                OnPropertyChanged(nameof(HasRename));
            }
        }
    }

    public DuplicateReviewViewModel? Duplicates
    {
        get => _duplicates;
        private set
        {
            if (SetField(ref _duplicates, value))
            {
                OnPropertyChanged(nameof(HasDuplicates));
            }
        }
    }

    /// <summary>The playing session, or nothing when no media is open.</summary>
    public PlayerSurfaces? Player
    {
        get => _player;
        private set
        {
            if (SetField(ref _player, value))
            {
                OnPropertyChanged(nameof(IsPlayerVisible));
                OnPropertyChanged(nameof(HasTracks));
                OnPropertyChanged(nameof(HasAudioOutput));
                OnPropertyChanged(nameof(HasMarkers));
                OnPropertyChanged(nameof(HasDetectedReview));
                OnPropertyChanged(nameof(HasResumePrompt));
                OnPropertyChanged(nameof(HasSkipMarker));
                OnPropertyChanged(nameof(HasNextEpisode));
                OnPropertyChanged(nameof(HasVersionSwitch));
                OnPropertyChanged(nameof(HasPlayerVersions));
                OnPropertyChanged(nameof(HasVideoStatus));
                OnPropertyChanged(nameof(HasLooseFile));
                OnPropertyChanged(nameof(IsPrimaryContentVisible));
                _closePlayer.RaiseCanExecuteChanged();
                _toggleMini.RaiseCanExecuteChanged();
                _toggleFullscreen.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Where the picture is being shown right now. The view follows this, not the reverse.</summary>
    public PlaybackMode PlaybackMode
    {
        get => _playbackMode;
        private set => SetField(ref _playbackMode, value);
    }

    public bool HasRestore => Restore is not null;

    public bool HasPrivacySettings => PrivacySettings is not null;

    public bool HasUpdates => Updates is not null;

    public bool HasRecommendationSettings => RecommendationSettings is not null;

    public bool HasLifecycleSettings => LifecycleSettings is not null;

    public bool HasOnboarding => Onboarding is not null;

    public bool HasReviewInbox => ReviewInbox is not null;

    public bool HasScanSettings => ScanSettings is not null;

    public bool HasShortcuts => Shortcuts is not null;

    public bool HasSubtitleStyle => SubtitleStyle is not null;

    public bool HasSegmentDetection => SegmentDetection is not null;

    public bool HasMetadataEditor => MetadataEditor is not null;

    public bool HasRename => Rename is not null;

    public bool HasDuplicates => Duplicates is not null;

    public bool HasTracks => Player?.Tracks is not null;

    public bool HasAudioOutput => Player?.AudioOutput is not null;

    public bool HasMarkers => Player?.Markers is not null;

    public bool HasDetectedReview => Player?.DetectedReview is not null;

    public bool HasResumePrompt => Player?.Resume is not null;

    public bool HasSkipMarker => Player?.Skip is not null;

    public bool HasNextEpisode => Player?.NextEpisode is not null;

    public bool HasVersionSwitch => Player?.VersionSwitch is not null;

    /// <summary>True only when the playing title has other versions to move the session to.</summary>
    public bool HasPlayerVersions => Player?.Versions is { HasAlternatives: true };

    public bool HasVideoStatus => Player?.VideoStatus is not null;

    public bool HasLooseFile => Player?.LooseFile is not null;

    public bool IsSettingsVisible => CurrentRoute == AppRoute.Settings;

    public bool IsLibraryVisible => CurrentRoute == AppRoute.Library;

    public bool IsHomeVisible => CurrentRoute == AppRoute.Home && Home is not null;

    public bool IsBackupsVisible => CurrentRoute == AppRoute.Backups && Backups is not null;

    public bool IsReviewVisible => CurrentRoute == AppRoute.Review && ReviewInbox is not null;

    /// <summary>True while a session is on screen; it covers whatever route is underneath it.</summary>
    public bool IsPlayerVisible => Player is not null;

    public bool IsPrimaryContentVisible =>
        !IsSettingsVisible
        && !IsLibraryVisible
        && !IsHomeVisible
        && !IsBackupsVisible
        && !IsReviewVisible
        && !IsPlayerVisible;

    /// <summary>Opens one media file and shows everything that session puts on screen.</summary>
    public async Task OpenPlayerAsync(PlayDetailsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_surfaces.OpenPlayer is not { } open)
        {
            return;
        }

        Player = await open(request, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Opens one file from outside the library and shows the session it makes.
    /// </summary>
    /// <remarks>
    /// The surfaces are assigned exactly as <see cref="OpenPlayerAsync"/> assigns them, and that is
    /// the whole point of it existing: until 2026-08-17 a file activated from Explorer reached the
    /// engine and never reached <see cref="Player"/>, so it played with nothing on screen — the banner
    /// that offers to add its folder lives on those surfaces and could not be seen, let alone pressed.
    /// </remarks>
    public async Task OpenLoosePlayerAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (_surfaces.OpenLoosePlayer is not { } open)
        {
            return;
        }

        Player = await open(path, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closes the session and stops the media with it.</summary>
    public async Task ClosePlayerAsync(CancellationToken cancellationToken = default)
    {
        if (Player is null)
        {
            return;
        }

        if (_surfaces.ClosePlayer is { } close)
        {
            await close(cancellationToken).ConfigureAwait(true);
        }

        // A closed session cannot stay in a window mode of its own, or the mini window outlives the
        // media it was showing.
        PlaybackMode = PlaybackMode.Embedded;
        Player = null;
    }

    /// <summary>Switches into a mode, or back to embedded when it is already the one in force.</summary>
    public async Task TogglePlaybackModeAsync(PlaybackMode mode, CancellationToken cancellationToken = default)
    {
        if (Player is null || _surfaces.ChangePlaybackMode is not { } change)
        {
            return;
        }

        var target = PlaybackMode == mode ? PlaybackMode.Embedded : mode;
        PlaybackMode = await change(target, cancellationToken).ConfigureAwait(true);
    }

    public async Task OpenMetadataEditorAsync(CancellationToken cancellationToken = default)
    {
        if (_surfaces.OpenMetadataEditor is { } open && SelectedTitleId is { } titleId)
        {
            MetadataEditor = await open(titleId, cancellationToken).ConfigureAwait(true);
        }
    }

    public async Task OpenRenamePreviewAsync(CancellationToken cancellationToken = default)
    {
        if (_surfaces.OpenRename is { } open && SelectedTitleId is { } titleId)
        {
            Rename = await open(titleId, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Loads the versions of the open title and moves to Review, because comparing two copies of the
    /// same film is a correction and corrections live in one place.
    /// </summary>
    public async Task OpenDuplicatesAsync(CancellationToken cancellationToken = default)
    {
        if (_surfaces.OpenDuplicates is { } open && SelectedTitleId is { } titleId)
        {
            Duplicates = await open(titleId, cancellationToken).ConfigureAwait(true);
            if (Duplicates is not null)
            {
                _navigationService.Navigate(AppRoute.Review);
            }
        }
    }

    private TitleId? SelectedTitleId => Library?.SelectedItem?.Item.Id;

    private void OnLibraryChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(LibraryViewModel.SelectedItem) or nameof(LibraryViewModel.Surface))
        {
            _editMetadata.RaiseCanExecuteChanged();
            _previewRename.RaiseCanExecuteChanged();
            _reviewDuplicates.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Consenting to the first scan starts it, and the library reloads when it is done. Recording the
    /// answer and doing nothing with it is how a new install adds a folder and stays empty.
    /// </summary>
    private void OnOnboardingChanged(object? sender, PropertyChangedEventArgs args) =>
        GuardedEvent.Run(() => OnboardingChangedAsync(args));

    private async Task OnboardingChangedAsync(PropertyChangedEventArgs args)
    {
        // A removed folder leaves the catalog, so the library on screen reloads to say so
        // (LIB-A01); the videos on disk were never part of the removal.
        if (args.PropertyName == nameof(RootOnboardingViewModel.RemovedRootId)
            && Onboarding is { RemovedRootId: not null }
            && Library is { } reloaded)
        {
            await reloaded.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            return;
        }

        if (args.PropertyName != nameof(RootOnboardingViewModel.CanStartInitialScan)
            || Onboarding is not { CanStartInitialScan: true, AddedRoot: { } root }
            || _surfaces.StartScan is not { } scan)
        {
            return;
        }

        await scan(root.Id, CancellationToken.None).ConfigureAwait(true);
        if (Library is { } library)
        {
            await library.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private void OnNavigated(object? sender, AppRoute route) =>
        GuardedEvent.Run(() => NavigatedAsync(route));

    private async Task NavigatedAsync(AppRoute route)
    {
        OnPropertyChanged(nameof(CurrentRoute));
        OnPropertyChanged(nameof(IsSettingsVisible));
        OnPropertyChanged(nameof(IsLibraryVisible));
        OnPropertyChanged(nameof(IsHomeVisible));
        OnPropertyChanged(nameof(IsBackupsVisible));
        OnPropertyChanged(nameof(IsReviewVisible));
        OnPropertyChanged(nameof(IsPrimaryContentVisible));
        if (route == AppRoute.Library && Onboarding is { } onboarding)
        {
            // The folder list is read on every visit: managing folders is this route's job, and a
            // stale list would offer removals of folders that already left.
            await onboarding.RefreshRootsAsync(CancellationToken.None).ConfigureAwait(true);
        }

        if (route == AppRoute.Library && Library is { Items.Count: 0 })
        {
            await Library.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        }
        else if (route == AppRoute.Home && Home is not null)
        {
            await Home.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        }
        else if (route == AppRoute.Review && ReviewInbox is { Items.Count: 0 })
        {
            await ReviewInbox.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class RouteNavigationCommand(INavigationService navigationService) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => parameter is AppRoute;

        public void Execute(object? parameter)
        {
            if (parameter is AppRoute route)
            {
                navigationService.Navigate(route);
            }
        }
    }

    /// <summary>
    /// A command that opens one surface and says when it became possible. Without the event a button
    /// stays as it was built, however many titles are opened afterwards.
    /// </summary>
}
