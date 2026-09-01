// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace ApSolutions.LocalMedia.IntegrationTests.Playback;

/// <summary>One speaker position of the tone-marked sample, and the tone only that position carries.</summary>
internal sealed record ToneMarkedChannel(int Index, string Speaker, int Frequency);

/// <summary>
/// How loudly one recorded channel answers at one frequency, and whether that answer is the loudest
/// one that channel gives. Kept as levels rather than a bare verdict so a failure says how far off it
/// was instead of only that it failed.
/// </summary>
internal sealed record ChannelToneResult(
    int ChannelIndex,
    string Speaker,
    double OwnToneDecibels,
    double LoudestForeignDecibels,
    int LoudestForeignFrequency)
{
    /// <summary>How far the channel's own tone stands above the loudest tone that belongs elsewhere.</summary>
    public double ContrastDecibels => OwnToneDecibels - LoudestForeignDecibels;

    public bool CarriesOnlyItsOwnTone(double minimumContrastDecibels) =>
        ContrastDecibels >= minimumContrastDecibels;
}

/// <summary>
/// Reads a loopback recording against the tone-marked sample, one Goertzel evaluation per
/// channel/frequency pair.
///
/// The point of the tone marking is that a channel count alone proves very little: eight channels of
/// the same signal, or seven channels of silence beside one that sounds, both count as eight. Naming
/// the tone each channel carries separates a real eight-channel path from an upmix, and catches a
/// permuted channel order, which a count cannot see at all.
/// </summary>
internal static class ChannelToneAnalysis
{
    /// <summary>
    /// The tone marking of <c>mkv-audio-71-tones</c>: prime, mutually non-harmonic frequencies, so no
    /// tone can be mistaken for another's harmonic if the chain adds distortion.
    /// </summary>
    public static IReadOnlyList<ToneMarkedChannel> Surround71 { get; } =
    [
        new(0, "FL", 277),
        new(1, "FR", 421),
        new(2, "FC", 647),
        new(3, "LFE", 983),
        new(4, "BL", 1493),
        new(5, "BR", 2269),
        new(6, "SL", 3449),
        new(7, "SR", 5237),
    ];

    /// <summary>
    /// Every channel of the recording measured against every tone of the marking. A channel that is
    /// missing from the recording is not silently skipped: the caller compares the counts.
    /// </summary>
    public static IReadOnlyList<ChannelToneResult> Measure(
        LoopbackCapture capture,
        IReadOnlyList<ToneMarkedChannel> marking)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(marking);
        var results = new List<ChannelToneResult>();

        for (var index = 0; index < capture.Channels.Count && index < marking.Count; index++)
        {
            var samples = capture.Channels[index];
            var own = marking[index];
            var ownLevel = LevelDecibels(samples, own.Frequency, capture.SampleRate);
            var loudestForeign = double.NegativeInfinity;
            var loudestForeignFrequency = 0;

            foreach (var other in marking)
            {
                if (other.Frequency == own.Frequency)
                {
                    continue;
                }

                var level = LevelDecibels(samples, other.Frequency, capture.SampleRate);
                if (level > loudestForeign)
                {
                    loudestForeign = level;
                    loudestForeignFrequency = other.Frequency;
                }
            }

            results.Add(new ChannelToneResult(index, own.Speaker, ownLevel, loudestForeign, loudestForeignFrequency));
        }

        return results;
    }

    /// <summary>
    /// The amplitude at one frequency, in dBFS, by the Goertzel algorithm. A Hann window is applied
    /// first: without it, a tone that does not land exactly on a bin smears into its neighbours, and
    /// the smearing is the same size as the contrast being measured.
    /// </summary>
    public static double LevelDecibels(float[] samples, double frequency, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Length == 0)
        {
            return double.NegativeInfinity;
        }

        var omega = 2.0 * Math.PI * frequency / sampleRate;
        var coefficient = 2.0 * Math.Cos(omega);
        double previous = 0;
        double older = 0;
        double windowSum = 0;

        for (var index = 0; index < samples.Length; index++)
        {
            var window = 0.5 - (0.5 * Math.Cos(2.0 * Math.PI * index / (samples.Length - 1.0)));
            windowSum += window;
            var current = (samples[index] * window) + (coefficient * previous) - older;
            older = previous;
            previous = current;
        }

        var power = (previous * previous) + (older * older) - (coefficient * previous * older);
        if (power <= 0 || windowSum <= 0)
        {
            return double.NegativeInfinity;
        }

        var amplitude = 2.0 * Math.Sqrt(power) / windowSum;
        return amplitude <= 0 ? double.NegativeInfinity : 20.0 * Math.Log10(amplitude);
    }

    /// <summary>The rows an evidence run records, so the numbers outlive the assertion that read them.</summary>
    public static IReadOnlyList<string> ToCsv(LoopbackCapture capture, IReadOnlyList<ChannelToneResult> results)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(results);
        var rows = new List<string>
        {
            "channelIndex,speaker,ownToneDb,loudestForeignDb,loudestForeignHz,contrastDb",
        };

        rows.AddRange(results.Select(result => string.Create(
            CultureInfo.InvariantCulture,
            $"{result.ChannelIndex},{result.Speaker},{result.OwnToneDecibels:F2},{result.LoudestForeignDecibels:F2},{result.LoudestForeignFrequency},{result.ContrastDecibels:F2}")));
        return rows;
    }
}
