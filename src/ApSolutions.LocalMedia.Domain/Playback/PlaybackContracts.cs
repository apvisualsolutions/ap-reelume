// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>Lifecycle of the single embedded playback session.</summary>
public enum PlaybackState
{
    Idle,
    Opening,
    Playing,
    Paused,
    Stopped,

    /// <summary>
    /// The media played to its own end. Distinct from <see cref="Stopped"/> on purpose: stopping is
    /// somebody's decision, ending is the moment the next episode may be offered.
    /// </summary>
    Ended,
    Failed,
}

/// <summary>Kind of elementary stream exposed by the engine.</summary>
public enum MediaTrackKind
{
    Video,
    Audio,
    Subtitle,
}

/// <summary>Localizable reason why a playback request could not be honoured.</summary>
public enum PlaybackFailureCode
{
    FileNotFound,
    OpenFailed,
    EngineUnavailable,

    /// <summary>The container parsed but no decoder exists for the streams it announces.</summary>
    UnsupportedCodec,

    /// <summary>The file could not be parsed into playable streams.</summary>
    CorruptedMedia,

    /// <summary>The file parsed but announces no audio or video the engine could present.</summary>
    NoPlayableTrack,

    /// <summary>The media asks for a capability this release deliberately does not implement.</summary>
    UnsupportedCapability,
}

/// <summary>
/// What the person can do next. Every action preserves the file and the catalogue entity; the
/// enumeration deliberately contains no destructive option.
/// </summary>
public enum PlaybackRecoveryAction
{
    Retry,
    ChooseAnotherVersion,
    OpenExternally,
}

/// <summary>An elementary stream identified by stable attributes rather than a volatile index.</summary>
public sealed record MediaTrack(
    string Id,
    MediaTrackKind Kind,
    string? Language = null,
    string? Description = null,
    int? Channels = null,
    string? Codec = null,
    bool IsExternal = false);

/// <summary>A playback failure expressed in domain terms so the shell can offer a recovery action.</summary>
public sealed record PlaybackFailure(PlaybackFailureCode Code, string Detail)
{
    /// <summary>The non-destructive actions the shell offers for this failure.</summary>
    public IReadOnlyList<PlaybackRecoveryAction> RecoveryActions =>
        PlaybackDiagnosticsPolicy.RecoveryActionsFor(Code);
}

/// <summary>What the engine observed after parsing a file, expressed without engine types.</summary>
public sealed record MediaOpenObservation(bool Parsed, IReadOnlyList<MediaTrack> Tracks);

/// <summary>
/// Turns what the engine observed into an actionable domain failure. The rules live here, not in the
/// adapter, so every engine reports the same diagnosis for the same observation.
/// </summary>
public static class PlaybackDiagnosticsPolicy
{
    /// <summary>Returns the failure to report, or null when the media is playable.</summary>
    public static PlaybackFailure? Diagnose(MediaOpenObservation observation, string detail)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!observation.Parsed || observation.Tracks.Count == 0)
        {
            return new PlaybackFailure(PlaybackFailureCode.CorruptedMedia, detail);
        }

        var presentable = observation.Tracks
            .Where(track => track.Kind is MediaTrackKind.Video or MediaTrackKind.Audio)
            .ToArray();
        if (presentable.Length == 0)
        {
            return new PlaybackFailure(PlaybackFailureCode.NoPlayableTrack, detail);
        }

        return presentable.All(track => string.IsNullOrWhiteSpace(track.Codec))
            ? new PlaybackFailure(PlaybackFailureCode.UnsupportedCodec, detail)
            : null;
    }

    /// <summary>True when the media plays but carries no audio the person can hear.</summary>
    public static bool IsMissingAudio(IReadOnlyList<MediaTrack> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        return tracks.Any(track => track.Kind == MediaTrackKind.Video)
            && !tracks.Any(track => track.Kind == MediaTrackKind.Audio);
    }

    /// <summary>The offered actions never remove or rewrite the media.</summary>
    public static IReadOnlyList<PlaybackRecoveryAction> RecoveryActionsFor(PlaybackFailureCode code) => code switch
    {
        PlaybackFailureCode.FileNotFound =>
            [PlaybackRecoveryAction.Retry, PlaybackRecoveryAction.ChooseAnotherVersion],
        PlaybackFailureCode.EngineUnavailable =>
            [PlaybackRecoveryAction.Retry],
        PlaybackFailureCode.UnsupportedCodec or PlaybackFailureCode.CorruptedMedia
            or PlaybackFailureCode.NoPlayableTrack or PlaybackFailureCode.UnsupportedCapability =>
            [PlaybackRecoveryAction.ChooseAnotherVersion, PlaybackRecoveryAction.OpenExternally],
        _ =>
        [
            PlaybackRecoveryAction.Retry,
            PlaybackRecoveryAction.ChooseAnotherVersion,
            PlaybackRecoveryAction.OpenExternally,
        ],
    };
}

