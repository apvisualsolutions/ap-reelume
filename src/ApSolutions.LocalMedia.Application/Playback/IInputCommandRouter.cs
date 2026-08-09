namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>Every action the player exposes to any input method.</summary>
public enum PlaybackInputCommand
{
    PlayPause,
    Stop,
    SkipBackward,
    SkipForward,
    VolumeUp,
    VolumeDown,
    ToggleMute,
    ToggleFullscreen,
    ToggleMiniPlayer,
    ExitOverlayMode,
}

/// <summary>Where a command came from, so the same action is never applied twice.</summary>
public enum InputOrigin
{
    Keyboard,
    Mouse,
    MediaKey,
}

/// <summary>
/// Delivers a hardware media key to the application. Implemented by the Windows host; the router is
/// tested against a fake source so the routing rules do not need real keys.
/// </summary>
public interface IMediaKeySource
{
    event EventHandler<PlaybackInputCommand>? CommandReceived;

    /// <summary>Starts listening. Registering only while a session exists is the caller's rule.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops listening and releases every registration.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>True while the source holds system registrations.</summary>
    bool IsListening { get; }
}

/// <summary>
/// Resolves keyboard, mouse, and media-key input into one action each. A command that arrives from
/// two origins within the coalescing window is executed once, which is what stops a media key that
/// the window also sees as a key press from toggling playback twice.
/// </summary>
public interface IInputCommandRouter
{
    /// <summary>Commands that were actually executed, in order, for verification.</summary>
    IReadOnlyList<PlaybackInputCommand> Executed { get; }

    Task<bool> DispatchAsync(
        PlaybackInputCommand command,
        InputOrigin origin,
        CancellationToken cancellationToken = default);
}
