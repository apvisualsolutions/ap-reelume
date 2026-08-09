using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.MediaTests.Fixtures;

namespace ApSolutions.LocalMedia.MediaTests.Corpus;

/// <summary>Precision and recall of one marker kind over one corpus split.</summary>
internal sealed record KindMetrics(int Truth, int Emitted, int Hits)
{
    /// <summary>With nothing emitted there is nothing imprecise; recall is what fails then.</summary>
    public double Precision => Emitted == 0 ? 1.0 : (double)Hits / Emitted;

    public double Recall => Truth == 0 ? 1.0 : (double)Hits / Truth;
}

/// <summary>One series' own line in the report, so an average can never hide it.</summary>
internal sealed record SeriesMetrics(
    string SeriesId,
    int Episodes,
    int Emitted,
    int Hits,
    int SegmentFreeEpisodes,
    int SpuriousEpisodes)
{
    public double Precision => Emitted == 0 ? 1.0 : (double)Hits / Emitted;

    public double SpuriousRate =>
        SegmentFreeEpisodes == 0 ? 0.0 : (double)SpuriousEpisodes / SegmentFreeEpisodes;
}

/// <summary>What one detector achieved against one split of the frozen corpus.</summary>
internal sealed record BenchmarkReport(
    string Detector,
    int DetectorVersion,
    string Split,
    KindMetrics Intro,
    KindMetrics Recap,
    KindMetrics Credits,
    int SegmentFreeEpisodes,
    int SpuriousEpisodes,
    IReadOnlyList<SeriesMetrics> Series)
{
    public double AggregateSpuriousRate =>
        SegmentFreeEpisodes == 0 ? 0.0 : (double)SpuriousEpisodes / SegmentFreeEpisodes;

    /// <summary>
    /// Every approved threshold of the frozen subspec this run fails, in words. An empty list is
    /// what `PLY-013` needs; the sensitivity tests need the opposite.
    /// </summary>
    public IReadOnlyList<string> ThresholdFailures()
    {
        var failures = new List<string>();
        Check(failures, "intro precision", Intro.Precision, 0.90);
        Check(failures, "intro recall", Intro.Recall, 0.80);
        Check(failures, "credits precision", Credits.Precision, 0.90);
        Check(failures, "credits recall", Credits.Recall, 0.80);
        Check(failures, "recap precision", Recap.Precision, 0.85);
        Check(failures, "recap recall", Recap.Recall, 0.70);
        if (AggregateSpuriousRate > 0.05)
        {
            failures.Add(FormattableString.Invariant(
                $"aggregate spurious rate {AggregateSpuriousRate:0.###} exceeds 0.05"));
        }

        foreach (var series in Series)
        {
            Check(failures, $"series {series.SeriesId} precision", series.Precision, 0.80);
            if (series.SpuriousRate > 0.10)
            {
                failures.Add(FormattableString.Invariant(
                    $"series {series.SeriesId} spurious rate {series.SpuriousRate:0.###} exceeds 0.10"));
            }
        }

        return failures;
    }

    private static void Check(List<string> failures, string name, double value, double minimum)
    {
        if (value < minimum)
        {
            failures.Add(FormattableString.Invariant($"{name} {value:0.###} is below {minimum:0.##}"));
        }
    }
}

/// <summary>
/// Runs a detector over one split of the frozen corpus and scores it against the ground truth,
/// exactly as the subspec defines a hit: same kind, both boundaries within two seconds.
/// </summary>
internal static class SegmentBenchmark
{
    public static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(2);

    private static readonly JsonSerializerOptions ArchiveOptions = new() { WriteIndented = true };

    public static async Task<BenchmarkReport> EvaluateAsync(
        IAutomaticSegmentDetector detector,
        string detectorName,
        CorpusSplit split,
        bool materialise,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detector);
        var perKind = new Dictionary<MarkerKind, (int Truth, int Emitted, int Hits)>
        {
            [MarkerKind.Intro] = default,
            [MarkerKind.Recap] = default,
            [MarkerKind.Credits] = default,
        };
        var seriesRows = new List<SeriesMetrics>();
        var segmentFreeEpisodes = 0;
        var spuriousEpisodes = 0;

