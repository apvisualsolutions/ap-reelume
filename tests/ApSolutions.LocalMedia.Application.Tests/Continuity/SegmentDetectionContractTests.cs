using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Continuity;

/// <summary>
/// The contracts around detection: nothing is built half-armed, and reviewing a row that no longer
/// exists answers honestly instead of inventing one.
/// </summary>
public sealed class SegmentDetectionContractTests
{
    [Fact]
    public async Task The_detection_use_case_refuses_to_exist_half_armed()
    {
        var settings = new InMemorySettingsStore();
        var episodes = new EmptyEpisodeSequence();
        var detector = new EmptyDetector();
        var repository = new EmptyDetectedMarkers();

        _ = Assert.Throws<ArgumentNullException>(() => new DetectSeriesSegments(null!, episodes, detector, repository));
        _ = Assert.Throws<ArgumentNullException>(() => new DetectSeriesSegments(settings, null!, detector, repository));
        _ = Assert.Throws<ArgumentNullException>(() => new DetectSeriesSegments(settings, episodes, null!, repository));
        _ = Assert.Throws<ArgumentNullException>(() => new DetectSeriesSegments(settings, episodes, detector, null!));
        _ = Assert.Throws<ArgumentNullException>(() => new ReviewDetectedSegments(null!));

        var useCase = new DetectSeriesSegments(settings, episodes, detector, repository);
        _ = await Assert.ThrowsAsync<ArgumentNullException>(() => useCase.ExecuteAsync(
            null!,
            progress: null,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reviewing_an_existing_row_accepts_corrects_and_deletes()
    {
        var row = new DetectedMarker(
            Guid.NewGuid(),
            new SeriesId(Guid.NewGuid()),
            new MediaFileId(Guid.NewGuid()),
            MarkerKind.Intro,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(35),
            Confidence: 0.9,
            DetectorVersion: 1,
            UserCorrected: false);
        var repository = new SingleRowDetectedMarkers(row);
        var review = new ReviewDetectedSegments(repository);

        var accepted = await review.AcceptAsync(row.Id, TestContext.Current.CancellationToken);
        Assert.True(accepted is { UserCorrected: true });

        var corrected = await review.CorrectAsync(
            row.Id,
            TimeSpan.FromSeconds(12),
            TimeSpan.FromSeconds(37),
            duration: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromSeconds(12), corrected!.Start);

        Assert.True(await review.DeleteAsync(row.Id, TestContext.Current.CancellationToken));
        Assert.Null(repository.Row);
    }

    [Fact]
    public async Task Reviewing_a_row_that_no_longer_exists_answers_with_nothing()
    {
        var review = new ReviewDetectedSegments(new EmptyDetectedMarkers());
        var missing = Guid.NewGuid();

        Assert.Null(await review.AcceptAsync(missing, TestContext.Current.CancellationToken));
        Assert.Null(await review.CorrectAsync(
            missing,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            duration: null,
            TestContext.Current.CancellationToken));
        Assert.False(await review.DeleteAsync(missing, TestContext.Current.CancellationToken));
    }

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, object?> _values = [];

        public T? Read<T>(string key) =>
            _values.TryGetValue(key, out var value) && value is T typed ? typed : default;

        public void Write<T>(string key, T value) => _values[key] = value;
    }

    private sealed class EmptyEpisodeSequence : IEpisodeSequenceRepository
    {
        public Task<IReadOnlyList<EpisodeSequenceEntry>> GetSeriesAsync(
            TitleId showId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EpisodeSequenceEntry>>([]);

        public Task<EpisodeSequenceEntry?> GetAsync(
            EpisodeId episodeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EpisodeSequenceEntry?>(null);

        public Task<EpisodeSequenceEntry?> FindByFileAsync(
            MediaFileId fileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EpisodeSequenceEntry?>(null);
    }

    private sealed class EmptyDetector : IAutomaticSegmentDetector
    {
        public int Version => 1;

        public Task<SeriesSegmentDetection> DetectAsync(
            SeriesId seriesId,
            IReadOnlyList<SegmentDetectionEpisode> episodes,
            IProgress<SegmentDetectionProgress>? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SeriesSegmentDetection(seriesId, Version, []));
    }

    private sealed class SingleRowDetectedMarkers(DetectedMarker row) : IDetectedMarkerRepository
    {
        public DetectedMarker? Row { get; private set; } = row;

        public Task<IReadOnlyList<DetectedMarker>> GetForSeriesAsync(
            SeriesId seriesId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DetectedMarker>>(Row is { } value ? [value] : []);

        public Task<IReadOnlyList<DetectedMarker>> GetForFileAsync(
            MediaFileId fileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DetectedMarker>>(Row is { } value ? [value] : []);

        public Task<DetectedMarker?> GetAsync(Guid markerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Row?.Id == markerId ? Row : null);

        public Task ReplaceForSeriesAsync(
            SeriesId seriesId,
            IReadOnlyList<DetectedMarker> markers,
            CancellationToken cancellationToken = default)
        {
            Row = markers.Count == 0 ? null : markers[0];
            return Task.CompletedTask;
        }

        public Task SaveAsync(DetectedMarker marker, CancellationToken cancellationToken = default)
        {
            Row = marker;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid markerId, CancellationToken cancellationToken = default)
        {
            if (Row?.Id == markerId)
            {
                Row = null;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class EmptyDetectedMarkers : IDetectedMarkerRepository
    {
        public Task<IReadOnlyList<DetectedMarker>> GetForSeriesAsync(
            SeriesId seriesId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DetectedMarker>>([]);

        public Task<IReadOnlyList<DetectedMarker>> GetForFileAsync(
            MediaFileId fileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DetectedMarker>>([]);

        public Task<DetectedMarker?> GetAsync(Guid markerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DetectedMarker?>(null);

        public Task ReplaceForSeriesAsync(
            SeriesId seriesId,
            IReadOnlyList<DetectedMarker> markers,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveAsync(DetectedMarker marker, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid markerId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
