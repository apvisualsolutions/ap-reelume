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
    private PlaybackState _state = PlaybackState.Idle;
    private PlaybackFailureCode? _failureCode;
    private IReadOnlyList<PlaybackRecoveryAction> _recoveryActions = [];
    private IReadOnlyList<MediaTrack> _tracks = [];
    private MediaFileId _mediaFileId;
    private string _mediaPath = string.Empty;
    private bool _externalLaunchFailed;
    private bool _areControlsRevealed = true;

    public PlayerViewModel(
        IPlaybackSessionCoordinator coordinator,
        IVideoFrameSource? frameSource = null,
        IExternalPlaybackLauncher? externalLauncher = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _externalLauncher = externalLauncher;
        FrameSource = frameSource;
        PauseCommand = new AsyncRelayCommand(() => _coordinator.PauseAsync(CancellationToken.None), () => CanPause);
        ResumeCommand = new AsyncRelayCommand(() => _coordinator.ResumeAsync(CancellationToken.None), () => CanResume);
        StopCommand = new AsyncRelayCommand(() => _coordinator.StopAsync(CancellationToken.None), () => CanStop);
        RetryCommand = new AsyncRelayCommand(RetryAsync, () => CanRetry);
        OpenExternallyCommand = new AsyncRelayCommand(OpenExternallyAsync, () => CanOpenExternally);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand PauseCommand { get; }

    public ICommand ResumeCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand RetryCommand { get; }

    public ICommand OpenExternallyCommand { get; }

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

    public bool CanChooseAnotherVersion => _recoveryActions.Contains(PlaybackRecoveryAction.ChooseAnotherVersion);

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
    public void ApplySessionState(PlaybackState state, PlaybackFailure? failure)
    {
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
        foreach (var command in new[]
        {
            PauseCommand,
            ResumeCommand,
            StopCommand,
            RetryCommand,
            OpenExternallyCommand,
        })
        {
            (command as AsyncRelayCommand)?.RaiseCanExecuteChanged();
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
