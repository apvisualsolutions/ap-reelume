using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Application.Continuity;

/// <summary>
/// Keeps the stored position within one interval of what the engine is actually playing.
/// <para>
/// Observations are cheap and never touch storage: the caller can report a position from the engine's
/// event as often as it likes, and only the latest one survives to the next write. Writes are
/// serialised, so two triggers arriving together cannot interleave, and a critical trigger is bounded
/// by a timeout because a closing session cannot wait for a slow disk.
/// </para>
/// </summary>
public sealed class PlaybackProgressTracker : IAsyncDisposable
{
    private static readonly TimeSpan DefaultCriticalFlushTimeout = TimeSpan.FromSeconds(2);

    private readonly IWatchStateRepository _repository;
    private readonly IClock _clock;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _criticalFlushTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _observationSync = new();
    private ContentKey _content;
    private MediaFileId _source;
    private TimeSpan? _lastPersisted;
    private TimeSpan _position;
    private TimeSpan? _duration;
    private bool _hasSession;
    private int _writeCount;
    private int _timedOutFlushes;
    private bool _isDisposed;

    public PlaybackProgressTracker(
        IWatchStateRepository repository,
        IClock clock,
        TimeSpan? interval = null,
        TimeSpan? criticalFlushTimeout = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _interval = interval ?? ProgressPolicy.SaveInterval;
        _criticalFlushTimeout = criticalFlushTimeout ?? DefaultCriticalFlushTimeout;
    }

    /// <summary>How many writes actually reached storage.</summary>
    public int WriteCount => Volatile.Read(ref _writeCount);

    /// <summary>How many critical writes hit their timeout instead of completing.</summary>
    public int TimedOutFlushes => Volatile.Read(ref _timedOutFlushes);

    /// <summary>
    /// Attaches the tracker to one piece of content and returns what was already stored, which is what
    /// the resume prompt reads.
    /// </summary>
    public async Task<WatchState?> BeginAsync(
        ContentKey content,
        MediaFileId sourceMediaFileId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        var existing = await _repository.GetAsync(content, cancellationToken).ConfigureAwait(false);
        lock (_observationSync)
        {
            _content = content;
            _source = sourceMediaFileId;
            _lastPersisted = null;
            _position = existing?.Position ?? TimeSpan.Zero;
            _duration = existing?.ObservedDuration;
            _hasSession = true;
        }

        return existing;
    }

    /// <summary>
    /// Records what the engine is playing right now. This is called from the engine's position event,
    /// so it performs no input or output and never blocks.
    /// </summary>
    public void Observe(TimeSpan position, TimeSpan? duration)
    {
        lock (_observationSync)
        {
            _position = position;
            _duration = duration ?? _duration;
        }
    }

    /// <summary>Runs the periodic write until the session is cancelled.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _clock.DelayAsync(_interval, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _ = await FlushAsync(PersistenceTrigger.Tick, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Writes the latest observation when the policy says it is worth it.</summary>
    public async Task<bool> FlushAsync(
        PersistenceTrigger trigger,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!Volatile.Read(ref _hasSession))
        {
            return false;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ContentKey content;
            MediaFileId source;
            TimeSpan? lastPersisted;
            TimeSpan position;
            TimeSpan? duration;
            lock (_observationSync)
            {
                content = _content;
                source = _source;
                lastPersisted = _lastPersisted;
                position = _position;
                duration = _duration;
            }

            var clamped = ProgressPolicy.ClampPosition(position, duration);
            if (!ProgressPolicy.ShouldPersist(lastPersisted, clamped, trigger))
            {
                return false;
            }

            // The stored row is read again rather than cached: a person can mark content watched from
            // the catalogue while it is playing, and that decision must survive the next write.
            var stored = await _repository.GetAsync(content, cancellationToken).ConfigureAwait(false);

            var now = _clock.UtcNow;
            var state = new WatchState
            {
                Content = content,
                Position = clamped,
                ObservedDuration = duration ?? stored?.ObservedDuration,
                SourceMediaFileId = source,

                // The status machine belongs to the watch-state policy; progress only reports that
                // something is under way and never overrules a decision the person made by hand.
                Status = stored is { IsManualOverride: true } ? stored.Status : WatchStatus.InProgress,
                IsManualOverride = stored?.IsManualOverride ?? false,
                StartedUtc = stored?.StartedUtc ?? now,
                UpdatedUtc = now,
            };

            if (!await WriteAsync(state, trigger, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            lock (_observationSync)
            {
                _lastPersisted = clamped;
            }

            _ = Interlocked.Increment(ref _writeCount);
            return true;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return ValueTask.CompletedTask;
        }

        _isDisposed = true;
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<bool> WriteAsync(
        WatchState state,
        PersistenceTrigger trigger,
        CancellationToken cancellationToken)
    {
        var save = _repository.SaveAsync(state, cancellationToken);
        if (!ProgressPolicy.IsCritical(trigger))
        {
            await save.ConfigureAwait(false);
            return true;
        }

        try
        {
            // A closing or failing session cannot wait indefinitely: the wait is real wall-clock time,
            // deliberately not the injected clock, because it bounds the shutdown path.
            await save.WaitAsync(_criticalFlushTimeout, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            _ = Interlocked.Increment(ref _timedOutFlushes);
            _ = save.ContinueWith(
                static abandoned => _ = abandoned.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return false;
        }
    }
}
