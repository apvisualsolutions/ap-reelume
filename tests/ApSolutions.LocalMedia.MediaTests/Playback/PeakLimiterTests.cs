// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// The limiter is what makes boosted volume safe. These tests drive it with a synthetic sweep and
/// with real decoded audio and record the measured peaks as evidence.
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class PeakLimiterTests
{
    private const int SampleRate = 48_000;

    [Theory]
    [InlineData(101)]
    [InlineData(150)]
    [InlineData(200)]
    public void A_full_scale_sweep_never_exceeds_the_normalised_peak_at_any_boost(int percent)
    {
        var decision = VolumeBoostPolicy.Decide(percent, muted: false);
        var limiter = new PeakLimiterAudioFilter(SampleRate);
        var sweep = BuildSweep(seconds: 2, startHz: 20, endHz: 18_000, amplitude: 1.0);

        limiter.Process(sweep.AsSpan(), decision.LinearGain);

        Assert.True(decision.LimiterEngaged);
        Assert.True(limiter.HasEngaged, $"The limiter never engaged at {percent}%.");
        Assert.All(sweep, sample => Assert.InRange(
            Math.Abs(sample),
            0,
            (float)VolumeBoostPolicy.LimiterThreshold + 1e-5f));
        Assert.InRange(limiter.ObservedPeak, 0, VolumeBoostPolicy.LimiterThreshold + 1e-9);
    }

    [Fact]
    public void Below_the_threshold_the_limiter_leaves_the_signal_alone()
    {
        var limiter = new PeakLimiterAudioFilter(SampleRate);
        var quiet = BuildSweep(seconds: 1, startHz: 100, endHz: 4000, amplitude: 0.4);
        var original = (float[])quiet.Clone();

        limiter.Process(quiet.AsSpan(), VolumeBoostPolicy.Decide(100, muted: false).LinearGain);

        Assert.False(limiter.HasEngaged);
        for (var index = 0; index < quiet.Length; index++)
        {
            Assert.Equal(original[index], quiet[index], 4);
        }
    }

    [Fact]
    public void A_step_transient_is_held_at_the_ceiling_from_its_very_first_sample()
    {
        var limiter = new PeakLimiterAudioFilter(SampleRate);
        var step = new float[SampleRate / 10];
        Array.Fill(step, 1.0f);

        limiter.Process(step.AsSpan(), VolumeBoostPolicy.Decide(200, muted: false).LinearGain);

        Assert.All(step, sample => Assert.InRange(
            sample,
            0,
            (float)VolumeBoostPolicy.LimiterThreshold + 1e-5f));
    }

    [Fact]
    public void Muting_produces_silence_and_restoring_the_level_brings_the_signal_back()
    {
        var limiter = new PeakLimiterAudioFilter(SampleRate);
        var muted = BuildSweep(seconds: 1, startHz: 200, endHz: 2000, amplitude: 1.0);
        limiter.Process(muted.AsSpan(), VolumeBoostPolicy.Decide(180, muted: true).LinearGain);
        Assert.All(muted, sample => Assert.Equal(0f, sample));

        limiter.Reset();
        var restored = BuildSweep(seconds: 1, startHz: 200, endHz: 2000, amplitude: 1.0);
        limiter.Process(restored.AsSpan(), VolumeBoostPolicy.Decide(180, muted: false).LinearGain);
        Assert.Contains(restored, sample => Math.Abs(sample) > 0.5f);
    }

    [Fact]
    public async Task Real_decoded_audio_stays_under_the_ceiling_at_two_hundred_percent()
    {
        var sample = MediaManifest.Require("mp4-h264-aac");
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        var pcm = await DecodeToPcmAsync(path);
        Assert.SkipWhen(pcm.Length == 0, "The encoder produced no decodable PCM for the limiter test.");

        var rawPeak = pcm.Max(value => Math.Abs(value / (double)short.MaxValue));
        var decision = VolumeBoostPolicy.Decide(200, muted: false);

        var asDecoded = (short[])pcm.Clone();
        var quietLimiter = new PeakLimiterAudioFilter(SampleRate);
        quietLimiter.Process(asDecoded.AsSpan(), decision.LinearGain);
        var quietPeak = asDecoded.Max(value => Math.Abs(value / (double)short.MaxValue));

        // The generated tone is quiet, so it is also normalised to full scale to exercise the case
        // the limiter exists for: real decoded material that would clip under boost.
        var normalised = Normalise(pcm);
        var loudLimiter = new PeakLimiterAudioFilter(SampleRate);
        loudLimiter.Process(normalised.AsSpan(), decision.LinearGain);
        var loudPeak = normalised.Max(value => Math.Abs(value / (double)short.MaxValue));

        Assert.InRange(quietPeak, 0, VolumeBoostPolicy.LimiterThreshold + 1e-3);
        Assert.InRange(loudPeak, 0, VolumeBoostPolicy.LimiterThreshold + 1e-3);
        Assert.True(loudLimiter.HasEngaged, "The limiter never engaged on full-scale decoded audio.");
        Assert.False(quietLimiter.HasEngaged);

        var report = Path.Combine(
            MediaToolchain.RepositoryRoot,
            "artifacts",
            "test-results",
            "T21",
            "green",
            "limiter-peaks.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        await File.WriteAllLinesAsync(
            report,
            [
                "source,gain,rawPeak,limitedPeak,limiterEngaged,threshold",
                FormattableString.Invariant(
                    $"mp4-h264-aac as decoded,{decision.LinearGain:F2},{rawPeak:F4},{quietPeak:F4},{quietLimiter.HasEngaged},{VolumeBoostPolicy.LimiterThreshold:F4}"),
                FormattableString.Invariant(
                    $"mp4-h264-aac normalised,{decision.LinearGain:F2},1.0000,{loudPeak:F4},{loudLimiter.HasEngaged},{VolumeBoostPolicy.LimiterThreshold:F4}"),
            ],
            TestContext.Current.CancellationToken);
    }

    private static short[] Normalise(short[] samples)
    {
        var peak = samples.Max(value => Math.Abs((int)value));
        if (peak == 0)
        {
            return (short[])samples.Clone();
        }

        var factor = short.MaxValue / (double)peak;
        return [.. samples.Select(value => (short)Math.Clamp(
            Math.Round(value * factor),
            short.MinValue,
            short.MaxValue))];
    }

    /// <summary>Decodes the sample to interleaved 16-bit PCM with the local encoder.</summary>
    private static async Task<short[]> DecodeToPcmAsync(string path)
    {
        var destination = Path.Combine(MediaToolchain.OutputRoot, "T21", "decoded.raw");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (!File.Exists(destination))
        {
            _ = await MediaToolchain.EnsureSampleAsync(
                "T21/decoded.raw",
                FormattableString.Invariant(
                    $"-i \"{path}\" -vn -ac 1 -ar {SampleRate} -f s16le -acodec pcm_s16le"),
                TestContext.Current.CancellationToken);
        }

        var bytes = await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken);
        var samples = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
        return samples;
    }

    private static float[] BuildSweep(double seconds, double startHz, double endHz, double amplitude)
    {
        var count = (int)(seconds * SampleRate);
        var samples = new float[count];
        var phase = 0.0;
        for (var index = 0; index < count; index++)
        {
            var progress = index / (double)count;
            var frequency = startHz + ((endHz - startHz) * progress);
            phase += 2 * Math.PI * frequency / SampleRate;
            samples[index] = (float)(Math.Sin(phase) * amplitude);
        }

        return samples;
    }

    private static string Describe(double value) => value.ToString("F4", CultureInfo.InvariantCulture);
}
