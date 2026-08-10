// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Infrastructure.Media;

/// <summary>
/// Finds recurring audio across the episodes of one series and classifies the runs into intro,
/// recap, and credits. Everything happens locally over extracted fingerprints; the detector yields
/// to an active playback between episodes and honours cancellation promptly.
/// </summary>
public sealed class AutomaticSegmentDetector : IAutomaticSegmentDetector
{
    /// <summary>Two seconds sound alike above this; the corpus benchmark arbitrates the value.</summary>
    private const double SimilarityThreshold = 0.80;

    /// <summary>
    /// The shortest recurrence worth calling a segment. The corpus guarantees unique material never
    /// shares a run this long by accident, and the shortest real segment in it is twelve seconds.
    /// </summary>
    private const int MinimumRunSeconds = 10;

    private static readonly TimeSpan PlaybackYield = TimeSpan.FromMilliseconds(500);

    private readonly ISegmentFeatureExtractor _extractor;
    private readonly IPlaybackActivity? _playback;

    public AutomaticSegmentDetector(ISegmentFeatureExtractor extractor, IPlaybackActivity? playback = null)
    {
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _playback = playback;
    }

    public int Version => 1;

    public async Task<SeriesSegmentDetection> DetectAsync(
        SeriesId seriesId,
        IReadOnlyList<SegmentDetectionEpisode> episodes,
        IProgress<SegmentDetectionProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        if (episodes.Count < 2)
        {
            // Recurrence needs something to recur across; one episode proves nothing.
            return new SeriesSegmentDetection(seriesId, Version, []);
        }

        var prints = new List<EpisodeAudioFingerprints>(episodes.Count);
        for (var index = 0; index < episodes.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            while (_playback?.IsPlaybackActive == true)
            {
                // Detection always yields to the person actually watching something.
                await Task.Delay(PlaybackYield, cancellationToken).ConfigureAwait(false);
            }

            prints.Add(await _extractor.ExtractAsync(episodes[index], cancellationToken).ConfigureAwait(false));
            progress?.Report(new SegmentDetectionProgress(index + 1, episodes.Count));
        }

        var segments = new List<DetectedSegment>();
        segments.AddRange(AnalyseWindow(
            prints,
            print => print.Opening,
            print => TimeSpan.Zero,
            isOpening: true));
        segments.AddRange(AnalyseWindow(
            prints,
            print => print.Closing,
            print => print.ClosingStart,
            isOpening: false));
        return new SeriesSegmentDetection(seriesId, Version, segments);
    }

    /// <summary>One recurring stretch of one episode, with the episodes that vouch for it.</summary>
    private sealed record RecurringInterval(int Episode, int Start, int End)
    {
        public HashSet<int> Supporters { get; } = [];

        public int ClusterRoot { get; set; } = -1;
    }

    private sealed record MatchRun(int EpisodeA, int EpisodeB, int StartA, int StartB, int Length);

    private static List<DetectedSegment> AnalyseWindow(
        List<EpisodeAudioFingerprints> prints,
        Func<EpisodeAudioFingerprints, IReadOnlyList<float[]>> windowOf,
        Func<EpisodeAudioFingerprints, TimeSpan> offsetOf,
        bool isOpening)
    {
        var count = prints.Count;
        var runs = new List<MatchRun>();
        for (var first = 0; first < count; first++)
        {
            for (var second = first + 1; second < count; second++)
            {
                runs.AddRange(FindRuns(first, second, windowOf(prints[first]), windowOf(prints[second])));
            }
        }

        if (runs.Count == 0)
        {
            return [];
        }

        var minimumSupport = Math.Max(2, (int)Math.Ceiling(0.4 * (count - 1)));
        var intervals = BuildIntervals(runs, count, prints, windowOf, minimumSupport);
        if (intervals.Count == 0)
        {
            return [];
        }

        var clusters = Cluster(runs, intervals);
        return Classify(clusters, prints, offsetOf, isOpening, count);
    }

