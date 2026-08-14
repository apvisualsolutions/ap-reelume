// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Domain.Playback;
using LibVLCSharp.Shared;
using DomainMediaTrack = ApSolutions.LocalMedia.Domain.Playback.MediaTrack;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// Minimal LibVLC adapter behind <see cref="IMediaPlayerEngine"/>. It borrows exactly one media
/// player from the shared factory, holds at most one media at a time, and hands every native media
/// to the factory's deferred release, which is the same discipline the media probe needed.
/// </summary>
/// <remarks>
/// The queue used to live here too (BUG-011), and this copy disposed the native media inside its own
/// lock with nothing to catch a throwing release: the worker flag stayed raised, so the first failure
/// ended the drain for good and every media after it leaked in silence. Unifying it needs the factory
/// to flush on request, because teardown here must release the media before the player that
/// referenced them.
/// </remarks>
public sealed class LibVlcMediaPlayerEngine : IMediaPlayerEngine, IVideoFrameSource
{
    private const int ParseTimeoutMilliseconds = 10_000;
    private const int DisabledTrack = -1;
    private const uint MaximumFrameWidth = 3840;
    private const uint MaximumFrameHeight = 2160;

    // Long enough for the quiescence window of this engine's media and of whatever else is resting
    // in the shared queue; short enough that a busy catalogue scan cannot hold up a shutdown.
    private static readonly TimeSpan ReleaseFlushCeiling = TimeSpan.FromSeconds(5);

    // A near-unity threshold with a limiter-grade ratio: transparent below the ceiling, hard above
    // it. These mirror PeakLimiterAudioFilter, which is the managed reference implementation.
    private static readonly string[] LimiterOptions =
    [
        ":audio-filter=compressor",
        ":compressor-rms-peak=0.0",
        ":compressor-attack=1.5",
        ":compressor-release=60.0",
        ":compressor-threshold=-1.0",
        ":compressor-ratio=20.0",
        ":compressor-knee=1.0",
        ":compressor-makeup-gain=0.0",
    ];

    private readonly LibVlcFactory _factory;
    private readonly IDisplayCapabilityProvider _displays;
    private readonly HardwareAccelerationFallback _acceleration = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private MediaPlayer? _mediaPlayer;
    private VlcMedia? _media;
    private IReadOnlyList<DomainMediaTrack> _tracks = [];
    private TimeSpan? _duration;
    private PlaybackState _state = PlaybackState.Idle;
    private nint _frameBuffer;
    private byte[]? _managedFrame;
    private int _frameWidth;
    private int _frameHeight;
    private int _frameStride;
    private object? _formatCallback;
    private object? _cleanupCallback;
    private object? _lockCallback;
    private object? _displayCallback;
    private int _decodedFrameCount;
    private bool _isDisposed;

    public LibVlcMediaPlayerEngine(LibVlcFactory factory, IDisplayCapabilityProvider? displays = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _displays = displays ?? new SdrOnlyDisplayProvider();
    }

    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

    public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

    public event EventHandler<PlaybackFailureEventArgs>? Failure;

    public event EventHandler<VideoFrameEventArgs>? FrameRendered;

    public PlaybackState State => _state;

    /// <summary>Number of native media objects currently attached to the player: zero or one.</summary>
    public int LiveMediaCount => _media is null ? 0 : 1;

    /// <summary>Frames the decoder actually produced for the active media.</summary>
    public int DecodedFrameCount => Volatile.Read(ref _decodedFrameCount);

    /// <summary>What the engine achieved for the active media; null until one is open.</summary>
    public PlaybackCapabilities? Capabilities { get; private set; }

    /// <summary>True once hardware decoding has been abandoned for this engine.</summary>
    public bool HasFallenBackToSoftware => _acceleration.HasFallenBack;

