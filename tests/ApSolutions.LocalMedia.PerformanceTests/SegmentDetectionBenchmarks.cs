using System.Diagnostics;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.PerformanceTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.PerformanceTests;

/// <summary>
/// Detection next to playback. The subspec promises two measurable things: nothing is extracted
/// while a playback is active, and the analysis of a full series is bounded work that never makes
/// a player miss its beat. Both are measured here, not declared.
/// </summary>
public sealed class SegmentDetectionBenchmarks
{
    /// <summary>The specification's dropout budget: nothing attributable to detection above this.</summary>
    private const double DropoutBudgetMilliseconds = 250;

    /// <summary>Analysing twelve episodes is seconds of work; a blowup here is an algorithm defect.</summary>
    private const double AnalysisBudgetMilliseconds = 10_000;

    private static readonly SeriesId Series = new(Guid.Parse("beac0001-0000-4000-8000-000000000001"));

    [Fact]
    public async Task Nothing_is_extracted_while_a_playback_is_active()
    {
        var playback = new TogglePlayback { IsPlaybackActive = true };
        var clock = Stopwatch.StartNew();
        var extractor = new SyntheticExtractor(clock);
        var detector = new AutomaticSegmentDetector(extractor, playback);
        var episodes = Episodes(4);

        var detecting = Task.Run(() => detector.DetectAsync(
            Series,
            episodes,
            progress: null,
            TestContext.Current.CancellationToken));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        var playbackEnded = clock.Elapsed;
        playback.IsPlaybackActive = false;
        _ = await detecting;

        Assert.Equal(episodes.Count, extractor.ExtractionTimes.Count);
        var earliest = extractor.ExtractionTimes.Min();
        Assert.True(
            earliest >= playbackEnded,
            $"Extraction began {earliest.TotalMilliseconds:F0} ms in, while playback was active "
            + $"until {playbackEnded.TotalMilliseconds:F0} ms.");
    }

    [Fact]
    public async Task Analysing_a_series_is_bounded_and_never_costs_a_player_its_beat()
    {
        var clock = Stopwatch.StartNew();
        var extractor = new SyntheticExtractor(clock);
        var detector = new AutomaticSegmentDetector(extractor);
        var episodes = Episodes(12);
        using var playing = new CancellationTokenSource();
        var gaps = new List<double>();

        var player = Task.Factory.StartNew(
            () =>
            {
                var beat = Stopwatch.StartNew();
                var previous = beat.Elapsed;
                while (!playing.IsCancellationRequested)
                {
                    Thread.Sleep(20);
                    var now = beat.Elapsed;
                    gaps.Add((now - previous).TotalMilliseconds);
                    previous = now;
                }
            },
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        var analysis = Stopwatch.StartNew();
        var detection = await Task.Run(() => detector.DetectAsync(
            Series,
            episodes,
            progress: null,
            TestContext.Current.CancellationToken));
        analysis.Stop();
        await playing.CancelAsync();
        await player;

        // The workload has to be real before its cost means anything: the synthetic series carries
        // a recurring intro and recurring credits, and the detector must have found them.
        Assert.Equal(episodes.Count, detection.Segments.Count(segment => segment.Kind == MarkerKind.Intro));
        Assert.Equal(episodes.Count, detection.Segments.Count(segment => segment.Kind == MarkerKind.Credits));

        Assert.NotEmpty(gaps);
        var worst = gaps.Max();
        var ordered = gaps.Order().ToList();
        var samples = new PerformanceSampleSet(
            gaps,
            ordered[gaps.Count / 2],
            ordered[Math.Max(0, (int)Math.Ceiling(gaps.Count * 0.95) - 1)],
            worst);
        await PerformanceEvidence.WriteAsync(
            "playback-beat-during-segment-analysis",
            samples,
            DropoutBudgetMilliseconds,
            TestContext.Current.CancellationToken);

        Assert.True(
            analysis.Elapsed.TotalMilliseconds < AnalysisBudgetMilliseconds,
            $"Analysing twelve episodes took {analysis.Elapsed.TotalMilliseconds:F0} ms.");
        Assert.True(
            worst < DropoutBudgetMilliseconds,
            $"The playback beat lost {worst:F1} ms while a series was being analysed.");
    }

    private static List<SegmentDetectionEpisode> Episodes(int count)
    {
        var episodes = new List<SegmentDetectionEpisode>(count);
        for (var index = 0; index < count; index++)
        {
            episodes.Add(new SegmentDetectionEpisode(
                new MediaFileId(Guid.Parse(FormattableString.Invariant(
                    $"beac0001-0000-4000-8000-0000000000{index + 1:x2}"))),
                FormattableString.Invariant($@"D:\Media\Bench\S01E{index + 1:D2}.mkv"),
                TimeSpan.FromMinutes(20)));
        }

        return episodes;
    }

    private sealed class TogglePlayback : IPlaybackActivity
    {
        public bool IsPlaybackActive { get; set; }
    }

    /// <summary>
    /// Produces fingerprints shaped like the real corpus — a variable cold open, a shared intro,
    /// unique body, shared credits — without decoding anything, so the measurement isolates the
    /// detector's own cost.
    /// </summary>
    private sealed class SyntheticExtractor(Stopwatch clock) : ISegmentFeatureExtractor
    {
        private readonly List<TimeSpan> _extractionTimes = [];
        private readonly Dictionary<int, float[]> _shared = [];

        public List<TimeSpan> ExtractionTimes => _extractionTimes;

        public Task<EpisodeAudioFingerprints> ExtractAsync(
            SegmentDetectionEpisode episode,
            CancellationToken cancellationToken)
        {
            lock (_extractionTimes)
            {
                _extractionTimes.Add(clock.Elapsed);
            }

            var seed = episode.FileId.Value.GetHashCode();
            var coldOpen = 5 + Math.Abs(seed % 40);
            var opening = new List<float[]>();
            var random = new Random(seed);
            for (var second = 0; second < coldOpen; second++)
            {
                opening.Add(Unique(random));
            }

            for (var second = 0; second < 25; second++)
            {
                opening.Add(Shared(second));
            }

            while (opening.Count < 180)
            {
                opening.Add(Unique(random));
            }

            var closing = new List<float[]>();
            for (var second = 0; second < 60; second++)
            {
                closing.Add(Unique(random));
            }

            for (var second = 0; second < 30; second++)
            {
                closing.Add(Shared(1000 + second));
            }

            return Task.FromResult(new EpisodeAudioFingerprints(
                episode.FileId,
                TimeSpan.FromMinutes(20),
                opening,
                TimeSpan.FromMinutes(20) - TimeSpan.FromSeconds(90),
                closing));
        }

        private float[] Shared(int key)
        {
            if (!_shared.TryGetValue(key, out var vector))
            {
                vector = Unique(new Random(key));
                _shared[key] = vector;
            }

            return vector;
        }

        private static float[] Unique(Random random)
        {
            var vector = new float[32];
            double mean = 0;
            for (var band = 0; band < vector.Length; band++)
            {
                vector[band] = (float)random.NextDouble();
                mean += vector[band];
            }

            mean /= vector.Length;
            double norm = 0;
            for (var band = 0; band < vector.Length; band++)
            {
                vector[band] -= (float)mean;
                norm += vector[band] * vector[band];
            }

            var scale = (float)(1 / Math.Sqrt(norm));
            for (var band = 0; band < vector.Length; band++)
            {
                vector[band] *= scale;
            }

            return vector;
        }
    }
}