    /// <summary>
    /// Every diagonal stretch where the two windows sound alike for long enough. A single sour
    /// second inside a stretch is tolerated; encoded audio is not bit-exact.
    /// </summary>
    private static List<MatchRun> FindRuns(
        int episodeA,
        int episodeB,
        IReadOnlyList<float[]> windowA,
        IReadOnlyList<float[]> windowB)
    {
        var runs = new List<MatchRun>();
        var lengthA = windowA.Count;
        var lengthB = windowB.Count;
        for (var diagonal = -(lengthB - 1); diagonal < lengthA; diagonal++)
        {
            var start = -1;
            var gapUsed = false;
            var a = Math.Max(0, diagonal);
            for (; a < lengthA && a - diagonal < lengthB; a++)
            {
                var matches = LocalSegmentFeatureExtractor.Similarity(
                    windowA[a],
                    windowB[a - diagonal]) >= SimilarityThreshold;
                if (matches)
                {
                    if (start < 0)
                    {
                        start = a;
                        gapUsed = false;
                    }

                    continue;
                }

                if (start >= 0 && !gapUsed
                    && a + 1 < lengthA && a + 1 - diagonal < lengthB
                    && LocalSegmentFeatureExtractor.Similarity(
                        windowA[a + 1],
                        windowB[a + 1 - diagonal]) >= SimilarityThreshold)
                {
                    gapUsed = true;
                    continue;
                }

                CloseRun(runs, episodeA, episodeB, diagonal, start, a, gapUsed);
                start = -1;
            }

            CloseRun(runs, episodeA, episodeB, diagonal, start, a, gapUsed: false);
        }

        return runs;
    }

    private static void CloseRun(
        List<MatchRun> runs,
        int episodeA,
        int episodeB,
        int diagonal,
        int start,
        int end,
        bool gapUsed)
    {
        if (start < 0)
        {
            return;
        }

        // A run that ended on its tolerated gap does not own that last second.
        var length = end - start - (gapUsed ? 1 : 0);
        if (length >= MinimumRunSeconds)
        {
            runs.Add(new MatchRun(episodeA, episodeB, start, start - diagonal, length));
        }
    }

    /// <summary>
    /// Turns pairwise runs into per-episode intervals via a per-second support profile, splitting
    /// where the level of support steps — that is where a recap ends and an intro begins.
    /// </summary>
    private static List<RecurringInterval> BuildIntervals(
        List<MatchRun> runs,
        int episodeCount,
        List<EpisodeAudioFingerprints> prints,
        Func<EpisodeAudioFingerprints, IReadOnlyList<float[]>> windowOf,
        int minimumSupport)
    {
        var intervals = new List<RecurringInterval>();
        for (var episode = 0; episode < episodeCount; episode++)
        {
            var windowLength = windowOf(prints[episode]).Count;
            if (windowLength == 0)
            {
                continue;
            }

            var support = new HashSet<int>[windowLength];
            for (var second = 0; second < windowLength; second++)
            {
                support[second] = [];
            }

            foreach (var run in runs)
            {
                if (run.EpisodeA == episode)
                {
                    Cover(support, run.StartA, run.Length, run.EpisodeB);
                }
                else if (run.EpisodeB == episode)
                {
                    Cover(support, run.StartB, run.Length, run.EpisodeA);
                }
            }

            var start = -1;
            for (var second = 0; second <= windowLength; second++)
            {
                var supported = second < windowLength && support[second].Count >= minimumSupport;
                var stepped = start >= 0 && second < windowLength && supported
                    && Math.Abs(support[second].Count - support[second - 1].Count) >= 2;
                if (supported && start >= 0 && !stepped)
                {
                    continue;
                }

                if (start >= 0 && second - start >= MinimumRunSeconds)
                {
                    var interval = new RecurringInterval(episode, start, second);
                    for (var covered = start; covered < second; covered++)
                    {
                        interval.Supporters.UnionWith(support[covered]);
                    }

                    intervals.Add(interval);
                }

                start = supported ? second : -1;
            }
        }

        return intervals;
    }

    private static void Cover(HashSet<int>[] support, int start, int length, int supporter)
    {
        var end = Math.Min(support.Length, start + length);
        for (var second = Math.Max(0, start); second < end; second++)
        {
            _ = support[second].Add(supporter);
        }
    }

    /// <summary>
    /// Links intervals across episodes when a pairwise run maps one onto the other; the connected
    /// components are the series' recurring pieces of content.
    /// </summary>
    private static List<List<RecurringInterval>> Cluster(
        List<MatchRun> runs,
        List<RecurringInterval> intervals)
    {
        var parents = Enumerable.Range(0, intervals.Count).ToArray();

        int Find(int node) => parents[node] == node ? node : parents[node] = Find(parents[node]);

        void Union(int first, int second) => parents[Find(first)] = Find(second);

        foreach (var run in runs)
        {
            for (var left = 0; left < intervals.Count; left++)
            {
                var intervalA = intervals[left];
                if (intervalA.Episode != run.EpisodeA)
                {
                    continue;
                }

                var overlapStart = Math.Max(intervalA.Start, run.StartA);
                var overlapEnd = Math.Min(intervalA.End, run.StartA + run.Length);
                if (overlapEnd - overlapStart < MinimumRunSeconds / 2)
                {
                    continue;
                }

                var imageStart = run.StartB + (overlapStart - run.StartA);
                var imageEnd = run.StartB + (overlapEnd - run.StartA);
                for (var right = 0; right < intervals.Count; right++)
                {
                    var intervalB = intervals[right];
                    if (intervalB.Episode != run.EpisodeB)
                    {
                        continue;
                    }

                    var mapped = Math.Min(intervalB.End, imageEnd) - Math.Max(intervalB.Start, imageStart);
                    if (mapped >= MinimumRunSeconds / 2)
                    {
                        Union(left, right);
                    }
                }
            }
        }

        return intervals
            .Select((interval, index) => (Interval: interval, Root: Find(index)))
            .GroupBy(entry => entry.Root)
            .Select(group => group.Select(entry => entry.Interval).ToList())
            .ToList();
    }

