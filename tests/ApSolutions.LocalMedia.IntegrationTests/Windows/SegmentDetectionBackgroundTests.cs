using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Windows.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Windows;

/// <summary>
/// The background side of detection: a series is read at most once per session, a failed run may be
/// tried again later, and the activity adapter tells the truth about whether something is playing.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SegmentDetectionBackgroundTests
{
    private static readonly TitleId Show = new(Guid.Parse("e8b10005-0000-4000-8000-000000000001"));

    private static readonly SeriesId Series = new(Guid.Parse("e8b10005-0000-4000-8000-000000000002"));

    [Fact]
    public async Task A_series_is_detected_at_most_once_per_session()
    {
        var detector = new SignallingDetector();
        var scheduler = new SegmentDetectionScheduler(() => UseCase(detector, enabled: true));

        scheduler.Schedule(Show, Series);
        scheduler.Schedule(Show, Series);
        await detector.FirstCall.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(1, detector.Calls);
    }

    [Fact]
    public async Task A_failed_run_lets_a_later_session_try_the_series_again()
    {
        var detector = new SignallingDetector { FailFirstCall = true };
        var scheduler = new SegmentDetectionScheduler(() => UseCase(detector, enabled: true));

        scheduler.Schedule(Show, Series);
        await detector.FirstCall.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        // The scheduler forgets a failed series only after its catch has run, so the retry is
        // offered repeatedly until it lands; a bounded poll keeps the test deterministic.
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (detector.Calls < 2 && deadline.Elapsed < TimeSpan.FromSeconds(10))
        {
            scheduler.Schedule(Show, Series);
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, detector.Calls);
    }

    [Fact]
    public async Task A_cancelled_run_is_forgotten_so_a_later_session_can_try_again()
    {
        var detector = new CancellingFirstDetector();
        var scheduler = new SegmentDetectionScheduler(() => UseCase(detector, enabled: true));

        scheduler.Schedule(Show, Series);
        await detector.FirstCall.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (detector.Calls < 2 && deadline.Elapsed < TimeSpan.FromSeconds(10))
        {
            scheduler.Schedule(Show, Series);
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, detector.Calls);
    }

    [Fact]
    public void The_background_pieces_refuse_to_exist_half_armed()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new SegmentDetectionScheduler(null!));
        _ = Assert.Throws<ArgumentNullException>(() => new CoordinatorPlaybackActivity(null!));
    }

    [Fact]
    public void The_activity_adapter_reports_exactly_whether_a_session_exists()
    {
        var coordinator = new FakeCoordinator();
        var activity = new CoordinatorPlaybackActivity(coordinator);

        Assert.False(activity.IsPlaybackActive);
        coordinator.Session = new PlaybackSession(Guid.NewGuid(), new MediaFileId(Guid.NewGuid()), @"D:\Media\a.mkv");
        Assert.True(activity.IsPlaybackActive);
        coordinator.Session = null;
        Assert.False(activity.IsPlaybackActive);
    }

    private static DetectSeriesSegments UseCase(IAutomaticSegmentDetector detector, bool enabled)
    {
        var settings = new InMemorySettingsStore();
        var useCase = new DetectSeriesSegments(
            settings,
            new SingleEpisodeSequence(),
            detector,
            new InMemoryDetectedMarkers());
        useCase.SetEnabled(enabled);
        return useCase;
    }

    /// <summary>Counts calls and lets a test await them; failing the first call is scripted.</summary>
    private sealed class SignallingDetector : IAutomaticSegmentDetector
    {
        private int _calls;

        public bool FailFirstCall { get; init; }

        public int Calls => Volatile.Read(ref _calls);

        public TaskCompletionSource FirstCall { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Version => 1;

        public Task<SeriesSegmentDetection> DetectAsync(
            SeriesId seriesId,
            IReadOnlyList<SegmentDetectionEpisode> episodes,
            IProgress<SegmentDetectionProgress>? progress,
            CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call == 1)
            {
                _ = FirstCall.TrySetResult();
                if (FailFirstCall)
                {
                    throw new InvalidOperationException("Scripted failure.");
                }
            }

            return Task.FromResult(new SeriesSegmentDetection(seriesId, Version, []));
        }
    }

    private sealed class CancellingFirstDetector : IAutomaticSegmentDetector
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public TaskCompletionSource FirstCall { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Version => 1;

        public Task<SeriesSegmentDetection> DetectAsync(
            SeriesId seriesId,
            IReadOnlyList<SegmentDetectionEpisode> episodes,
            IProgress<SegmentDetectionProgress>? progress,
            CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call == 1)
            {
                _ = FirstCall.TrySetResult();
                throw new OperationCanceledException();
            }

            return Task.FromResult(new SeriesSegmentDetection(seriesId, Version, []));
        }
    }

    private sealed class FakeCoordinator : IPlaybackSessionCoordinator
    {
        public PlaybackSession? Session { get; set; }

        public PlaybackSession? ActiveSession => Session;

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SingleEpisodeSequence : IEpisodeSequenceRepository
    {
        private static readonly EpisodeSequenceEntry Entry = new(
            new EpisodeId(Guid.Parse("e8b10005-0000-4000-8000-000000000010")),
            Show,
            SeasonNumber: 1,
            EpisodeNumber: 1,
            new MediaFileId(Guid.Parse("e8b10005-0000-4000-8000-000000000011")),
            @"D:\Media\S01E01.mkv",
            IsAvailable: true);

        public Task<IReadOnlyList<EpisodeSequenceEntry>> GetSeriesAsync(
            TitleId showId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EpisodeSequenceEntry>>([Entry]);

        public Task<EpisodeSequenceEntry?> GetAsync(
            EpisodeId episodeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EpisodeSequenceEntry?>(Entry);

        public Task<EpisodeSequenceEntry?> FindByFileAsync(
            MediaFileId fileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EpisodeSequenceEntry?>(Entry);
    }

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, object?> _values = [];

        public T? Read<T>(string key) =>
            _values.TryGetValue(key, out var value) && value is T typed ? typed : default;

        public void Write<T>(string key, T value) => _values[key] = value;
    }

    private sealed class InMemoryDetectedMarkers : IDetectedMarkerRepository
    {
        private readonly Dictionary<Guid, DetectedMarker> _rows = [];

        public Task<IReadOnlyList<DetectedMarker>> GetForSeriesAsync(
            SeriesId seriesId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DetectedMarker>>(
                [.. _rows.Values.Where(row => row.SeriesId == seriesId)]);

        public Task<IReadOnlyList<DetectedMarker>> GetForFileAsync(
            MediaFileId fileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DetectedMarker>>(
                [.. _rows.Values.Where(row => row.FileId == fileId)]);

        public Task<DetectedMarker?> GetAsync(Guid markerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_rows.TryGetValue(markerId, out var row) ? row : null);

        public Task ReplaceForSeriesAsync(
            SeriesId seriesId,
            IReadOnlyList<DetectedMarker> markers,
            CancellationToken cancellationToken = default)
        {
            foreach (var stale in _rows.Values.Where(row => row.SeriesId == seriesId).ToArray())
            {
                _ = _rows.Remove(stale.Id);
            }

            foreach (var marker in markers)
            {
                _rows[marker.Id] = marker;
            }

            return Task.CompletedTask;
        }

        public Task SaveAsync(DetectedMarker marker, CancellationToken cancellationToken = default)
        {
            _rows[marker.Id] = marker;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid markerId, CancellationToken cancellationToken = default)
        {
            _ = _rows.Remove(markerId);
            return Task.CompletedTask;
        }
    }
}
