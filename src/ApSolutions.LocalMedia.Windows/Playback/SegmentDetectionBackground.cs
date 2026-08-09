using System.Collections.Concurrent;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Windows.Playback;

/// <summary>
/// Detection's view of playback: whether an embedded session exists right now. The detector reads
/// this between episodes and yields, because the person watching something always comes first.
/// </summary>
public sealed class CoordinatorPlaybackActivity : IPlaybackActivity
{
    private readonly IPlaybackSessionCoordinator _coordinator;

    public CoordinatorPlaybackActivity(IPlaybackSessionCoordinator coordinator) =>
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

    public bool IsPlaybackActive => _coordinator.ActiveSession is not null;
}

/// <summary>
/// Runs segment detection for a series in the background, at most once per series per session.
/// Scheduling happens when an episode opens; the run itself waits while that playback is active, so
/// the work effectively lands in the quiet time after watching.
/// </summary>
public sealed class SegmentDetectionScheduler : IDisposable
{
    private readonly Func<DetectSeriesSegments> _detectFactory;
    private readonly ConcurrentDictionary<Guid, byte> _scheduled = new();

    // Detection is a guest in somebody's session: every run it starts carries this token, so the
    // exit path can end a decode in flight instead of leaving a process working after its window.
    private readonly CancellationTokenSource _shutdown = new();

    public SegmentDetectionScheduler(Func<DetectSeriesSegments> detectFactory) =>
        _detectFactory = detectFactory ?? throw new ArgumentNullException(nameof(detectFactory));

    public void Schedule(TitleId showId, SeriesId seriesId)
    {
        if (_shutdown.IsCancellationRequested || !_scheduled.TryAdd(seriesId.Value, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                _ = await _detectFactory()
                    .ExecuteAsync(new DetectSeriesSegmentsCommand(showId, seriesId), progress: null, _shutdown.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _ = _scheduled.TryRemove(seriesId.Value, out _);
            }
            catch (Exception)
            {
                // A failed background detection must never take the application down; forgetting
                // the series lets a later session try again.
                _ = _scheduled.TryRemove(seriesId.Value, out _);
            }
        });
    }

    /// <summary>Stops accepting work and cancels any detection in flight.</summary>
    public void Stop()
    {
        if (!_shutdown.IsCancellationRequested)
        {
            _shutdown.Cancel();
        }
    }

    public void Dispose()
    {
        Stop();
        _shutdown.Dispose();
    }
}