/// <summary>Thrown when an engine cannot honour a request; always carries an actionable domain code.</summary>
public sealed class PlaybackFailureException : Exception
{
    public PlaybackFailureException()
        : this(new PlaybackFailure(PlaybackFailureCode.OpenFailed, "Playback failed."))
    {
    }

    public PlaybackFailureException(string message)
        : this(new PlaybackFailure(PlaybackFailureCode.OpenFailed, message))
    {
    }

    public PlaybackFailureException(string message, Exception innerException)
        : base(message, innerException) =>
        Failure = new PlaybackFailure(PlaybackFailureCode.OpenFailed, message);

    public PlaybackFailureException(PlaybackFailure failure)
        : base(Describe(failure)) =>
        Failure = failure;

    public PlaybackFailureException(PlaybackFailure failure, Exception innerException)
        : base(Describe(failure), innerException) =>
        Failure = failure;

    /// <summary>The domain failure that callers present to the person using the application.</summary>
    public PlaybackFailure Failure { get; }

    private static string Describe(PlaybackFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return $"{failure.Code}: {failure.Detail}";
    }
}

/// <summary>What the engine was asked to do and what it actually achieved for the active media.</summary>
public sealed record PlaybackCapabilities(
    bool HardwareAccelerationRequested,
    bool HardwareAccelerationActive,
    HdrFormat SourceHdr = HdrFormat.None,
    bool DisplaySupportsHdr = false,
    VideoOutputPath OutputPath = VideoOutputPath.Sdr);

/// <summary>An explicit instruction to open one catalogued media file in the embedded engine.</summary>
public sealed record PlaybackRequest
{
    public PlaybackRequest(
        MediaFileId mediaFileId,
        string path,
        TimeSpan startPosition = default,
        bool useHardwareAcceleration = true,
        HdrFormat sourceHdr = HdrFormat.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(startPosition, TimeSpan.Zero);
        MediaFileId = mediaFileId;
        Path = path;
        StartPosition = startPosition;
        UseHardwareAcceleration = useHardwareAcceleration;
        SourceHdr = sourceHdr;
    }

    public MediaFileId MediaFileId { get; }

    public string Path { get; }

    public TimeSpan StartPosition { get; }

    public bool UseHardwareAcceleration { get; }

    /// <summary>
    /// What the catalogue already knows about the picture. LibVLC 3 does not expose transfer
    /// characteristics on its tracks, so the caller states what the media probe read.
    /// </summary>
    public HdrFormat SourceHdr { get; }
}

/// <summary>An immutable observation of the engine taken without mutating it.</summary>
public sealed record PlaybackSnapshot
{
    private PlaybackSnapshot(
        PlaybackState state,
        TimeSpan position,
        TimeSpan? duration,
        IReadOnlyList<MediaTrack> tracks,
        PlaybackFailure? failure,
        string? activeAudioTrackId,
        string? activeSubtitleTrackId)
    {
        State = state;
        Position = position;
        Duration = duration;
        Tracks = tracks;
        Failure = failure;
        ActiveAudioTrackId = activeAudioTrackId;
        ActiveSubtitleTrackId = activeSubtitleTrackId;
    }

    public PlaybackState State { get; }

    public TimeSpan Position { get; }

    public TimeSpan? Duration { get; }

    public IReadOnlyList<MediaTrack> Tracks { get; }

    public PlaybackFailure? Failure { get; }

    /// <summary>
    /// The audio track the engine is actually playing, or null when the kind is off or unknown.
    /// </summary>
    /// <remarks>
    /// What is announced and what is <em>in force</em> are different questions, and until 2026-08-25
    /// nothing could ask the second one. It matters because the engine chooses on its own when
    /// nobody has stored a preference — a container names a default track and LibVLC honours it —
    /// so a surface that assumed «nothing stored means nothing playing» showed the wrong answer.
    /// </remarks>
    public string? ActiveAudioTrackId { get; }

    /// <summary>The subtitle track in force, or null when subtitles are off.</summary>
    public string? ActiveSubtitleTrackId { get; }

    /// <summary>Creates a snapshot whose position always stays inside the observed duration.</summary>
    public static PlaybackSnapshot Create(
        PlaybackState state,
        TimeSpan position,
        TimeSpan? duration,
        IReadOnlyList<MediaTrack> tracks,
        PlaybackFailure? failure = null,
        string? activeAudioTrackId = null,
        string? activeSubtitleTrackId = null)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        var clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        if (duration is { } observed && clamped > observed)
        {
            clamped = observed;
        }

        return new PlaybackSnapshot(
            state,
            clamped,
            duration,
            tracks,
            failure,
            activeAudioTrackId,
            activeSubtitleTrackId);
    }
}

/// <summary>The approved lifecycle rules shared by every engine implementation.</summary>
public static class PlaybackStatePolicy
{
    /// <summary>A session holds engine resources while it is opening, playing, or paused.</summary>
    public static bool IsActive(PlaybackState state) =>
        state is PlaybackState.Opening or PlaybackState.Playing or PlaybackState.Paused;

