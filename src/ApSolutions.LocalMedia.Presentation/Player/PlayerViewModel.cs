// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Drives the embedded player through the single-session coordinator. It never holds an engine
/// object: the native surface is handed over as an opaque handle and every transition is a use case.
/// </summary>
public sealed class PlayerViewModel : INotifyPropertyChanged
{
    private readonly IPlaybackSessionCoordinator _coordinator;
    private readonly IExternalPlaybackLauncher? _externalLauncher;
    private readonly Func<bool>? _alternativesExist;
    private PlaybackState _state = PlaybackState.Idle;
    private PlaybackFailureCode? _failureCode;
    private IReadOnlyList<PlaybackRecoveryAction> _recoveryActions = [];
    private IReadOnlyList<MediaTrack> _tracks = [];
    private MediaFileId _mediaFileId;
    private string _mediaPath = string.Empty;
    private bool _externalLaunchFailed;
    private bool _areControlsRevealed = true;
    private Func<PlaybackMode, Task>? _modeHandler;
    private bool _isCompact;
    private bool _isFullscreen;

    /// <param name="alternativesExist">
    /// Whether the content being played has other versions catalogued, asked rather than stored.
    /// </param>
    /// <remarks>
    /// It is a delegate and not a value because the session is assembled in that order: the player is
    /// built before the version group has been read, so a value passed here would always be the one
    /// that was true before anybody looked. Absent, this model answers that there are none, which is
    /// what a player with no catalogue behind it should say.
    /// </remarks>
    public PlayerViewModel(
        IPlaybackSessionCoordinator coordinator,
        IVideoFrameSource? frameSource = null,
        IExternalPlaybackLauncher? externalLauncher = null,
        Func<bool>? alternativesExist = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _externalLauncher = externalLauncher;
        _alternativesExist = alternativesExist;
        FrameSource = frameSource;
        PauseCommand = new AsyncRelayCommand(() => _coordinator.PauseAsync(CancellationToken.None), () => CanPause);
        ResumeCommand = new AsyncRelayCommand(() => _coordinator.ResumeAsync(CancellationToken.None), () => CanResume);
        StopCommand = new AsyncRelayCommand(() => _coordinator.StopAsync(CancellationToken.None), () => CanStop);
        RetryCommand = new AsyncRelayCommand(RetryAsync, () => CanRetry);
        OpenExternallyCommand = new AsyncRelayCommand(OpenExternallyAsync, () => CanOpenExternally);
        TogglePlaybackCommand = new AsyncRelayCommand(TogglePlaybackAsync, () => CanPause || CanResume);
        ToggleFullscreenCommand = new AsyncRelayCommand(
            () => ChangeModeAsync(PlaybackMode.Fullscreen),
            () => ModeHandler is not null);
        TogglePictureInPictureCommand = new AsyncRelayCommand(
            () => ChangeModeAsync(PlaybackMode.Mini),
            () => ModeHandler is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand PauseCommand { get; }

    public ICommand ResumeCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand RetryCommand { get; }

    public ICommand OpenExternallyCommand { get; }

    /// <summary>
    /// Pause and resume behind one control, for a window with no room for two.
    /// </summary>
    /// <remarks>
    /// The mini player has five buttons and 480 logical pixels, so it asks the one question a person
    /// asks - keep going, or stop for a moment - rather than showing two buttons of which exactly one
    /// is ever enabled. What it does is decided by the state, not by a flag this class would then
    /// have to keep in step with it.
    /// </remarks>
    public ICommand TogglePlaybackCommand { get; }

    /// <summary>Decoded frames the view draws; never an engine object.</summary>
    public IVideoFrameSource? FrameSource { get; }

    /// <summary>Speed, skips, volume, and the boost warning; null until a session owns them.</summary>
    public TransportControlsViewModel? Transport { get; init; }

    /// <summary>
    /// Resolves one key gesture into a session action, or refuses it. The composition root installs
    /// it while a session exists; the view asks synchronously, so a handled key never also scrolls.
    /// </summary>
    public Func<Avalonia.Input.KeyGesture, bool>? GestureHandler { get; set; }

    /// <summary>
    /// Changes which window the picture is in. Installed by the composition root, like the gestures.
    /// </summary>
    /// <remarks>
    /// The transport bar needs the two mode buttons the owner asked for — full screen and
    /// picture-in-picture, «en la barra de controles» — and the bar travels: the same control is
    /// handed to the mini window, where there is no shell above it to reach up to. So the surface
    /// carries the commands and the composition fills them, exactly as it fills the gestures.
    /// </remarks>
    public Func<PlaybackMode, Task>? ModeHandler
    {
        get => _modeHandler;
        set
        {
            _modeHandler = value;
            (ToggleFullscreenCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (TogglePictureInPictureCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Whether the picture is in the small window, which carries a transport of its own.
    /// </summary>
    /// <remarks>
    /// The stage travels: the very control the shell shows is the one the picture-in-picture window
    /// is handed, so the full bar went with it and sat under the five buttons that window already
    /// draws. This is what the bar stands down for, and it comes back the moment the picture does.
    /// </remarks>
    public bool IsCompact
    {
        get => _isCompact;
        set
        {
            SetField(ref _isCompact, value);
            OnPropertyChanged(nameof(HasFullTransport));
        }
    }

    /// <summary>True while the picture's own transport bar is the one a person uses.</summary>
    public bool HasFullTransport => !_isCompact;

    /// <summary>
    /// Whether the picture has the whole screen, which is what the transport's mode button draws.
    /// </summary>
    /// <remarks>
    /// The prototype swaps that button's picture the instant the mode changes —
    /// <c>icon(mode === 'fullscreen' ? 'exitfull' : 'full')</c> — and this application drew the
    /// entering arrows in both states: a control saying the same thing whatever it had done, which is
    /// the exact defect the mute button was found with on 2026-08-25 and fixed for. <c>exitfull</c>
    /// was already in the dictionary and was drawn by the mini window alone.
    ///
    /// <para>
    /// Pushed in from <c>ShellView</c> beside <see cref="IsCompact"/> rather than read out of the
    /// shell, and for the same two reasons that one is: the bar travels to a window with no shell
    /// above it, and the mode is applied while the stage is between two windows.
    /// </para>
    /// </remarks>
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => SetField(ref _isFullscreen, value);
    }

    /// <summary>Puts the picture on the whole screen, and takes it back off.</summary>
    public ICommand ToggleFullscreenCommand { get; }

    /// <summary>Sends the picture to the small always-on-top window, and brings it back.</summary>
    public ICommand TogglePictureInPictureCommand { get; }

    /// <summary>
    /// Whether the transport bar is shown. Hiding it only changes its opacity, so the controls stay
    /// in the focus and automation tree and keyboard users never lose them.
    /// </summary>
    public bool AreControlsRevealed
    {
        get => _areControlsRevealed;
        private set => SetField(ref _areControlsRevealed, value);
    }

    public double ControlsOpacity => AreControlsRevealed ? 0.92 : 0.0;

    public void RevealControls() => SetControlsRevealed(revealed: true);

    public void HideControls() => SetControlsRevealed(revealed: false);

    public string MediaPath
    {
        get => _mediaPath;
        private set => SetField(ref _mediaPath, value);
    }

    public bool IsIdle => _state == PlaybackState.Idle;

    public bool IsOpening => _state == PlaybackState.Opening;

    public bool IsPlaying => _state == PlaybackState.Playing;

    public bool IsPaused => _state == PlaybackState.Paused;

    public bool IsStopped => _state == PlaybackState.Stopped;

    public bool HasFailed => _state == PlaybackState.Failed;

    public bool CanPause => _state == PlaybackState.Playing;

    public bool CanResume => _state == PlaybackState.Paused;

    public bool CanStop => PlaybackStatePolicy.IsActive(_state);

    public bool FileWasNotFound => _failureCode == PlaybackFailureCode.FileNotFound;

    public bool OpenFailed => _failureCode == PlaybackFailureCode.OpenFailed;

    public bool EngineWasUnavailable => _failureCode == PlaybackFailureCode.EngineUnavailable;

    public bool CodecIsUnsupported => _failureCode == PlaybackFailureCode.UnsupportedCodec;

    public bool MediaWasCorrupted => _failureCode == PlaybackFailureCode.CorruptedMedia;

    public bool HasNoPlayableTrack => _failureCode == PlaybackFailureCode.NoPlayableTrack;

    /// <summary>The media plays but carries no audible track, which the shell announces in text.</summary>
    public bool HasNoAudioTrack => PlaybackDiagnosticsPolicy.IsMissingAudio(_tracks);

    public bool CanRetry =>
        _recoveryActions.Contains(PlaybackRecoveryAction.Retry) && !string.IsNullOrWhiteSpace(MediaPath);

    /// <summary>
    /// True when the domain offers this recovery <b>and</b> there is another version to move to.
    /// </summary>
    /// <remarks>
    /// The domain decides by failure code: five of the seven offer this, which makes it the most
    /// offered of the three. It knows nothing about whether the title has a version group, so on its
    /// own it said "choose another version of the same content" to somebody with one file - which is
    /// the ordinary case, because most files have no alternative version at all.
    /// <para>
    /// Its two siblings both check that the action can actually be carried out - <c>CanRetry</c> wants
    /// a path, <c>CanOpenExternally</c> wants a path and a launcher - and they are the two that have a
    /// button. This one had neither the check nor the button.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The rows the recovery button's flyout shows. The composition hands over the same object the
    /// side column binds, so choosing in either place is the same switch. Absent when the title has
    /// no group - and <see cref="CanChooseAnotherVersion"/> is false then, so no button offers an
    /// empty list.
    /// </summary>
    public PlayerVersionsViewModel? Versions
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Versions));
        }
    }

    public bool CanChooseAnotherVersion =>
        _recoveryActions.Contains(PlaybackRecoveryAction.ChooseAnotherVersion)
        && _alternativesExist?.Invoke() == true;

    public bool CanOpenExternally =>
        _externalLauncher is not null
        && _recoveryActions.Contains(PlaybackRecoveryAction.OpenExternally)
        && !string.IsNullOrWhiteSpace(MediaPath);

    /// <summary>Set when the operating system had no handler for the file.</summary>
    public bool ExternalLaunchFailed => _externalLaunchFailed;

    /// <summary>Opens one catalogued file in the single embedded session.</summary>
    public async Task OpenAsync(
        MediaFileId mediaFileId,
        string path,
        TimeSpan startPosition = default,
        CancellationToken cancellationToken = default)
    {
        _mediaFileId = mediaFileId;
        MediaPath = path;
        _failureCode = null;
        _recoveryActions = [];
        _tracks = [];
        _externalLaunchFailed = false;
        UpdateState(PlaybackState.Opening);
        try
        {
            _ = await _coordinator.StartAsync(new PlaybackRequest(mediaFileId, path, startPosition), cancellationToken)
                .ConfigureAwait(true);
            UpdateState(PlaybackState.Playing);
        }
        catch (PlaybackFailureException exception)
        {
            Report(exception.Failure);
            UpdateState(PlaybackState.Failed);
        }
    }

    /// <summary>Applied when the coordinator reports a transition for the active session.</summary>
    /// <remarks>
    /// A failure is terminal for the attempt that produced it, and <see cref="PlaybackStatePolicy"/>
    /// already says so: the only move out of <see cref="PlaybackState.Failed"/> is reopening. The
    /// engine does not know that — LibVLC tears the media down after refusing it and reports the stop
    /// a moment later — and applying that stop erased the recovery a person was in the middle of
    /// reading. It showed up as a flake before it showed up as a bug: the physical walk waited a full
    /// minute for a failure that had already happened and been overwritten.
    ///
    /// Only the engine's own end-of-session states are ignored, and only after a failure. Reopening,
    /// a second failure with another reason, and going idle all still apply: those come from this
    /// application deciding something, not from LibVLC finishing its clean-up.
    /// </remarks>
    public void ApplySessionState(PlaybackState state, PlaybackFailure? failure)
    {
        if (_state == PlaybackState.Failed && state is PlaybackState.Stopped or PlaybackState.Ended)
        {
            return;
        }

        Report(failure);
        UpdateState(state);
    }

    /// <summary>Records the tracks of the active media so absent audio can be announced.</summary>
    public void ApplyTracks(IReadOnlyList<MediaTrack> tracks)
    {
        _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
        OnPropertyChanged(nameof(HasNoAudioTrack));
    }

    private void Report(PlaybackFailure? failure)
    {
        _failureCode = failure?.Code;
        _recoveryActions = failure?.RecoveryActions ?? [];
    }

    private void SetControlsRevealed(bool revealed)
    {
        AreControlsRevealed = revealed;
        OnPropertyChanged(nameof(ControlsOpacity));
    }

    private Task RetryAsync() => OpenAsync(_mediaFileId, MediaPath);

    private async Task OpenExternallyAsync()
    {
        if (_externalLauncher is not { } launcher)
        {
            return;
        }

        _externalLaunchFailed = !await launcher.TryLaunchAsync(MediaPath, CancellationToken.None)
            .ConfigureAwait(true);
        OnPropertyChanged(nameof(ExternalLaunchFailed));
    }

    private Task ChangeModeAsync(PlaybackMode mode) =>
        ModeHandler is { } handler ? handler(mode) : Task.CompletedTask;

    private Task TogglePlaybackAsync() =>
        CanPause
            ? _coordinator.PauseAsync(CancellationToken.None)
            : _coordinator.ResumeAsync(CancellationToken.None);

    private void UpdateState(PlaybackState state)
    {
        _state = state;
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsOpening));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(HasFailed));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(FileWasNotFound));
        OnPropertyChanged(nameof(OpenFailed));
        OnPropertyChanged(nameof(EngineWasUnavailable));
        OnPropertyChanged(nameof(CodecIsUnsupported));
        OnPropertyChanged(nameof(MediaWasCorrupted));
        OnPropertyChanged(nameof(HasNoPlayableTrack));
        OnPropertyChanged(nameof(HasNoAudioTrack));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(CanChooseAnotherVersion));
        OnPropertyChanged(nameof(CanOpenExternally));
        OnPropertyChanged(nameof(ExternalLaunchFailed));

        // A button does not watch these properties. It asks its command, once, and then only asks
        // again when the command says so - so without this the transport's enabled state freezes at
        // whatever the first evaluation gave. Measured on 2026-08-15: pressing Pause worked and
        // Resume stayed disabled for good, which is a session a mouse can stop and never restart.
        // The keyboard was unaffected, because the player answers keys itself, and that is why it
        // survived this long.
        // Cast hard on purpose: every one of the six is constructed as AsyncRelayCommand above,
        // and one that stopped being it would stop being refreshed - which is the 2026-08-15
        // defect returning in silence. A throw is the better failure.
        foreach (var command in new[]
        {
            (AsyncRelayCommand)PauseCommand,
            (AsyncRelayCommand)ResumeCommand,
            (AsyncRelayCommand)StopCommand,
            (AsyncRelayCommand)RetryCommand,
            (AsyncRelayCommand)OpenExternallyCommand,
            (AsyncRelayCommand)TogglePlaybackCommand,
        })
        {
            command.RaiseCanExecuteChanged();
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
