// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Corpus;

/// <summary>
/// Proves the benchmark can fail. A detector that finds nothing and a detector that guesses fixed
/// positions must both miss the approved thresholds; if either passed, the benchmark would measure
/// nothing and every later green would be worthless. Neither needs a single media file.
/// </summary>
public sealed class SegmentBenchmarkSensitivityTests
{
    [Fact]
    public async Task A_detector_that_finds_nothing_fails_the_approved_thresholds()
    {
        var report = await SegmentBenchmark.EvaluateAsync(
            new NullSegmentDetector(),
            "null",
            CorpusSplit.Development,
            materialise: false,
            TestContext.Current.CancellationToken);

        SegmentBenchmark.Archive(report, "red");
        var failures = report.ThresholdFailures();
        Assert.NotEmpty(failures);
        Assert.Contains(failures, failure => failure.Contains("intro recall", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("credits recall", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("recap recall", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_detector_that_guesses_fixed_positions_fails_the_approved_thresholds()
    {
        var report = await SegmentBenchmark.EvaluateAsync(
            new FixedPositionDetector(),
            "fixed-position",
            CorpusSplit.Development,
            materialise: false,
            TestContext.Current.CancellationToken);

        SegmentBenchmark.Archive(report, "red");
        var failures = report.ThresholdFailures();
        Assert.NotEmpty(failures);

        // Guessing marks the segment-free series too, which is exactly what the spurious bound is for.
        Assert.True(
            report.AggregateSpuriousRate > 0.05,
            "The fixed-position baseline should mark segment-free episodes as having segments.");
    }

    /// <summary>Finds nothing, ever. The floor every real detector has to beat.</summary>
    private sealed class NullSegmentDetector : IAutomaticSegmentDetector
    {
        public int Version => 0;

        public Task<SeriesSegmentDetection> DetectAsync(
            SeriesId seriesId,
            IReadOnlyList<SegmentDetectionEpisode> episodes,
            IProgress<SegmentDetectionProgress>? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SeriesSegmentDetection(seriesId, Version, []));
    }

    /// <summary>
    /// Claims every episode opens with a thirty-second intro and closes with thirty seconds of
    /// credits. Plausible-sounding and wrong: the corpus varies exactly what this guess fixes.
    /// </summary>
    private sealed class FixedPositionDetector : IAutomaticSegmentDetector
    {
        public int Version => 0;

        public Task<SeriesSegmentDetection> DetectAsync(
            SeriesId seriesId,
            IReadOnlyList<SegmentDetectionEpisode> episodes,
            IProgress<SegmentDetectionProgress>? progress,
            CancellationToken cancellationToken)
        {
            var segments = new List<DetectedSegment>();
            foreach (var episode in episodes)
            {
                var duration = episode.Duration ?? TimeSpan.FromMinutes(3);
                segments.Add(new DetectedSegment(
                    episode.FileId,
                    MarkerKind.Intro,
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(30),
                    Confidence: 1.0));
                segments.Add(new DetectedSegment(
                    episode.FileId,
                    MarkerKind.Credits,
                    duration - TimeSpan.FromSeconds(30),
                    duration,
                    Confidence: 1.0));
            }

            return Task.FromResult(new SeriesSegmentDetection(seriesId, Version, segments));
        }
    }
}
