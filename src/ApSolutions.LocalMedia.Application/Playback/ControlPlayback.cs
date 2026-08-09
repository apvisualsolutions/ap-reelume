using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>The state the transport is in after a command, ready for the interface to display.</summary>
public sealed record PlaybackControlState(
    TimeSpan Position,
    TimeSpan? Duration,
    double SpeedMultiplier,
    VolumeDecision Volume,
    TimeSpan BackwardSkip,
    TimeSpan ForwardSkip);

/// <summary>
/// Serialises every transport command against the single active session. Commands are clamped by the
/// domain policies first, so a hundred rapid changes end on a deterministic state and the engine
/// never receives a value outside its contract.
/// </summary>
public sealed class ControlPlayback : IDisposable
{
    private readonly IMediaPlayerEngine _engine;
    private readonly Func<TimeSpan, TimeSpan?, CancellationToken, Task>? _persistAfterSeek;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private double _speed = 1.0;
    private int _volumePercent = VolumeBoostPolicy.MaximumNormalPercent;
    private bool _muted;
    private TimeSpan _backwardSkip = PlaybackControlPolicy.DefaultBackwardSkip;
    private TimeSpan _forwardSkip = PlaybackControlPolicy.DefaultForwardSkip;

    /// <param name="engine">The single active engine every command lands on.</param>
    /// <param name="persistAfterSeek">
    /// Called with the clamped target after every seek, because a jump is a moment worth surviving a
    /// crash: the person chose that position on purpose.
    /// </param>
    public ControlPlayback(
        IMediaPlayerEngine engine,
        Func<TimeSpan, TimeSpan?, CancellationToken, Task>? persistAfterSeek = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _persistAfterSeek = persistAfterSeek;
    }

    public Task<PlaybackControlState> SetSpeedAsync(double requested, CancellationToken cancellationToken = default) =>
        RunAsync(
            async token =>
            {
                _speed = PlaybackControlPolicy.ClampSpeed(requested);
                await _engine.SetSpeedAsync(_speed, token).ConfigureAwait(false);
            },
            cancellationToken);

    public Task<PlaybackControlState> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
        RunAsync(
            async token =>
            {
                var snapshot = await _engine.GetSnapshotAsync(token).ConfigureAwait(false);
                var target = PlaybackControlPolicy.ClampPosition(position, snapshot.Duration);
                await _engine.SeekAsync(target, token).ConfigureAwait(false);
                await PersistSeekAsync(target, snapshot.Duration, token).ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>Moves backward by the configured interval, landing exactly on the start.</summary>
    public Task<PlaybackControlState> SkipBackwardAsync(CancellationToken cancellationToken = default) =>
        SkipAsync(-_backwardSkip, cancellationToken);

    /// <summary>Moves forward by the configured interval, landing exactly on the end.</summary>
    public Task<PlaybackControlState> SkipForwardAsync(CancellationToken cancellationToken = default) =>
        SkipAsync(_forwardSkip, cancellationToken);

    public Task<PlaybackControlState> ConfigureSkipsAsync(
        TimeSpan backward,
        TimeSpan forward,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            _ =>
            {
                _backwardSkip = PlaybackControlPolicy.ClampSkipInterval(backward);
                _forwardSkip = PlaybackControlPolicy.ClampSkipInterval(forward);
                return Task.CompletedTask;
            },
            cancellationToken);

    public Task<PlaybackControlState> SetVolumeAsync(int percent, CancellationToken cancellationToken = default) =>
        RunAsync(
            async token =>
            {
                _volumePercent = VolumeBoostPolicy.Decide(percent, _muted).Percent;
                await _engine
                    .ApplyVolumeAsync(VolumeBoostPolicy.Decide(_volumePercent, _muted), token)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>Mutes and unmutes without forgetting the chosen level.</summary>
    public Task<PlaybackControlState> ToggleMuteAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            async token =>
            {
                _muted = !_muted;
                await _engine
                    .ApplyVolumeAsync(VolumeBoostPolicy.Decide(_volumePercent, _muted), token)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    public void Dispose() => _gate.Dispose();

    private Task<PlaybackControlState> SkipAsync(TimeSpan delta, CancellationToken cancellationToken) =>
        RunAsync(
            async token =>
            {
                var snapshot = await _engine.GetSnapshotAsync(token).ConfigureAwait(false);
                var target = PlaybackControlPolicy.Skip(snapshot.Position, delta, snapshot.Duration);
                await _engine.SeekAsync(target, token).ConfigureAwait(false);
                await PersistSeekAsync(target, snapshot.Duration, token).ConfigureAwait(false);
            },
            cancellationToken);

    private async Task PersistSeekAsync(TimeSpan target, TimeSpan? duration, CancellationToken cancellationToken)
    {
        if (_persistAfterSeek is { } persist)
        {
            await persist(target, duration, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<PlaybackControlState> RunAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await action(cancellationToken).ConfigureAwait(false);
            var snapshot = await _engine.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            return new PlaybackControlState(
                snapshot.Position,
                snapshot.Duration,
                _speed,
                VolumeBoostPolicy.Decide(_volumePercent, _muted),
                _backwardSkip,
                _forwardSkip);
        }
        finally
        {
            _ = _gate.Release();
        }
    }
}