    /// <summary>
    /// Names the clusters. In the opening window the cluster most episodes share is the intro, and a
    /// cluster that consistently precedes it is the recap; the closing window holds the credits.
    /// </summary>
    private static List<DetectedSegment> Classify(
        List<List<RecurringInterval>> clusters,
        List<EpisodeAudioFingerprints> prints,
        Func<EpisodeAudioFingerprints, TimeSpan> offsetOf,
        bool isOpening,
        int episodeCount)
    {
        var segments = new List<DetectedSegment>();
        var eligible = clusters
            .Select(cluster => Consolidate(cluster))
            .Where(cluster => cluster.Count >= 2)
            .OrderByDescending(cluster => cluster.Count)
            .ToList();
        if (eligible.Count == 0)
        {
            return segments;
        }

        if (!isOpening)
        {
            foreach (var cluster in eligible)
            {
                Emit(segments, cluster, MarkerKind.Credits, prints, offsetOf, episodeCount);
            }

            return segments;
        }

        var intro = eligible[0];
        Emit(segments, intro, MarkerKind.Intro, prints, offsetOf, episodeCount);
        foreach (var cluster in eligible.Skip(1))
        {
            var sharedEpisodes = cluster.Keys.Where(intro.ContainsKey).ToList();
            if (sharedEpisodes.Count == 0)
            {
                continue;
            }

            var precedesIntro = sharedEpisodes.All(episode =>
                cluster[episode].Start < intro[episode].Start);
            if (precedesIntro)
            {
                Emit(segments, cluster, MarkerKind.Recap, prints, offsetOf, episodeCount);
            }
        }

        return segments;
    }

    /// <summary>One interval per episode inside a cluster; overlapping members are merged.</summary>
    private static Dictionary<int, RecurringInterval> Consolidate(List<RecurringInterval> cluster)
    {
        var byEpisode = new Dictionary<int, RecurringInterval>();
        foreach (var interval in cluster)
        {
            if (byEpisode.TryGetValue(interval.Episode, out var existing))
            {
                var merged = new RecurringInterval(
                    interval.Episode,
                    Math.Min(existing.Start, interval.Start),
                    Math.Max(existing.End, interval.End));
                merged.Supporters.UnionWith(existing.Supporters);
                merged.Supporters.UnionWith(interval.Supporters);
                byEpisode[interval.Episode] = merged;
            }
            else
            {
                byEpisode[interval.Episode] = interval;
            }
        }

        return byEpisode;
    }

    private static void Emit(
        List<DetectedSegment> segments,
        Dictionary<int, RecurringInterval> cluster,
        MarkerKind kind,
        List<EpisodeAudioFingerprints> prints,
        Func<EpisodeAudioFingerprints, TimeSpan> offsetOf,
        int episodeCount)
    {
        foreach (var (episode, interval) in cluster)
        {
            var offset = offsetOf(prints[episode]);
            var supportShare = (double)(cluster.Count - 1) / Math.Max(1, episodeCount - 1);

            // The emitted range is clamped to the episode it was measured in (BUG-009): a
            // recurring window that runs past a short episode's end would otherwise be stored as
            // a range no playback can ever reach.
            var duration = prints[episode].Duration;
            var start = offset + TimeSpan.FromSeconds(interval.Start);
            var end = offset + TimeSpan.FromSeconds(interval.End);
            if (duration > TimeSpan.Zero)
            {
                if (start >= duration)
                {
                    continue;
                }

                end = end > duration ? duration : end;
            }

            if (end <= start)
            {
                continue;
            }

            segments.Add(new DetectedSegment(
                prints[episode].FileId,
                kind,
                start,
                end,
                Confidence: Math.Min(1.0, 0.7 + (0.3 * supportShare))));
        }
    }
}
