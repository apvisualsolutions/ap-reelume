// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Corpus;

/// <summary>
/// The real detector against the frozen corpus. The development split is what the detector may be
/// tuned against; the held-out split is only ever measured, and it is the one the thresholds judge.
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class SegmentDetectionCorpusTests
{
    [Fact]
    public async Task The_detector_meets_the_approved_thresholds_on_the_development_series()
    {
        var report = await EvaluateAsync(CorpusSplit.Development);

        SegmentBenchmark.Archive(report, "green");
        Assert.Empty(report.ThresholdFailures());
    }

    [Fact]
    public async Task The_detector_meets_the_approved_thresholds_on_the_held_out_series()
    {
        var report = await EvaluateAsync(CorpusSplit.HeldOut);

        SegmentBenchmark.Archive(report, "green");
        Assert.Empty(report.ThresholdFailures());
    }

    private static async Task<BenchmarkReport> EvaluateAsync(CorpusSplit split)
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var missing = SegmentCorpus.MissingEncoders();
        Assert.SkipWhen(missing.Count > 0, $"The local ffmpeg build lacks: {string.Join(", ", missing)}.");

        await using var factory = LibVlcFactory.CreateHeadless();
        var detector = new AutomaticSegmentDetector(new LocalSegmentFeatureExtractor(factory));
        return await SegmentBenchmark.EvaluateAsync(
            detector,
            "local",
            split,
            materialise: true,
            TestContext.Current.CancellationToken);
    }
}