        foreach (var series in SegmentCorpus.Split(split))
        {
            var seriesId = new SeriesId(StableGuid($"series:{series.Id}"));
            var byFile = new Dictionary<MediaFileId, SegmentCorpusEpisode>();
            var episodes = new List<SegmentDetectionEpisode>();
            foreach (var episode in series.Episodes)
            {
                var fileId = new MediaFileId(StableGuid($"episode:{episode.Id}"));
                byFile[fileId] = episode;
                var path = materialise
                    ? await SegmentCorpus.MaterialiseAsync(episode, cancellationToken).ConfigureAwait(false)
                    : Path.Combine(MediaToolchain.OutputRoot, episode.RelativePath);
                episodes.Add(new SegmentDetectionEpisode(
                    fileId,
                    path,
                    SegmentCorpus.EpisodeDuration(episode)));
            }

            var detection = await detector
                .DetectAsync(seriesId, episodes, progress: null, cancellationToken)
                .ConfigureAwait(false);

            var seriesEmitted = 0;
            var seriesHits = 0;
            var seriesSegmentFree = 0;
            var seriesSpurious = 0;
            foreach (var (fileId, episode) in byFile)
            {
                var truth = SegmentCorpus.GroundTruth(episode);
                var emitted = detection.Segments.Where(segment => segment.FileId == fileId).ToList();
                seriesEmitted += emitted.Count;
                if (truth.Count == 0)
                {
                    segmentFreeEpisodes++;
                    seriesSegmentFree++;
                    if (emitted.Count > 0)
                    {
                        spuriousEpisodes++;
                        seriesSpurious++;
                    }
                }

                foreach (var kind in perKind.Keys.ToArray())
                {
                    var kindTruth = truth.Where(range => range.Kind == kind).ToList();
                    var kindEmitted = emitted.Where(segment => segment.Kind == kind).ToList();
                    var hits = CountHits(kindTruth, kindEmitted);
                    var current = perKind[kind];
                    perKind[kind] = (
                        current.Truth + kindTruth.Count,
                        current.Emitted + kindEmitted.Count,
                        current.Hits + hits);
                    seriesHits += hits;
                }
            }

            seriesRows.Add(new SeriesMetrics(
                series.Id,
                series.Episodes.Count,
                seriesEmitted,
                seriesHits,
                seriesSegmentFree,
                seriesSpurious));
        }

        return new BenchmarkReport(
            detectorName,
            detector.Version,
            split.ToString(),
            ToMetrics(perKind[MarkerKind.Intro]),
            ToMetrics(perKind[MarkerKind.Recap]),
            ToMetrics(perKind[MarkerKind.Credits]),
            segmentFreeEpisodes,
            spuriousEpisodes,
            seriesRows);
    }

    /// <summary>Writes the report where the task keeps its evidence, one file per detector and split.</summary>
    public static void Archive(BenchmarkReport report, string phase)
    {
        ArgumentNullException.ThrowIfNull(report);
        var directory = Path.Combine(MediaToolchain.RepositoryRoot, "artifacts", "test-results", "T43", phase);
        _ = Directory.CreateDirectory(directory);
        var name = FormattableString.Invariant(
            $"benchmark-{report.Detector.ToLowerInvariant()}-{report.Split.ToLowerInvariant()}.json");
        File.WriteAllText(
            Path.Combine(directory, name),
            JsonSerializer.Serialize(
                new
                {
                    report.Detector,
                    report.DetectorVersion,
                    report.Split,
                    report.Intro,
                    IntroPrecision = report.Intro.Precision,
                    IntroRecall = report.Intro.Recall,
                    report.Recap,
                    RecapPrecision = report.Recap.Precision,
                    RecapRecall = report.Recap.Recall,
                    report.Credits,
                    CreditsPrecision = report.Credits.Precision,
                    CreditsRecall = report.Credits.Recall,
                    report.SegmentFreeEpisodes,
                    report.SpuriousEpisodes,
                    report.AggregateSpuriousRate,
                    Series = report.Series.Select(series => new
                    {
                        series.SeriesId,
                        series.Episodes,
                        series.Emitted,
                        series.Hits,
                        series.Precision,
                        series.SegmentFreeEpisodes,
                        series.SpuriousEpisodes,
                        series.SpuriousRate,
                    }),
                    Failures = report.ThresholdFailures(),
                },
                ArchiveOptions));
    }

    /// <summary>Greedy one-to-one matching: each truth range takes the first unused hit.</summary>
    private static int CountHits(
        IReadOnlyList<(MarkerKind Kind, TimeSpan Start, TimeSpan End)> truth,
        List<DetectedSegment> emitted)
    {
        var used = new bool[emitted.Count];
        var hits = 0;
        foreach (var range in truth)
        {
            for (var index = 0; index < emitted.Count; index++)
            {
                if (used[index])
                {
                    continue;
                }

                var candidate = emitted[index];
                if (Within(candidate.Start, range.Start) && Within(candidate.End, range.End))
                {
                    used[index] = true;
                    hits++;
                    break;
                }
            }
        }

        return hits;
    }

    private static bool Within(TimeSpan actual, TimeSpan expected) =>
        (actual - expected).Duration() <= Tolerance;

    private static KindMetrics ToMetrics((int Truth, int Emitted, int Hits) counts) =>
        new(counts.Truth, counts.Emitted, counts.Hits);

    /// <summary>The same corpus name always maps to the same identifier, run after run.</summary>
    private static Guid StableGuid(string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return new Guid(hash.AsSpan(0, 16));
    }
}