    /// <summary>Records that hardware decoding failed; the retry happens once and only once.</summary>
    public bool TryFallBackToSoftware() => _acceleration.TryFallBack();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            InitializeCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(request.Path))
        {
            throw new PlaybackFailureException(
                new PlaybackFailure(PlaybackFailureCode.FileNotFound, request.Path));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            InitializeCore();
            DetachMediaCore();
            Transition(PlaybackState.Opening);
            var media = _factory.CreateMedia(request.Path);
            try
            {
                // The native peak limiter is attached to every media, not only when boost is on, so
                // raising the level can never outrun it. Below its threshold it is transparent.
                foreach (var option in LimiterOptions)
                {
                    media.AddOption(option);
                }

                var useHardware = _acceleration.ShouldUseHardware(request.UseHardwareAcceleration);
                foreach (var option in LibVlcVideoCapabilities.AccelerationOptions(useHardware))
                {
                    media.AddOption(option);
                }

                if (request.StartPosition > TimeSpan.Zero)
                {
                    media.AddOption(FormattableString.Invariant(
                        $":start-time={request.StartPosition.TotalSeconds:F3}"));
                }

                var status = await media
                    .Parse(MediaParseOptions.ParseLocal, ParseTimeoutMilliseconds, cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                // The engine reports an empty codec description for a stream it has no decoder for,
                // so the diagnosis is taken from what LibVLC actually recognised, not from the
                // container extension.
                var observed = MapTracks(media);
                var diagnosis = PlaybackDiagnosticsPolicy.Diagnose(
                    new MediaOpenObservation(status == MediaParsedStatus.Done, observed),
                    FormattableString.Invariant(
                        $"LibVLC parse status '{status}' with {observed.Length} announced track(s)."));
                if (diagnosis is not null)
                {
                    throw new PlaybackFailureException(diagnosis);
                }

                _tracks = observed;
                var source = LibVlcVideoCapabilities.Describe(media) with { Hdr = request.SourceHdr };
                Capabilities = _acceleration
                    .Decide(source, _displays.GetCurrentDisplay(), request.UseHardwareAcceleration)
                    .ToCapabilities();
                _duration = media.Duration > 0 ? TimeSpan.FromMilliseconds(media.Duration) : null;
                _mediaPlayer!.Media = media;
                _media = media;
            }
            catch (PlaybackFailureException exception)
            {
                _factory.DeferRelease(media);
                Transition(PlaybackState.Failed);
                Failure?.Invoke(this, new PlaybackFailureEventArgs(exception.Failure));
                throw;
            }
            catch (OperationCanceledException)
            {
                _factory.DeferRelease(media);
                Transition(PlaybackState.Stopped);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task PlayAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync(
            player => player.Play(),
            PlaybackState.Playing,
            cancellationToken);

    public Task PauseAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync(
            player => player.SetPause(pause: true),
            PlaybackState.Paused,
            cancellationToken);

    public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
        InvokeAsync(
            player => player.Time = (long)Math.Max(0, position.TotalMilliseconds),
            targetState: null,
            cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            StopCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SelectTrackAsync(
        MediaTrackKind kind,
        string? trackId,
        CancellationToken cancellationToken = default)
    {
        if (kind == MediaTrackKind.Video)
        {
            throw new PlaybackFailureException(new PlaybackFailure(
                PlaybackFailureCode.EngineUnavailable,
                "The embedded session does not switch video tracks."));
        }

        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_media is null || _mediaPlayer is not { } player)
            {
                throw new PlaybackFailureException(new PlaybackFailure(
                    PlaybackFailureCode.EngineUnavailable,
                    "No media is currently open."));
            }

            // LibVLC identifies tracks with the integers it announced; -1 disables the kind.
            var identifier = trackId is null
                ? DisabledTrack
                : int.Parse(trackId, CultureInfo.InvariantCulture);
            if (kind == MediaTrackKind.Audio)
            {
                player.SetAudioTrack(identifier);
            }
            else
            {
                player.SetSpu(identifier);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DomainMediaTrack> AddExternalSubtitleAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!File.Exists(path))
        {
            throw new PlaybackFailureException(new PlaybackFailure(PlaybackFailureCode.FileNotFound, path));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_media is null || _mediaPlayer is not { } player)
            {
                throw new PlaybackFailureException(new PlaybackFailure(
                    PlaybackFailureCode.EngineUnavailable,
                    "No media is currently open."));
            }

            var uri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
            if (!player.AddSlave(MediaSlaveType.Subtitle, uri, select: true))
            {
                throw new PlaybackFailureException(new PlaybackFailure(
                    PlaybackFailureCode.OpenFailed,
                    "LibVLC refused the external subtitle."));
            }

            var track = new DomainMediaTrack(
                player.Spu.ToString(CultureInfo.InvariantCulture),
                MediaTrackKind.Subtitle,
                Language: null,
                Description: Path.GetFileName(path),
                Channels: null,
                Codec: Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
                IsExternal: true);
            _tracks = [.. _tracks, track];
            return track;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default) =>
        InvokeAsync(
            player => player.SetRate((float)PlaybackControlPolicy.ClampSpeed(multiplier)),
            targetState: null,
            cancellationToken);

    public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return InvokeAsync(
            player =>
            {
                // LibVLC names endpoints with its module prefix; the catalog names them the way
                // Windows stores them. The endpoint identifier is common to both, so the suffix
                // match is the join — and an identifier LibVLC does not announce is handed over
                // unchanged, which VLC treats as a no-op rather than a failure.
                var announced = player.AudioOutputDeviceEnum ?? [];
                var known = announced.FirstOrDefault(device =>
                    device.DeviceIdentifier is { } identifier
                    && identifier.EndsWith(deviceId, StringComparison.OrdinalIgnoreCase));
                player.SetOutputDevice(known.DeviceIdentifier ?? deviceId);
            },
            targetState: null,
            cancellationToken);
    }

    public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.IsBoosted && !decision.LimiterEngaged)
        {
            throw new PlaybackFailureException(new PlaybackFailure(
                PlaybackFailureCode.EngineUnavailable,
                "Boosted volume was requested without its limiter."));
        }

        return InvokeAsync(
            player =>
            {
                player.Mute = decision.IsMuted;
                player.Volume = decision.Percent;
            },
            targetState: null,
            cancellationToken);
    }

    public async Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return PlaybackSnapshot.Create(_state, ReadPosition(), ReadDuration(), _tracks);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        MediaPlayer? player;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            StopCore();
            player = _mediaPlayer;
            _mediaPlayer = null;
        }
        finally
        {
            _gate.Release();
        }

        // Every native media must be released before the player that referenced it, and only after
        // the quiescence window; releasing the player first crashes the LibVLC teardown path. An
        // exhausted ceiling is not a failure to report: the drain still owns the media, and refusing
        // to finish this teardown would be the worse outcome.
        _ = await LibVlcFactory.FlushDeferredReleasesAsync(ReleaseFlushCeiling).ConfigureAwait(false);
        if (player is not null)
        {
            player.TimeChanged -= OnTimeChanged;
            player.EncounteredError -= OnEncounteredError;
            player.EndReached -= OnEndReached;
            _factory.ReleaseMediaPlayer(player);
        }

        ReleaseFrameBuffer();
        _formatCallback = null;
        _cleanupCallback = null;
        _lockCallback = null;
        _displayCallback = null;
        _gate.Dispose();
    }

    private void InitializeCore()
    {
        if (_mediaPlayer is not null)
        {
            return;
        }

        var player = _factory.CreateMediaPlayer();
        player.TimeChanged += OnTimeChanged;
        player.EncounteredError += OnEncounteredError;
        player.EndReached += OnEndReached;
        InstallVideoSink(player);
        _mediaPlayer = player;
    }

    /// <summary>
    /// Decodes into process memory and publishes each frame. The LibVLC native window output is not
    /// used: a native child window always covers the composition, hiding the accessible controls,
    /// and the LibVLC dummy video output faults while media are opened and released in sequence.
    /// </summary>
    private void InstallVideoSink(MediaPlayer player)
    {
        var formatCallback = new MediaPlayer.LibVLCVideoFormatCb(OnVideoFormat);
        var cleanupCallback = new MediaPlayer.LibVLCVideoCleanupCb(OnVideoCleanup);
        var lockCallback = new MediaPlayer.LibVLCVideoLockCb((_, planes) =>
        {
            Marshal.WriteIntPtr(planes, _frameBuffer);
            return nint.Zero;
        });
        var displayCallback = new MediaPlayer.LibVLCVideoDisplayCb((_, _) =>
        {
            _ = Interlocked.Increment(ref _decodedFrameCount);
            if (FrameRendered is not { } handler || _managedFrame is not { } managed)
            {
                return;
            }

            Marshal.Copy(_frameBuffer, managed, 0, managed.Length);
            handler(this, new VideoFrameEventArgs(managed, _frameWidth, _frameHeight, _frameStride));
        });

        _formatCallback = formatCallback;
        _cleanupCallback = cleanupCallback;
        _lockCallback = lockCallback;
        _displayCallback = displayCallback;

        player.SetVideoFormatCallbacks(formatCallback, cleanupCallback);
        player.SetVideoCallbacks(lockCallback, null, displayCallback);
    }

    private uint OnVideoFormat(
        ref nint opaque,
        nint chroma,
        ref uint width,
        ref uint height,
        ref uint pitches,
        ref uint lines)
    {
        width = Math.Clamp(width, 1, MaximumFrameWidth);
        height = Math.Clamp(height, 1, MaximumFrameHeight);
        Marshal.Copy("RV32"u8.ToArray(), 0, chroma, 4);
        pitches = width * 4;
        lines = height;

        ReleaseFrameBuffer();
        _frameWidth = (int)width;
        _frameHeight = (int)height;
        _frameStride = (int)pitches;
        _frameBuffer = Marshal.AllocHGlobal(_frameStride * _frameHeight);
        _managedFrame = new byte[_frameStride * _frameHeight];
        return 1;
    }

    private void OnVideoCleanup(ref nint opaque) => ReleaseFrameBuffer();

    private void ReleaseFrameBuffer()
    {
        if (_frameBuffer == nint.Zero)
        {
            return;
        }

        Marshal.FreeHGlobal(_frameBuffer);
        _frameBuffer = nint.Zero;
        _managedFrame = null;
        _frameWidth = 0;
        _frameHeight = 0;
        _frameStride = 0;
    }

    private async Task InvokeAsync(
        Action<MediaPlayer> action,
        PlaybackState? targetState,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_media is null || _mediaPlayer is not { } player)
            {
                throw new PlaybackFailureException(new PlaybackFailure(
                    PlaybackFailureCode.EngineUnavailable,
                    "No media is currently open."));
            }

            await Task.Run(() => action(player), cancellationToken).ConfigureAwait(false);
            if (targetState is { } state)
            {
                Transition(state);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void StopCore()
    {
        if (_mediaPlayer is { } player && _media is not null)
        {
            // Stopping a player that is still in the playing state crashes this LibVLC build.
            // Pausing first drains the decoder and makes teardown deterministic; the same
            // quiescence discipline the media probe already required.
            if (player.IsPlaying)
            {
                player.SetPause(pause: true);
            }

            player.Stop();
        }

        DetachMediaCore();
        if (_state != PlaybackState.Idle && _state != PlaybackState.Stopped)
        {
            Transition(PlaybackState.Stopped);
        }
    }

    private void DetachMediaCore()
    {
        if (_media is not { } media)
        {
            return;
        }

        _media = null;
        _tracks = [];
        _duration = null;
        _factory.DeferRelease(media);
    }

    private TimeSpan ReadPosition()
    {
        if (_mediaPlayer is not { } player || _media is null)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromMilliseconds(Math.Max(0, player.Time));
    }

    private TimeSpan? ReadDuration()
    {
        if (_mediaPlayer is { Length: > 0 } player && _media is not null)
        {
            return TimeSpan.FromMilliseconds(player.Length);
        }

        return _duration;
    }

    private void Transition(PlaybackState next)
    {
        var previous = _state;
        if (previous == next)
        {
            return;
        }

        _state = next;
        StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(previous, next));
    }

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs args) =>
        PositionChanged?.Invoke(
            this,
            new PlaybackPositionChangedEventArgs(
                TimeSpan.FromMilliseconds(Math.Max(0, args.Time)),
                _duration));

    /// <summary>
    /// Raised on LibVLC's event thread, which must never call back into the player; the transition
    /// only flips the state and raises the managed event, and whoever listens posts elsewhere.
    /// </summary>
    private void OnEndReached(object? sender, EventArgs args) => Transition(PlaybackState.Ended);

    private void OnEncounteredError(object? sender, EventArgs args) =>
        Failure?.Invoke(
            this,
            new PlaybackFailureEventArgs(new PlaybackFailure(
                PlaybackFailureCode.OpenFailed,
                "LibVLC encountered an unrecoverable error for the active media.")));

    private static DomainMediaTrack[] MapTracks(VlcMedia media) =>
    [
        .. media.Tracks
            .Where(track => track.TrackType is TrackType.Video or TrackType.Audio or TrackType.Text)
            .Select(track => new DomainMediaTrack(
                track.Id.ToString(CultureInfo.InvariantCulture),
                track.TrackType switch
                {
                    TrackType.Video => MediaTrackKind.Video,
                    TrackType.Audio => MediaTrackKind.Audio,
                    _ => MediaTrackKind.Subtitle,
                },
                track.Language,
                track.Description,
                track.TrackType == TrackType.Audio ? checked((int)track.Data.Audio.Channels) : null,
                media.CodecDescription(track.TrackType, track.Codec))),
    ];

    /// <summary>Used when no host provider is supplied: reports no HDR rather than guessing.</summary>
    private sealed class SdrOnlyDisplayProvider : IDisplayCapabilityProvider
    {
        public DisplayCapabilities GetCurrentDisplay() => new(SupportsHdr10: false, HdrEnabled: false);
    }
}
