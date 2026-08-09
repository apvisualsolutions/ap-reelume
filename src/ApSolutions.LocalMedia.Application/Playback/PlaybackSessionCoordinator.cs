using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>Guarantees that at most one embedded playback session exists at any moment.</summary>
public interface IPlaybackSessionCoordinator
{
    PlaybackSession? ActiveSession { get; }

    Task<PlaybackSession> StartAsync(PlaybackRequest request, CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Serialises every engine transition so a second start deterministically stops the first session,
/// a cancelled or failed open releases the engine, and disposal never leaves a session behind.
/// </summary>
public sealed class PlaybackSessionCoordinator : IPlaybackSessionCoordinator, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IMediaPlayerEngine _engine;
    private readonly IApplicationEventPublisher _eventPublisher;
    private PlaybackSession? _activeSession;
    private bool _isInitialized;
    private bool _isDisposed;

    public PlaybackSessionCoordinator(IMediaPlayerEngine engine, IApplicationEventPublisher eventPublisher)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public PlaybackSession? ActiveSession => _activeSession;

    public async Task<PlaybackSession> StartAsync(
        PlaybackRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopActiveSessionAsync(CancellationToken.None).ConfigureAwait(false);
            if (!_isInitialized)
            {
                await _engine.InitializeAsync(cancellationToken).ConfigureAwait(false);
                _isInitialized = true;
            }

            var session = new PlaybackSession(Guid.NewGuid(), request.MediaFileId, request.Path);
            await PublishAsync(session, PlaybackState.Opening, failure: null, cancellationToken).ConfigureAwait(false);
            try
            {
                await _engine.OpenAsync(request, cancellationToken).ConfigureAwait(false);
                await _engine.PlayAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await _engine.StopAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            catch (PlaybackFailureException exception)
            {
                await _engine.StopAsync(CancellationToken.None).ConfigureAwait(false);
                await PublishAsync(session, PlaybackState.Failed, exception.Failure, CancellationToken.None)
                    .ConfigureAwait(false);
                throw;
            }

            _activeSession = session;
            await PublishAsync(session, PlaybackState.Playing, failure: null, cancellationToken).ConfigureAwait(false);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task PauseAsync(CancellationToken cancellationToken = default) =>
        ChangeActiveStateAsync(PlaybackState.Paused, cancellationToken);

    public Task ResumeAsync(CancellationToken cancellationToken = default) =>
        ChangeActiveStateAsync(PlaybackState.Playing, cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopActiveSessionAsync(cancellationToken).ConfigureAwait(false);
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
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopActiveSessionAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        await _engine.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private async Task ChangeActiveStateAsync(PlaybackState target, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_activeSession is not { } session)
            {
                return;
            }

            if (target == PlaybackState.Paused)
            {
                await _engine.PauseAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _engine.PlayAsync(cancellationToken).ConfigureAwait(false);
            }

            await PublishAsync(session, target, failure: null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopActiveSessionAsync(CancellationToken cancellationToken)
    {
        if (_activeSession is not { } session)
        {
            return;
        }

        _activeSession = null;
        await _engine.StopAsync(cancellationToken).ConfigureAwait(false);
        await PublishAsync(session, PlaybackState.Stopped, failure: null, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishAsync(
        PlaybackSession session,
        PlaybackState state,
        PlaybackFailure? failure,
        CancellationToken cancellationToken)
    {
        var snapshot = await _engine.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        await _eventPublisher
            .PublishAsync(
                new PlaybackSessionChanged(session.Id, session.MediaFileId, state, snapshot.Position, failure),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
