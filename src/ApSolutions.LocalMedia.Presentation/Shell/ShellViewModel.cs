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
using ApSolutions.LocalMedia.Presentation.Show;
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
    private readonly AsyncRelayCommand _addMedia;
    private readonly AsyncRelayCommand _cancelAddMedia;
    private readonly AsyncRelayCommand _closePlayerPanel;
    private readonly PlayerPanelCommand _togglePlayerPanel;
    private bool _isAddingRoot;
    private int _reviewPendingCount;
    private SettingsSection _settingsSection = SettingsSection.Appearance;
    private MetadataEditorViewModel? _metadataEditor;
    private int _editorTab;
    private RenamePreviewViewModel? _rename;
    private DuplicateReviewViewModel? _duplicates;
    private PlayerSurfaces? _player;
    private PlaybackMode _playbackMode = PlaybackMode.Embedded;
    private PlayerPanel _playerPanel = PlayerPanel.None;
    private int _playerSessionOrdinal;
    private bool _isChromeRevealed = true;

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
        _addMedia = new AsyncRelayCommand(
            () =>
            {
                BeginAddMedia();
                return Task.CompletedTask;
            },
            () => Onboarding is not null);
        _cancelAddMedia = new AsyncRelayCommand(
            () =>
            {
                CloseAddMedia();
                return Task.CompletedTask;
            },
            () => IsAddingRoot);
        _togglePlayerPanel = new PlayerPanelCommand(TogglePlayerPanel);
        _closePlayerPanel = new AsyncRelayCommand(
            () =>
            {
                PlayerPanel = PlayerPanel.None;
                return Task.CompletedTask;
            },
            () => _playerPanel is not PlayerPanel.None);

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

        if (DuplicatesOverview is { } duplicatesOverview)
        {
            // A row on the overview opens the same comparison the film card's action opens, through
            // the same shell door - one surface, two ways in, never two answers.
            duplicatesOverview.GroupOpener = OpenDuplicatesForAsync;
        }

        // The route the service is born on never raises Navigated, so the surface it shows is fed
        // here: without this, Home waits for a navigation that already happened and starts empty
        // until somebody leaves and comes back.
        GuardedEvent.Run(() => NavigatedAsync(_navigationService.CurrentRoute));
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

    /// <summary>
    /// Opens the side panel named by the parameter, or closes it when it is the one already open.
    /// </summary>
    /// <remarks>
    /// One command for the five pills rather than five commands, which is what the library's kind
    /// filter already does: the parameter is the state, so a pill cannot open a panel and leave a
    /// second pill claiming to be pressed. And a pill is a toggle rather than a tab — the prototype
    /// gives the column's 320 px back to the picture when the open pill is pressed again, and a tab
    /// strip has no way to say "none of them".
    /// </remarks>
    public ICommand TogglePlayerPanelCommand => _togglePlayerPanel;

    /// <summary>The «×» in the panel column's own header; the same close the pill performs.</summary>
    public ICommand ClosePlayerPanelCommand => _closePlayerPanel;

    /// <summary>
    /// The one control at the foot of the navigation rail: it opens the surface a folder is added on
    /// and clears the form before it gets there.
    /// </summary>
    /// <remarks>
    /// The clearing is what makes this a command rather than a second Biblioteca. Nothing emptied the
    /// path after a folder was accepted, so somebody who added one and came back found their previous
    /// folder still typed in and a second press answered "it is already in the library" — a refusal
    /// caused by the screen rather than by them. The same for a refusal left over from a rejected
    /// path, and for a removal somebody walked away from half-confirmed.
    /// </remarks>
    public ICommand AddMediaCommand => _addMedia;

    public AppearanceSettingsViewModel? AppearanceSettings => _surfaces.AppearanceSettings;

    public LibraryViewModel? Library => _surfaces.Library;

    public HomeViewModel? Home => _surfaces.Home;

    public RecommendationSettingsViewModel? RecommendationSettings => _surfaces.RecommendationSettings;

    public LifecycleSettingsViewModel? LifecycleSettings => _surfaces.LifecycleSettings;

    public BackupViewModel? Backups => _surfaces.Backups;

    public DuplicatesOverviewViewModel? DuplicatesOverview => _surfaces.DuplicatesOverview;

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
                OnPropertyChanged(nameof(HasEditorPanel));
            }
        }
    }

    /// <summary>
    /// The tab standing in front of the editor panel: 0 is the metadata, 1 the renaming. Each door
    /// sets it as it opens, so pressing «Previsualizar renombrado» never leaves the metadata tab
    /// standing in front of the preview somebody just asked for.
    /// </summary>
    public int EditorTab
    {
        get => _editorTab;
        set => SetField(ref _editorTab, value);
    }

    /// <summary>One panel for the two title tools; the tabs decide, the way the player's panel does.</summary>
    public bool HasEditorPanel => HasMetadataEditor || HasRename;

    public RenamePreviewViewModel? Rename
    {
        get => _rename;
        private set
        {
            if (SetField(ref _rename, value))
            {
                OnPropertyChanged(nameof(HasRename));
                OnPropertyChanged(nameof(HasEditorPanel));
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
            // The session that is leaving stops being listened to before the next one arrives:
            // a handler left on a discarded player is how a closed session goes on deciding what
            // is on screen, which is the defect this repository has a name for.
            if (_player is { } leaving)
            {
                leaving.Player.PropertyChanged -= OnPlayerChanged;
            }

            if (SetField(ref _player, value))
            {
                if (value is { } arriving)
                {
                    arriving.Player.PropertyChanged += OnPlayerChanged;
                }

                // The chrome answers the state the session is in, not only the next change of it:
                // the surfaces are built and the file is opened before the shell is handed them, so
                // a session that is already playing when it arrives has no transition left to watch
                // — measured on the real engine, where the picture was running and the rail, the
                // title bar and the header were all still standing.
                ApplyChromeFor(value?.Player.IsPlaying == true);
                // A new session starts with its column closed and its own ordinal: the panel that
                // was open belonged to the file that just left, and a badge that kept counting the
                // previous one would be the only thing on this header still describing it.
                if (value is not null)
                {
                    _playerSessionOrdinal++;
                }

                PlayerPanel = PlayerPanel.None;
                OnPropertyChanged(nameof(PlayerSessionBadge));
                OnPropertyChanged(nameof(HasAudioPanel));
                OnPropertyChanged(nameof(HasSubtitlePanel));
                OnPropertyChanged(nameof(HasVideoPanel));
                OnPropertyChanged(nameof(HasMarkerPanel));
                OnPropertyChanged(nameof(IsPlayerVisible));
                OnPropertyChanged(nameof(PlayerTitle));
                OnPropertyChanged(nameof(PlayerSubtitle));
                OnPropertyChanged(nameof(HasPlayerTitle));
                OnPropertyChanged(nameof(HasPlayerSubtitle));
                OnPropertyChanged(nameof(HasTracks));
                OnPropertyChanged(nameof(HasAudioOutput));
                OnPropertyChanged(nameof(HasMarkers));
                OnPropertyChanged(nameof(HasDetectedReview));
                OnPropertyChanged(nameof(HasResumePrompt));
                OnPropertyChanged(nameof(HasSkipMarker));
                OnPropertyChanged(nameof(HasNextEpisode));
                OnPropertyChanged(nameof(HasVersionSwitch));
                OnPropertyChanged(nameof(HasPlayerVersions));
                OnPropertyChanged(nameof(HasPlayerPanels));
                OnPropertyChanged(nameof(HasVideoStatus));
                OnPropertyChanged(nameof(HasLooseFile));
                OnPropertyChanged(nameof(IsPrimaryContentVisible));
                _closePlayer.RaiseCanExecuteChanged();
                _toggleMini.RaiseCanExecuteChanged();
                _toggleFullscreen.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// What is playing, as the card that started it calls it, and the line under that.
    /// </summary>
    /// <remarks>
    /// The prototype writes both in the middle of the player's header and this application wrote
    /// nothing there, because the session holds a path and a path is not a heading. They travel with
    /// the request now, so the header says what a person pressed rather than where the file is.
    /// </remarks>
    public string PlayerTitle => Player?.Title ?? string.Empty;

    public string PlayerSubtitle => Player?.Subtitle ?? string.Empty;

    public bool HasPlayerTitle => PlayerTitle.Length > 0;

    public bool HasPlayerSubtitle => PlayerSubtitle.Length > 0;

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

    /// <summary>«Biblioteca y escaneo» exists if either of its two halves does.</summary>
    public bool HasLibrarySection => HasOnboarding || HasScanSettings;

    /// <summary>
    /// Whether the add-root dialog is over the shell right now.
    /// </summary>
    /// <remarks>
    /// Shell state rather than the onboarding's, because it is about the surface: the same form
    /// answers inline on a first run and floats over the grid the rest of the time, and which of
    /// the two is on screen is this flag's whole job. While it is set the onboarding hides
    /// (<see cref="ShowsOnboarding"/>), so the dialog's controls are the only instances of their
    /// keys on screen — two visible controls with one name is a defect the walk refuses.
    /// </remarks>
    public bool IsAddingRoot
    {
        get => _isAddingRoot;
        private set
        {
            if (SetField(ref _isAddingRoot, value))
            {
                OnPropertyChanged(nameof(ShowsOnboarding));
                _cancelAddMedia.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Closes the add-root dialog without adding anything.</summary>
    public ICommand CancelAddMediaCommand => _cancelAddMedia;

    /// <summary>
    /// Which Settings section is open, one at a time, which is the prototype's page. Not a route:
    /// leaving Settings and coming back finds the same section standing, the way the prototype's
    /// own setSection survives its navigation.
    /// </summary>
    public SettingsSection CurrentSettingsSection
    {
        get => _settingsSection;
        set
        {
            if (SetField(ref _settingsSection, value))
            {
                OnPropertyChanged(nameof(IsAppearanceSection));
                OnPropertyChanged(nameof(IsLibrarySection));
                OnPropertyChanged(nameof(IsRecommendationsSection));
                OnPropertyChanged(nameof(IsSubtitlesSection));
                OnPropertyChanged(nameof(IsSegmentDetectionSection));
                OnPropertyChanged(nameof(IsShortcutsSection));
                OnPropertyChanged(nameof(IsLifecycleSection));
                OnPropertyChanged(nameof(IsPrivacySection));
                OnPropertyChanged(nameof(IsUpdatesSection));
                OnPropertyChanged(nameof(IsCreditsSection));
                OnPropertyChanged(nameof(IsBackupsSection));
            }
        }
    }

    public bool IsAppearanceSection => CurrentSettingsSection == SettingsSection.Appearance;

    public bool IsLibrarySection => CurrentSettingsSection == SettingsSection.Library;

    public bool IsRecommendationsSection => CurrentSettingsSection == SettingsSection.Recommendations;

    public bool IsSubtitlesSection => CurrentSettingsSection == SettingsSection.Subtitles;

    public bool IsSegmentDetectionSection => CurrentSettingsSection == SettingsSection.SegmentDetection;

    public bool IsShortcutsSection => CurrentSettingsSection == SettingsSection.Shortcuts;

    public bool IsLifecycleSection => CurrentSettingsSection == SettingsSection.Lifecycle;

    public bool IsPrivacySection => CurrentSettingsSection == SettingsSection.Privacy;

    public bool IsUpdatesSection => CurrentSettingsSection == SettingsSection.Updates;

    public bool IsCreditsSection => CurrentSettingsSection == SettingsSection.Credits;

    /// <summary>
    /// The first-run surface, only while it is the first run: no folders yet, or a consent still
    /// owed for the folder just added. With a populated library the folders live in Settings and
    /// the grid owns the Library route — the redistribution the owner decided on 2026-08-23.
    /// </summary>
    public bool ShowsOnboarding =>
        Onboarding is { } onboarding
        && (onboarding.HasNoRoots || onboarding.InitialScanConsentRequired)
        && !IsAddingRoot;

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

    /// <summary>
    /// Whether the session's side column has anything in it at all.
    /// </summary>
    /// <remarks>
    /// The column is 320 px of the window and it used to be there whether or not a single one of its
    /// five panels existed — a file with one audio track, no markers and no other version left an
    /// empty rectangle taking a fifth of the picture's width. The prototype gives the column to a
    /// panel when there is one and gives the width back to the film when there is not.
    /// </remarks>
    /// <remarks>
    /// <b>Four and not five, and the subtitle panel is the one missing on purpose.</b> It is
    /// <c>Player?.Tracks is not null</c>, and so is half of <c>HasAudioPanel</c>, which is evaluated
    /// first — so the moment this chain reaches the subtitle term, the track list is already known
    /// to be absent and the term can only ever answer false. Written out it was a branch nothing
    /// could take, which the coverage gate is what noticed. The rule this tree follows for one of
    /// those is to make it reachable or delete it, never to write it an impossible test.
    /// </remarks>
    public bool HasPlayerPanels =>
        HasAudioPanel || HasVideoPanel || HasMarkerPanel || HasPlayerVersions;

    public bool HasVideoStatus => Player?.VideoStatus is not null;

    /// <summary>
    /// The four pills the prototype heads the player with, plus the fifth this application keeps.
    /// </summary>
    /// <remarks>
    /// The grouping is the prototype's and not this application's: audio tracks and the output
    /// device are one subject to a person choosing what they hear, so they head one pill together
    /// even though two view models answer for them. A pill with nothing behind it is not drawn —
    /// a file with a single audio track and no other version has fewer than five.
    /// </remarks>
    public bool HasAudioPanel => Player?.Tracks is not null || HasAudioOutput;

    public bool HasSubtitlePanel => Player?.Tracks is not null;

    public bool HasVideoPanel => HasVideoStatus;

    public bool HasMarkerPanel => HasMarkers || HasDetectedReview;

    /// <summary>Which panel the column is showing; <see cref="PlayerPanel.None"/> closes it.</summary>
    public PlayerPanel PlayerPanel
    {
        get => _playerPanel;
        private set
        {
            if (SetField(ref _playerPanel, value))
            {
                OnPropertyChanged(nameof(IsAudioPanelOpen));
                OnPropertyChanged(nameof(IsSubtitlePanelOpen));
                OnPropertyChanged(nameof(IsVideoPanelOpen));
                OnPropertyChanged(nameof(IsMarkerPanelOpen));
                OnPropertyChanged(nameof(IsVersionsPanelOpen));
                OnPropertyChanged(nameof(IsPlayerPanelOpen));
                OnPropertyChanged(nameof(IsPlayerColumnVisible));
                OnPropertyChanged(nameof(AudioPanelStateCue));
                OnPropertyChanged(nameof(SubtitlePanelStateCue));
                OnPropertyChanged(nameof(VideoPanelStateCue));
                OnPropertyChanged(nameof(MarkerPanelStateCue));
                OnPropertyChanged(nameof(VersionsPanelStateCue));
                _closePlayerPanel.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsAudioPanelOpen => _playerPanel is PlayerPanel.Audio;

    public bool IsSubtitlePanelOpen => _playerPanel is PlayerPanel.Subtitles;

    public bool IsVideoPanelOpen => _playerPanel is PlayerPanel.Video;

    public bool IsMarkerPanelOpen => _playerPanel is PlayerPanel.Markers;

    public bool IsVersionsPanelOpen => _playerPanel is PlayerPanel.Versions;

    /// <summary>Whether the column takes its 320 px at all.</summary>
    public bool IsPlayerPanelOpen => _playerPanel is not PlayerPanel.None;

    /// <summary>
    /// The glyph each pill carries beside its name, saying whether its panel is the open one.
    /// </summary>
    /// <remarks>
    /// The same pair the library's kind filter and the appearance pills already spend, for the same
    /// measured reason: in either high contrast dictionary AccentSubtleBrush and the resting fill
    /// resolve to the same white or the same black, so a pill that said "open" in colour alone would
    /// say nothing at all there.
    /// </remarks>
    public string AudioPanelStateCue => Cue(IsAudioPanelOpen);

    public string SubtitlePanelStateCue => Cue(IsSubtitlePanelOpen);

    public string VideoPanelStateCue => Cue(IsVideoPanelOpen);

    public string MarkerPanelStateCue => Cue(IsMarkerPanelOpen);

    public string VersionsPanelStateCue => Cue(IsVersionsPanelOpen);

    private static string Cue(bool selected) => selected ? "●" : "○";

    /// <summary>
    /// The badge beside the title: which session this is, and that only one engine is ever running.
    /// </summary>
    /// <remarks>
    /// The prototype writes «Sesión 1 · motor único activo» and this application can say the same
    /// thing truthfully: LibVLC is built once and one session at a time holds it, so the ordinal is
    /// how many times a session has been opened in this run rather than a handle to anything. It is
    /// counted here because the shell is what opens and closes them.
    /// </remarks>
    /// <summary>
    /// Whether everything that is not the picture is on screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The prototype does not do this — its player keeps its header and its transport standing the
    /// whole time, and that was checked in its source before any of this was written. It is a
    /// requirement of this application's own: a film is what the window is for while it is playing,
    /// and a title bar, a navigation rail, a session header and a side column around it are four
    /// surfaces about the application rather than about the film.
    /// </para>
    /// <para>
    /// <b>It comes back on a movement of the mouse or a key, and on nothing else — no timer puts it
    /// away again.</b> That is a decision with a cost written into it: a person who moves the mouse
    /// once keeps the chrome until they pause and play again. The alternative is a clock, and a clock
    /// here would be a second thing deciding what is on screen — one that no test can ask a question
    /// of without waiting for it, and that the autonomous walk would race against on every scene
    /// where it presses something during a real session.
    /// </para>
    /// </remarks>
    public bool IsChromeRevealed
    {
        get => _isChromeRevealed;
        private set
        {
            if (SetField(ref _isChromeRevealed, value))
            {
                OnPropertyChanged(nameof(IsPlayerColumnVisible));
            }
        }
    }

    /// <summary>The column is a panel somebody opened <b>and</b> chrome, so it answers to both.</summary>
    public bool IsPlayerColumnVisible => IsPlayerPanelOpen && _isChromeRevealed;

    /// <summary>Brings everything back, and does nothing at all when it is already there.</summary>
    public void RevealChrome()
    {
        if (_isChromeRevealed)
        {
            return;
        }

        IsChromeRevealed = true;
        _player?.Player.RevealControls();
    }

    /// <summary>
    /// Takes everything but the picture away. The transport goes with it, through the pair the
    /// player has always declared and nothing ever called.
    /// </summary>
    public void HideChrome()
    {
        if (!_isChromeRevealed)
        {
            return;
        }

        IsChromeRevealed = false;
        _player?.Player.HideControls();
    }

    public string PlayerSessionBadge => ShowText.Format(
        "PlayerSessionBadge",
        "Session {0} · single active engine",
        _playerSessionOrdinal.ToString(System.Globalization.CultureInfo.CurrentCulture));

    public bool HasLooseFile => Player?.LooseFile is not null;

    public bool IsSettingsVisible => CurrentRoute == AppRoute.Settings;

    public bool IsLibraryVisible => CurrentRoute == AppRoute.Library;

    public bool IsHomeVisible => CurrentRoute == AppRoute.Home && Home is not null;

    public bool IsDuplicatesVisible => CurrentRoute == AppRoute.Duplicates;

    /// <summary>Copias lives in Settings now; its stack shows on its own index entry.</summary>
    public bool IsBackupsSection => CurrentSettingsSection == SettingsSection.Backups;

    public bool IsReviewVisible => CurrentRoute == AppRoute.Review && ReviewInbox is not null;

    /// <summary>True while a session is on screen; it covers whatever route is underneath it.</summary>
    public bool IsPlayerVisible => Player is not null;

    public bool IsPrimaryContentVisible =>
        !IsSettingsVisible
        && !IsLibraryVisible
        && !IsHomeVisible
        && !IsDuplicatesVisible
        && !IsReviewVisible
        && !IsPlayerVisible;

    /// <summary>Opens one media file and shows everything that session puts on screen.</summary>
    /// <summary>
    /// How many proposals are waiting in the review inbox, which the rail draws over its icon.
    /// </summary>
    /// <remarks>
    /// The prototype puts a number there and this rail had none, which is the last of the eight
    /// differences the owner's comparison turned up. What made it worth doing rather than faking:
    /// <c>ReviewInboxChanged</c> has been published by <c>ResolveMatch</c> and <c>RejectMatch</c>
    /// since they were written and <b>subscribed to by nobody</b> — the whole application event bus
    /// had a publisher and no listener in the product. The badge is the first thing that listens.
    /// </remarks>
    public int ReviewPendingCount
    {
        get => _reviewPendingCount;
        private set
        {
            if (_reviewPendingCount == value)
            {
                return;
            }

            _reviewPendingCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasReviewPending));
            OnPropertyChanged(nameof(ReviewPendingText));
        }
    }

    /// <summary>Whether there is a badge at all: an empty inbox draws no zero.</summary>
    public bool HasReviewPending => _reviewPendingCount > 0;

    /// <summary>
    /// The number as the badge writes it, which stops at what the inbox can count.
    /// </summary>
    /// <remarks>
    /// <c>GetReviewInbox</c> refuses a page over a hundred, so a hundred and one means "at least
    /// this many" and the badge says so rather than printing a number it did not count.
    /// </remarks>
    public string ReviewPendingText => _reviewPendingCount > 100
        ? "99+"
        : _reviewPendingCount.ToString(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>Takes a fresh count, from whoever counted it.</summary>
    public void ApplyReviewPendingCount(int pending) =>
        ReviewPendingCount = Math.Max(0, pending);

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
    /// Goes to the surface a media folder is added on, with the form ready to be typed into.
    /// </summary>
    /// <remarks>
    /// Two halves, and the second is the one that is easy to leave out. Navigating alone would make
    /// this a second Biblioteca; what the name promises is that the form is ready, and only the
    /// clearing delivers that.
    /// </remarks>
    public void BeginAddMedia()
    {
        Onboarding?.BeginAdd();
        IsAddingRoot = true;
    }

    /// <summary>Puts the dialog away; the form's own clearing happens on the next open.</summary>
    public void CloseAddMedia() => IsAddingRoot = false;

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
            EditorTab = 0;
        }
    }

    public async Task OpenRenamePreviewAsync(CancellationToken cancellationToken = default)
    {
        if (_surfaces.OpenRename is { } open && SelectedTitleId is { } titleId)
        {
            Rename = await open(titleId, cancellationToken).ConfigureAwait(true);
            EditorTab = 1;
        }
    }

    /// <summary>
    /// Loads the versions of the open title and moves to the duplicates destination, which is the
    /// rail's own door to the same comparison — one door for the card and one for the overview, and
    /// both open the identical surface.
    /// </summary>
    public Task OpenDuplicatesAsync(CancellationToken cancellationToken = default) =>
        SelectedTitleId is { } titleId
            ? OpenDuplicatesForAsync(titleId, cancellationToken)
            : Task.CompletedTask;

    /// <summary>The shared half: one title's comparison, on the duplicates route.</summary>
    public async Task OpenDuplicatesForAsync(TitleId titleId, CancellationToken cancellationToken = default)
    {
        if (_surfaces.OpenDuplicates is { } open)
        {
            Duplicates = await open(titleId, cancellationToken).ConfigureAwait(true);
            if (Duplicates is not null)
            {
                _navigationService.Navigate(AppRoute.Duplicates);
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
        // The pieces of ShowsOnboarding that live on the form: the dialog half is this model's own.
        if (args.PropertyName is nameof(RootOnboardingViewModel.HasNoRoots)
            or nameof(RootOnboardingViewModel.HasRoots)
            or nameof(RootOnboardingViewModel.InitialScanConsentRequired))
        {
            OnPropertyChanged(nameof(ShowsOnboarding));
        }

        // A folder accepted from the dialog closes it: the form's job is done, and what is owed
        // next - the first scan's consent - is asked by the surface the route shows.
        if (args.PropertyName == nameof(RootOnboardingViewModel.AddedRoot)
            && Onboarding is { AddedRoot: not null }
            && IsAddingRoot)
        {
            CloseAddMedia();
            if (Library is { } refreshed)
            {
                await refreshed.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            }
        }

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
        OnPropertyChanged(nameof(IsDuplicatesVisible));
        OnPropertyChanged(nameof(IsReviewVisible));
        OnPropertyChanged(nameof(IsPrimaryContentVisible));
        if (route == AppRoute.Duplicates && DuplicatesOverview is { } duplicates)
        {
            // The list is read on every visit: a group confirmed away in the review would otherwise
            // keep its row until a restart.
            await duplicates.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        }

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

    /// <summary>Opens a panel, or closes the column when the panel asked for is already open.</summary>
    private void TogglePlayerPanel(PlayerPanel panel) =>
        PlayerPanel = _playerPanel == panel ? PlayerPanel.None : panel;

    /// <summary>
    /// The picture starting is what takes the chrome away; anything else brings it back.
    /// </summary>
    /// <remarks>
    /// Paused, stopped and failed all reveal, and that is not three cases of one rule: a paused film
    /// is somebody who has stopped watching for a moment and wants the controls they paused with,
    /// and a failed one has a recovery surface that is the only thing left worth reading.
    /// </remarks>
    private void OnPlayerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(PlayerViewModel.IsPlaying) || sender is not PlayerViewModel player)
        {
            return;
        }

        ApplyChromeFor(player.IsPlaying);
    }

    private void ApplyChromeFor(bool isPlaying)
    {
        if (isPlaying)
        {
            HideChrome();
        }
        else
        {
            RevealChrome();
        }
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
    /// The five pills' one command: it takes the panel it is for and never asks whether it may run.
    /// </summary>
    /// <remarks>
    /// No <c>CanExecute</c> that changes, and that is deliberate rather than unfinished: a pill is
    /// drawn only when the panel behind it has something in it, so a pill that exists can always be
    /// pressed. Making it also disable itself would be a second answer to the same question, and the
    /// two would disagree the first time one of them was updated alone.
    /// </remarks>
    private sealed class PlayerPanelCommand(Action<PlayerPanel> open) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => parameter is PlayerPanel;

        public void Execute(object? parameter)
        {
            if (parameter is PlayerPanel panel)
            {
                open(panel);
            }
        }
    }
}
