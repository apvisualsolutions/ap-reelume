// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Continuity;

/// <summary>
/// The whole run, from the switch to the stored rows: nothing happens while detection is off, the
/// policy decides what is kept, a person's review survives the next run, and a cancelled run leaves
/// storage exactly as it was.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AutomaticSegmentDetectionTests
{
    private static readonly TitleId Show = new(Guid.Parse("e8b10004-0000-4000-8000-000000000001"));

    private static readonly SeriesId Series = new(Guid.Parse("e8b10004-0000-4000-8000-000000000002"));

    private static readonly DetectSeriesSegmentsCommand Command = new(Show, Series);

    [Fact]
    public async Task Nothing_runs_while_the_switch_is_off()
    {
        using var directory = new DatabaseTestDirectory();
        var harness = await HarnessAsync(directory, Episodes(3));
        var detector = new ScriptedDetector(_ => []);

        var result = await UseCase(harness, detector).ExecuteAsync(
            Command,
            progress: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(DetectSegmentsOutcome.Disabled, result.Outcome);
        Assert.Equal(0, detector.Calls);
        Assert.Empty(await harness.Repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_enabled_run_reads_only_playable_episodes_and_stores_what_the_policy_keeps()
    {
        using var directory = new DatabaseTestDirectory();
        var episodes = Episodes(3, unavailable: 1);
        var harness = await HarnessAsync(directory, episodes);
        var confident = Segment(FileOf(1), MarkerKind.Intro, 10, 35, 0.9);
        var timid = Segment(FileOf(2), MarkerKind.Intro, 10, 35, 0.2);
        var impossible = Segment(FileOf(3), MarkerKind.Credits, 40, 20, 0.9);
        var detector = new ScriptedDetector(request => [confident, timid, impossible]);
        var useCase = UseCase(harness, detector);
        useCase.SetEnabled(true);

        var result = await useCase.ExecuteAsync(Command, progress: null, TestContext.Current.CancellationToken);

        Assert.Equal(DetectSegmentsOutcome.Completed, result.Outcome);
        Assert.Equal(3, result.EpisodesRead);
        Assert.Equal(1, result.MarkersStored);
        Assert.Equal(3, Assert.Single(detector.Requests).Count);
        var row = Assert.Single(
            await harness.Repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken));
        Assert.Equal(FileOf(1), row.FileId);
        Assert.Equal(MarkerKind.Intro, row.Kind);
        Assert.Equal(TimeSpan.FromSeconds(10), row.Start);
        Assert.False(row.UserCorrected);
    }

    [Fact]
    public async Task A_persons_review_survives_the_next_run()
    {
        using var directory = new DatabaseTestDirectory();
        var harness = await HarnessAsync(directory, Episodes(2));
        var first = new ScriptedDetector(_ =>
        [
            Segment(FileOf(1), MarkerKind.Intro, 10, 35, 0.9),
            Segment(FileOf(2), MarkerKind.Intro, 8, 33, 0.9),
        ]);
        var useCase = UseCase(harness, first);
        useCase.SetEnabled(true);
        _ = await useCase.ExecuteAsync(Command, progress: null, TestContext.Current.CancellationToken);
        var review = new ReviewDetectedSegments(harness.Repository);
        var stored = await harness.Repository.GetForFileAsync(FileOf(1), TestContext.Current.CancellationToken);
        var corrected = await review.CorrectAsync(
            Assert.Single(stored).Id,
            TimeSpan.FromSeconds(12),
            TimeSpan.FromSeconds(37),
            duration: null,
            TestContext.Current.CancellationToken);
        Assert.NotNull(corrected);

        var second = new ScriptedDetector(_ => [Segment(FileOf(1), MarkerKind.Intro, 50, 80, 0.95)]);
        var secondRun = UseCase(harness, second);
        secondRun.SetEnabled(true);
        _ = await secondRun.ExecuteAsync(Command, progress: null, TestContext.Current.CancellationToken);

        var rows = await harness.Repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken);
        var survivor = Assert.Single(rows);
        Assert.Equal(corrected!.Id, survivor.Id);
        Assert.True(survivor.UserCorrected);
        Assert.Equal(TimeSpan.FromSeconds(12), survivor.Start);
    }

    [Fact]
    public async Task A_cancelled_run_leaves_the_stored_rows_exactly_as_they_were()
    {
        using var directory = new DatabaseTestDirectory();
        var harness = await HarnessAsync(directory, Episodes(2));
        var seed = new ScriptedDetector(_ => [Segment(FileOf(1), MarkerKind.Intro, 10, 35, 0.9)]);
        var useCase = UseCase(harness, seed);
        useCase.SetEnabled(true);
        _ = await useCase.ExecuteAsync(Command, progress: null, TestContext.Current.CancellationToken);
        var before = await harness.Repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken);

        var cancelling = UseCase(harness, new CancellingDetector());
        cancelling.SetEnabled(true);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cancelling.ExecuteAsync(Command, progress: null, TestContext.Current.CancellationToken));

        Assert.Equal(
            before,
            await harness.Repository.GetForSeriesAsync(Series, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_series_with_nothing_playable_reads_nothing()
    {
        using var directory = new DatabaseTestDirectory();
        var harness = await HarnessAsync(directory, Episodes(0, unavailable: 2));
        var detector = new ScriptedDetector(_ => []);
        var useCase = UseCase(harness, detector);
        useCase.SetEnabled(true);

        var result = await useCase.ExecuteAsync(Command, progress: null, TestContext.Current.CancellationToken);

        Assert.Equal(DetectSegmentsOutcome.NothingToRead, result.Outcome);
        Assert.Equal(0, detector.Calls);
    }

    [Fact]
    public async Task Progress_flows_from_the_detector_to_the_caller()
    {
        using var directory = new DatabaseTestDirectory();
        var harness = await HarnessAsync(directory, Episodes(2));
        var detector = new ScriptedDetector(_ => [], reportProgress: true);
        var useCase = UseCase(harness, detector);
        useCase.SetEnabled(true);
        var received = new List<SegmentDetectionProgress>();

        _ = await useCase.ExecuteAsync(
            Command,
            new ImmediateProgress(received.Add),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, received.Count);
        Assert.Equal(new SegmentDetectionProgress(2, 2), received[^1]);
    }

    [Fact]
    public async Task Review_accepts_locks_corrects_validates_and_deletes()
    {
        using var directory = new DatabaseTestDirectory();
        var harness = await HarnessAsync(directory, Episodes(1));
        var detector = new ScriptedDetector(_ =>
        [
            Segment(FileOf(1), MarkerKind.Intro, 10, 35, 0.9),
            Segment(FileOf(1), MarkerKind.Credits, 150, 175, 0.9),
        ]);
        var useCase = UseCase(harness, detector);
        useCase.SetEnabled(true);
        _ = await useCase.ExecuteAsync(Command, progress: null, TestContext.Current.CancellationToken);
        var review = new ReviewDetectedSegments(harness.Repository);
        var rows = await review.ListForFileAsync(FileOf(1), TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Count);
        var intro = Assert.Single(rows, row => row.Kind == MarkerKind.Intro);
        var credits = Assert.Single(rows, row => row.Kind == MarkerKind.Credits);

        var accepted = await review.AcceptAsync(intro.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(accepted);
        Assert.True(accepted!.UserCorrected);

        Assert.Null(await review.CorrectAsync(
            credits.Id,
            TimeSpan.FromSeconds(200),
            TimeSpan.FromSeconds(150),
            duration: null,
            TestContext.Current.CancellationToken));
        var untouched = Assert.Single(
            await review.ListForFileAsync(FileOf(1), TestContext.Current.CancellationToken),
            row => row.Kind == MarkerKind.Credits);
        Assert.False(untouched.UserCorrected);

        Assert.True(await review.DeleteAsync(credits.Id, TestContext.Current.CancellationToken));
        var remaining = await review.ListForFileAsync(FileOf(1), TestContext.Current.CancellationToken);
        Assert.Equal([accepted.Id], remaining.Select(row => row.Id));
    }

    private static MediaFileId FileOf(int number) =>
        new(Guid.Parse(FormattableString.Invariant($"e8b10004-0000-4000-8000-0000000000{number:x2}")));

    private static List<EpisodeSequenceEntry> Episodes(int playable, int unavailable = 0)
    {
        var entries = new List<EpisodeSequenceEntry>();
        for (var number = 1; number <= playable + unavailable; number++)
        {
            var available = number <= playable;
            entries.Add(new EpisodeSequenceEntry(
                new EpisodeId(Guid.NewGuid()),
                Show,
                SeasonNumber: 1,
                EpisodeNumber: number,
                available ? FileOf(number) : null,
                available ? FormattableString.Invariant($@"D:\Media\S01E{number:D2}.mkv") : null,
                IsAvailable: available));
        }

        return entries;
    }

    private static DetectedSegment Segment(
        MediaFileId file,
        MarkerKind kind,
        double startSeconds,
        double endSeconds,
        double confidence) =>
        new(file, kind, TimeSpan.FromSeconds(startSeconds), TimeSpan.FromSeconds(endSeconds), confidence);

    private static async Task<Harness> HarnessAsync(
        DatabaseTestDirectory directory,
        IReadOnlyList<EpisodeSequenceEntry> episodes)
    {
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        return new Harness(
            new InMemorySettingsStore(),
            new FakeEpisodeSequenceRepository(episodes),
            new DetectedMarkerRepository(factory));
    }

    private static DetectSeriesSegments UseCase(Harness harness, IAutomaticSegmentDetector detector) =>
        new(harness.Settings, harness.Episodes, detector, harness.Repository);

    private sealed record Harness(
        InMemorySettingsStore Settings,
        FakeEpisodeSequenceRepository Episodes,
        DetectedMarkerRepository Repository);

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, object?> _values = [];

        public T? Read<T>(string key) =>
            _values.TryGetValue(key, out var value) && value is T typed ? typed : default;

        public void Write<T>(string key, T value) => _values[key] = value;
    }

    private sealed class FakeEpisodeSequenceRepository(IReadOnlyList<EpisodeSequenceEntry> episodes)
        : IEpisodeSequenceRepository
    {
        public Task<IReadOnlyList<EpisodeSequenceEntry>> GetSeriesAsync(
            TitleId showId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EpisodeSequenceEntry>>(
                [.. episodes.Where(entry => entry.ShowId == showId)]);

        public Task<EpisodeSequenceEntry?> GetAsync(
            EpisodeId episodeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(episodes.FirstOrDefault(entry => entry.Id == episodeId));

        public Task<EpisodeSequenceEntry?> FindByFileAsync(
            MediaFileId fileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(episodes.FirstOrDefault(entry => entry.MediaFileId == fileId));
    }

    /// <summary>Returns a scripted result and remembers exactly what it was asked to read.</summary>
    private sealed class ScriptedDetector(
        Func<IReadOnlyList<SegmentDetectionEpisode>, IReadOnlyList<DetectedSegment>> script,
        bool reportProgress = false) : IAutomaticSegmentDetector
    {
        public int Calls { get; private set; }

        public List<IReadOnlyList<SegmentDetectionEpisode>> Requests { get; } = [];

        public int Version => 1;

        public Task<SeriesSegmentDetection> DetectAsync(
            SeriesId seriesId,
            IReadOnlyList<SegmentDetectionEpisode> episodes,
            IProgress<SegmentDetectionProgress>? progress,
            CancellationToken cancellationToken)
        {
            Calls++;
            Requests.Add(episodes);
            if (reportProgress)
            {
                for (var processed = 1; processed <= episodes.Count; processed++)
                {
                    progress?.Report(new SegmentDetectionProgress(processed, episodes.Count));
                }
            }

            return Task.FromResult(new SeriesSegmentDetection(seriesId, Version, script(episodes)));
        }
    }

    private sealed class CancellingDetector : IAutomaticSegmentDetector
    {
        public int Version => 1;

        public Task<SeriesSegmentDetection> DetectAsync(
            SeriesId seriesId,
            IReadOnlyList<SegmentDetectionEpisode> episodes,
            IProgress<SegmentDetectionProgress>? progress,
            CancellationToken cancellationToken) =>
            throw new OperationCanceledException();
    }

    /// <summary>Delivers reports on the calling thread, so a test can assert them deterministically.</summary>
    private sealed class ImmediateProgress(Action<SegmentDetectionProgress> onReport)
        : IProgress<SegmentDetectionProgress>
    {
        public void Report(SegmentDetectionProgress value) => onReport(value);
    }
}
