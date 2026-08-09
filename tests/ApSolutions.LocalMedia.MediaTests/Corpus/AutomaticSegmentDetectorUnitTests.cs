using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Media;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Corpus;

/// <summary>
/// The detector's edges, driven with synthetic fingerprints so each case is exact: too few
/// episodes, recurrence without enough support, an episode with no window, a piece that occurs
/// twice in one episode, two recurrences that never share an episode, and a one-second dropout.
/// </summary>
public sealed class AutomaticSegmentDetectorUnitTests
{
    private static readonly SeriesId Series = new(Guid.Parse("d3ec0001-0000-4000-8000-000000000001"));

    [Fact]
    public void The_detector_refuses_to_exist_without_an_extractor()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new AutomaticSegmentDetector(null!));
    }

    [Fact]
    public async Task A_single_episode_proves_nothing_and_nothing_is_read()
    {
        var extractor = new MapExtractor([]);
        var detector = new AutomaticSegmentDetector(extractor);

        var detection = await detector.DetectAsync(
            Series,
            [Episode(0)],
            progress: null,
            TestContext.Current.CancellationToken);

        Assert.Empty(detection.Segments);
        Assert.Equal(0, extractor.Extractions);
    }

    [Fact]
    public async Task A_piece_shared_by_only_one_other_episode_is_not_recurrence_enough()
    {
        // Three episodes, but the shared piece exists in two: every second has one supporter and
        // the minimum is two, so runs exist and still no interval survives.
        var shared = Content("piece", 15);
        var prints = new Dictionary<int, EpisodeAudioFingerprints>
        {
            [0] = Fingerprints(0, opening: [.. shared, .. Content("solo-0", 45)], closing: Content("out-0", 30)),
            [1] = Fingerprints(1, opening: [.. shared, .. Content("solo-1", 45)], closing: Content("out-1", 30)),
            [2] = Fingerprints(2, opening: Content("solo-2", 60), closing: Content("out-2", 30)),
        };

        var detection = await Detect(prints);

        Assert.Empty(detection.Segments);
    }

    [Fact]
    public async Task An_episode_with_an_empty_window_is_skipped_without_disturbing_the_rest()
    {
        var credits = Content("credits", 20);
        var prints = new Dictionary<int, EpisodeAudioFingerprints>
        {
            [0] = Fingerprints(0, opening: Content("open-0", 30), closing: [.. Content("tail-0", 20), .. credits]),
            [1] = Fingerprints(1, opening: Content("open-1", 30), closing: [.. Content("tail-1", 20), .. credits]),
            [2] = Fingerprints(2, opening: Content("open-2", 30), closing: [.. Content("tail-2", 20), .. credits]),
            [3] = Fingerprints(3, opening: Content("open-3", 30), closing: []),
        };

        var detection = await Detect(prints);

        Assert.Equal(3, detection.Segments.Count(segment => segment.Kind == MarkerKind.Credits));
        Assert.DoesNotContain(detection.Segments, segment => segment.FileId == FileOf(3));
    }

    [Fact]
    public async Task A_piece_that_occurs_twice_in_one_episode_becomes_one_span_not_two_markers()
    {
        var theme = Content("theme", 15);
        var prints = new Dictionary<int, EpisodeAudioFingerprints>
        {
            [0] = Fingerprints(
                0,
                opening: [.. theme, .. Content("between", 10), .. theme, .. Content("solo-0", 20)],
                closing: Content("out-0", 30)),
            [1] = Fingerprints(1, opening: [.. theme, .. Content("solo-1", 45)], closing: Content("out-1", 30)),
            [2] = Fingerprints(2, opening: [.. theme, .. Content("solo-2", 45)], closing: Content("out-2", 30)),
        };

        var detection = await Detect(prints);

        var introsInDouble = detection.Segments
            .Where(segment => segment.FileId == FileOf(0) && segment.Kind == MarkerKind.Intro)
            .ToList();
        var merged = Assert.Single(introsInDouble);
        Assert.Equal(TimeSpan.Zero, merged.Start);
        Assert.Equal(TimeSpan.FromSeconds(40), merged.End);
    }

    [Fact]
    public async Task Two_recurrences_that_never_share_an_episode_cannot_name_a_recap()
    {
        var themeA = Content("theme-a", 15);
        var themeB = Content("theme-b", 15);
        var prints = new Dictionary<int, EpisodeAudioFingerprints>();
        for (var episode = 0; episode < 3; episode++)
        {
            prints[episode] = Fingerprints(
                episode,
                opening: [.. themeA, .. Content($"solo-{episode}", 45)],
                closing: Content($"out-{episode}", 30));
        }

        for (var episode = 3; episode < 6; episode++)
        {
            prints[episode] = Fingerprints(
                episode,
                opening: [.. themeB, .. Content($"solo-{episode}", 45)],
                closing: Content($"out-{episode}", 30));
        }

        var detection = await Detect(prints);

        Assert.Equal(3, detection.Segments.Count(segment => segment.Kind == MarkerKind.Intro));
        Assert.DoesNotContain(detection.Segments, segment => segment.Kind == MarkerKind.Recap);
    }

    [Fact]
    public async Task A_single_sour_second_inside_an_intro_does_not_split_it()
    {
        var intro = Content("intro", 16);
        var dented = intro.ToArray();
        dented[7] = Content("dent", 1)[0];
        var prints = new Dictionary<int, EpisodeAudioFingerprints>
        {
            [0] = Fingerprints(0, opening: [.. dented, .. Content("solo-0", 40)], closing: Content("out-0", 30)),
            [1] = Fingerprints(1, opening: [.. intro, .. Content("solo-1", 40)], closing: Content("out-1", 30)),
            [2] = Fingerprints(2, opening: [.. intro, .. Content("solo-2", 40)], closing: Content("out-2", 30)),
        };

        var detection = await Detect(prints);

        var dentedIntro = Assert.Single(
            detection.Segments,
            segment => segment.FileId == FileOf(0) && segment.Kind == MarkerKind.Intro);
        Assert.True(
            (dentedIntro.End - dentedIntro.Start).TotalSeconds >= 14,
            $"The dented intro shrank to {(dentedIntro.End - dentedIntro.Start).TotalSeconds} s.");
    }

    [Fact]
    public async Task Nothing_is_read_until_the_active_playback_ends()
    {
        var theme = Content("pause-theme", 15);
        var prints = new Dictionary<int, EpisodeAudioFingerprints>
        {
            [0] = Fingerprints(0, opening: [.. theme, .. Content("solo-0", 45)], closing: Content("out-0", 30)),
            [1] = Fingerprints(1, opening: [.. theme, .. Content("solo-1", 45)], closing: Content("out-1", 30)),
        };
        var playback = new TogglePlayback { IsPlaybackActive = true };
        var extractor = new MapExtractor(prints);
        var detector = new AutomaticSegmentDetector(extractor, playback);

        var detecting = Task.Run(() => detector.DetectAsync(
            Series,
            [.. prints.Keys.Order().Select(Episode)],
            progress: null,
            TestContext.Current.CancellationToken));
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.Equal(0, extractor.Extractions);
        playback.IsPlaybackActive = false;
        _ = await detecting;

        Assert.Equal(2, extractor.Extractions);
    }

    private sealed class TogglePlayback : ApSolutions.LocalMedia.Application.Continuity.IPlaybackActivity
    {
        public bool IsPlaybackActive { get; set; }
    }

    private static async Task<SeriesSegmentDetection> Detect(
        Dictionary<int, EpisodeAudioFingerprints> prints)
    {
        var detector = new AutomaticSegmentDetector(new MapExtractor(prints));
        return await detector.DetectAsync(
            Series,
            [.. prints.Keys.Order().Select(Episode)],
            progress: null,
            TestContext.Current.CancellationToken);
    }

    private static MediaFileId FileOf(int index) =>
        new(Guid.Parse(FormattableString.Invariant($"d3ec0001-0000-4000-8000-0000000000{index + 1:x2}")));

    /// <summary>
    /// BUG-009's other half: the detector clamps what it emits to the episode it measured, so a
    /// recurring window that runs past a short episode's end is trimmed at the source instead of
    /// stored as a range no playback can reach.
    /// </summary>
    [Fact]
    public async Task A_detected_range_never_outruns_the_episode_it_was_measured_in()
    {
        var shared = Content("intro", 60);
        var episodeDuration = TimeSpan.FromSeconds(50);
        var prints = new Dictionary<int, EpisodeAudioFingerprints>();
        for (var index = 0; index < 3; index++)
        {
            prints[index] = new EpisodeAudioFingerprints(
                FileOf(index),
                episodeDuration,
                shared,
                episodeDuration - TimeSpan.FromSeconds(10),
                Content($"out-{index}", 10));
        }

        var detector = new AutomaticSegmentDetector(new MapExtractor(prints));
        var detection = await detector.DetectAsync(
            Series,
            [Episode(0), Episode(1), Episode(2)],
            progress: null,
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(detection.Segments);
        Assert.All(detection.Segments, segment =>
        {
            Assert.True(
                segment.End <= episodeDuration,
                $"{segment.Kind} ends at {segment.End}, past the {episodeDuration} episode.");
            Assert.True(segment.Start < segment.End);
        });
    }

    private static SegmentDetectionEpisode Episode(int index) => new(
        FileOf(index),
        FormattableString.Invariant($@"D:\Media\Synthetic\E{index + 1:D2}.mkv"),
        TimeSpan.FromMinutes(3));

    private static EpisodeAudioFingerprints Fingerprints(
        int index,
        float[][] opening,
        float[][] closing) => new(
        FileOf(index),
        TimeSpan.FromMinutes(3),
        opening,
        TimeSpan.FromMinutes(3) - TimeSpan.FromSeconds(closing.Length),
        closing);

    /// <summary>One deterministic unit vector per named second; the same name always sounds alike.</summary>
    private static float[][] Content(string name, int seconds)
    {
        var vectors = new float[seconds][];
        for (var second = 0; second < seconds; second++)
        {
            var random = new Random(StringComparer.Ordinal.GetHashCode($"{name}:{second}") & int.MaxValue);
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

            vectors[second] = vector;
        }

        return vectors;
    }

    private sealed class MapExtractor(Dictionary<int, EpisodeAudioFingerprints> prints)
        : ISegmentFeatureExtractor
    {
        public int Extractions { get; private set; }

        public Task<EpisodeAudioFingerprints> ExtractAsync(
            SegmentDetectionEpisode episode,
            CancellationToken cancellationToken)
        {
            Extractions++;
            var index = prints.Values.Single(candidate => candidate.FileId == episode.FileId);
            return Task.FromResult(index);
        }
    }
}