    /// <summary>Playback never resumes without reopening the media.</summary>
    public static bool CanTransition(PlaybackState from, PlaybackState to) => (from, to) switch
    {
        (PlaybackState.Idle, PlaybackState.Opening) => true,
        (PlaybackState.Stopped, PlaybackState.Opening) => true,
        (PlaybackState.Failed, PlaybackState.Opening) => true,
        (PlaybackState.Opening, PlaybackState.Playing) => true,
        (PlaybackState.Opening, PlaybackState.Failed) => true,
        (PlaybackState.Opening, PlaybackState.Stopped) => true,
        (PlaybackState.Playing, PlaybackState.Paused) => true,
        (PlaybackState.Paused, PlaybackState.Playing) => true,
        (PlaybackState.Playing, PlaybackState.Stopped) => true,
        (PlaybackState.Paused, PlaybackState.Stopped) => true,
        (PlaybackState.Playing, PlaybackState.Failed) => true,
        (PlaybackState.Paused, PlaybackState.Failed) => true,
        _ => false,
    };
}

/// <summary>Raised whenever the engine moves between two lifecycle states.</summary>
public sealed class PlaybackStateChangedEventArgs(PlaybackState previousState, PlaybackState currentState) : EventArgs
{
    public PlaybackState PreviousState { get; } = previousState;

    public PlaybackState CurrentState { get; } = currentState;
}

/// <summary>Raised as the engine advances through the active media.</summary>
public sealed class PlaybackPositionChangedEventArgs(TimeSpan position, TimeSpan? duration) : EventArgs
{
    public TimeSpan Position { get; } = position;

    public TimeSpan? Duration { get; } = duration;
}

/// <summary>Raised when the engine cannot continue with the active media.</summary>
public sealed class PlaybackFailureEventArgs(PlaybackFailure failure) : EventArgs
{
    public PlaybackFailure Failure { get; } = failure;
}

/// <summary>
/// The replaceable playback engine. Implementations live in the infrastructure layer; nothing in this
/// contract exposes LibVLC, Avalonia, or Windows types, so the engine can be swapped without touching
/// use cases or view models.
/// </summary>
public interface IMediaPlayerEngine : IAsyncDisposable
{
    /// <summary>The current lifecycle state, observable without awaiting a snapshot.</summary>
    PlaybackState State { get; }

    event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

    event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

    event EventHandler<PlaybackFailureEventArgs>? Failure;

    /// <summary>Prepares the engine once; repeated calls are idempotent.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default);

    Task PlayAsync(CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);

    /// <summary>Releases the media held by the engine while keeping the engine reusable.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates one announced track, or disables that kind when the identifier is null. Only audio
    /// and subtitle tracks can be switched.
    /// </summary>
    Task SelectTrackAsync(
        MediaTrackKind kind,
        string? trackId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a subtitle file that sits beside the media and returns it as a track. The engine
    /// never searches the disk on its own; the caller supplies an already validated path.
    /// </summary>
    Task<MediaTrack> AddExternalSubtitleAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Sets the playback rate; the caller has already clamped it to the allowed range.</summary>
    Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Routes the session's audio to one render endpoint, named by its stable Windows identifier.
    /// An identifier the engine does not recognise is handed over as-is, which the engine treats
    /// as a harmless no-op — losing a device mid-session must never kill the session.
    /// </summary>
    Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a volume decision. An implementation must never raise gain above the normal range
    /// without the limiter the decision carries.
    /// </summary>
    Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default);
}

/// <summary>
/// A decoded frame in 32-bit BGRA. The engine reuses the backing buffer between frames, so handlers
/// must copy the pixels before returning.
/// </summary>
public sealed class VideoFrameEventArgs(ReadOnlyMemory<byte> pixels, int width, int height, int stride)
    : EventArgs
{
    public ReadOnlyMemory<byte> Pixels { get; } = pixels;

    public int Width { get; } = width;

    public int Height { get; } = height;

    public int Stride { get; } = stride;
}

/// <summary>
/// Implemented by engines that decode into process memory. Publishing frames instead of taking over
/// a native child window lets the shell compose accessible controls above the picture.
/// </summary>
public interface IVideoFrameSource
{
    event EventHandler<VideoFrameEventArgs>? FrameRendered;
}

/// <summary>
/// Implemented by engines that choose a track on their own, so a surface can follow the choice
/// instead of guessing it.
/// </summary>
/// <remarks>
/// <para>
/// LibVLC honours the flag a container puts on a track, and it makes that choice while the first
/// frames decode — after the media is open, so a snapshot taken at open time reports nothing in
/// force. Measured on 2026-08-25 against a real episode: seven tracks announced, the subtitle in
/// force reported as none before playing and as track 6 three seconds later.
/// </para>
/// <para>
/// The signal deliberately carries <b>nothing</b>. It is raised on the engine's own event thread,
/// which must never call back into the player, so whoever listens asks
/// <see cref="IMediaPlayerEngine.GetSnapshotAsync"/> from a thread that may.
/// </para>
/// </remarks>
public interface IActiveTrackSource
{
    event EventHandler? ActiveTracksChanged;
}
