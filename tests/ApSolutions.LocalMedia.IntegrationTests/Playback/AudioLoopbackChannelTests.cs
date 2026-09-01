// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Runtime.Versioning;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using ApSolutions.LocalMedia.Windows.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Playback;

/// <summary>
/// What the endpoint actually receives, recorded rather than read (PLY-004).
///
/// The surround rows of T23 were left unverified on the strength of a label: the registry says an
/// endpoint mixes in two channels, so 5.1 and 7.1 were recorded as a hardware block. That label is
/// written by Windows, and listing it is not verifying it. These tests play the tone-marked sample
/// through a real audio output and capture the engine's own mix back through WASAPI loopback, so both
/// the channel count and the content of every channel are measured.
///
/// Two limits, so that a pass here is not read as more than it is. The capture takes the endpoint's
/// whole mix, so audio from another application during the run would be included; and the assertions
/// scale to what the machine offers, which means the surround case skips rather than fails on a
/// stereo-only machine.
/// </summary>
[Trait("Category", "RealMedia")]
[SupportedOSPlatform("windows")]
public sealed class AudioLoopbackChannelTests
{
    private const string ToneSample = "mkv-audio-71-tones";

    /// <summary>
    /// Far enough above the worst cross-talk to be a verdict rather than a coin toss. The offline
    /// matrix of the sample itself reads 15.6 dB at its worst, so a recorded channel that clears this
    /// is carrying its own tone rather than a neighbour's leakage.
    /// </summary>
    private const double MinimumContrastDecibels = 10.0;

    /// <summary>A channel carrying no tone reads far below this; one carrying a tone reads far above.</summary>
    private const double SilenceFloorDecibels = -70.0;

    private static readonly TimeSpan RecordingSpan = TimeSpan.FromSeconds(2.5);

    [Fact]
    public async Task The_endpoint_receives_as_many_channels_as_its_mix_format_declares()
    {
        var endpoint = RequireLargestEndpoint();
        var capture = await RecordToneSampleAsync(endpoint.Id);

        Assert.Equal(endpoint.Channels, capture.ChannelCount);
        Assert.True(
            capture.DurationSeconds > 0.5,
            $"The loopback captured only {capture.DurationSeconds:F2} s, too little to measure.");

        // No channel may be dead. An endpoint that declares N channels and delivers silence on some of
        // them is exactly the failure a channel count cannot see.
        var levels = capture.Channels
            .Select(samples => ChannelToneAnalysis.Surround71
                .Max(tone => ChannelToneAnalysis.LevelDecibels(samples, tone.Frequency, capture.SampleRate)))
            .ToArray();
        Assert.All(levels, level => Assert.True(
            level > SilenceFloorDecibels,
            $"A channel of the {endpoint.Channels}-channel mix carried no tone at all ({level:F1} dBFS)."));
    }

    [Fact]
    public async Task Every_channel_of_a_surround_endpoint_carries_only_its_own_tone()
    {
        var endpoint = RequireLargestEndpoint();
        Assert.SkipWhen(
            endpoint.Channels < 8,
            $"The largest active render endpoint mixes {endpoint.Channels} channels, so 7.1 cannot be "
                + "recorded here. PLY-004's surround rows stay unverified until an eight-channel one exists.");

        var capture = await RecordToneSampleAsync(endpoint.Id);
        var results = ChannelToneAnalysis.Measure(capture, ChannelToneAnalysis.Surround71);
        await WriteEvidenceAsync(capture, results, "loopback-71-channels.csv");

        Assert.Equal(ChannelToneAnalysis.Surround71.Count, results.Count);
        Assert.All(results, result => Assert.True(
            result.CarriesOnlyItsOwnTone(MinimumContrastDecibels),
            $"Channel {result.ChannelIndex} ({result.Speaker}) answered {result.OwnToneDecibels:F1} dBFS at its "
                + $"own tone but {result.LoudestForeignDecibels:F1} dBFS at {result.LoudestForeignFrequency} Hz, "
                + $"a contrast of only {result.ContrastDecibels:F1} dB. The eight channels are not distinct."));
    }

    /// <summary>
    /// The stereo half of the same question, which every machine can answer. A 7.1 source on a
    /// two-channel endpoint has to arrive as a downmix — both channels sounding, every source channel
    /// folded in — rather than a truncation that drops six of them on the floor.
    /// </summary>
    [Fact]
    public async Task A_surround_source_on_a_stereo_endpoint_is_mixed_down_rather_than_truncated()
    {
        var endpoint = RequireLargestEndpoint();
        Assert.SkipWhen(
            endpoint.Channels != 2,
            $"The largest active render endpoint mixes {endpoint.Channels} channels, so the stereo "
                + "downmix is not what this machine exercises.");

        var capture = await RecordToneSampleAsync(endpoint.Id);
        var levels = ChannelToneAnalysis.Surround71.ToDictionary(
            tone => tone.Speaker,
            tone => capture.Channels
                .Max(samples => ChannelToneAnalysis.LevelDecibels(samples, tone.Frequency, capture.SampleRate)));
        await WriteDownmixEvidenceAsync(capture, levels, "loopback-stereo-downmix.csv");

        Assert.Equal(2, capture.ChannelCount);

        // Every programme channel has to survive the fold. LFE is excluded deliberately and measured
        // separately below: a stereo downmix drops the low-frequency effects channel by convention
        // (ITU-R BS.775 builds its two-channel fold from the programme channels alone), so demanding
        // its tone here would fail a chain that is behaving correctly.
        foreach (var tone in ChannelToneAnalysis.Surround71.Where(tone => tone.Speaker != "LFE"))
        {
            Assert.True(
                levels[tone.Speaker] > SilenceFloorDecibels,
                $"The {tone.Speaker} tone at {tone.Frequency} Hz reached the stereo mix at only "
                    + $"{levels[tone.Speaker]:F1} dBFS, so that source channel was dropped rather than folded in.");
        }

        // The convention above is asserted rather than assumed, so the day a chain starts folding LFE
        // in, this says so instead of staying quietly green on a changed behaviour.
        Assert.True(
            levels["LFE"] < SilenceFloorDecibels,
            $"LFE reached the stereo mix at {levels["LFE"]:F1} dBFS. It is expected to be dropped by the "
                + "downmix; this chain now folds it in, and the evidence needs to say so.");
    }

    /// <summary>
    /// The catalog and WASAPI have to agree about how many channels an endpoint mixes. They are two
    /// independent reads of the same fact — the registry blob and the live audio client — and the
    /// selection policy trusts the first, so a disagreement would make the layouts the interface
    /// offers wrong in a way nothing else here would catch.
    /// </summary>
    [Fact]
    public async Task The_catalog_and_wasapi_agree_on_every_endpoint_channel_count()
    {
        var wasapi = RequireEndpoints();
        var catalog = await new WindowsAudioDeviceCatalog()
            .GetOutputsAsync(TestContext.Current.CancellationToken);

        foreach (var endpoint in wasapi)
        {
            var matching = catalog.SingleOrDefault(device =>
                endpoint.Id.EndsWith(device.Id, StringComparison.OrdinalIgnoreCase));
            if (matching is null)
            {
                continue;
            }

            var offered = matching.SupportedLayouts.Count == 0
                ? 0
                : matching.SupportedLayouts.Max(layout => (int)layout);
            var expected = endpoint.Channels >= 8 ? 8 : endpoint.Channels >= 6 ? 6 : 2;
            Assert.True(
                offered == expected,
                $"WASAPI mixes {endpoint.Channels} channels on '{endpoint.Id}' but the catalog offers "
                    + $"{offered} as its largest layout.");
        }
    }

    private static async Task<string> RequireToneSampleAsync()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var sample = MediaManifest.Require(ToneSample);
        var missing = MediaManifest.MissingEncoders(sample);
        Assert.SkipWhen(
            missing.Count > 0,
            $"The local encoder cannot produce '{sample.Id}': missing {string.Join(", ", missing)}.");
        return await MediaManifest.MaterialiseAsync(sample, TestContext.Current.CancellationToken);
    }

    private static (string Id, int Channels, int SampleRate) RequireLargestEndpoint() =>
        RequireEndpoints().MaxBy(endpoint => endpoint.Channels);

    private static IReadOnlyList<(string Id, int Channels, int SampleRate)> RequireEndpoints()
    {
        var endpoints = WasapiLoopbackRecorder.ActiveRenderEndpoints();
        Assert.SkipWhen(
            endpoints.Count == 0,
            "This machine has no active render endpoint, so nothing can be recorded; a hosted runner "
                + "has no audio device at all.");
        return endpoints;
    }

    /// <summary>
    /// Plays the tone-marked sample through a real audio output while the loopback records. The
    /// headless factory cannot be used here — it silences the output with <c>--aout=dummy</c>, and a
    /// recording of that would be a recording of nothing. Playback starts inside the callback, after
    /// the recorder is already live, so the opening of the media is not lost.
    /// </summary>
    private static async Task<LoopbackCapture> RecordToneSampleAsync(string endpointId)
    {
        var path = await RequireToneSampleAsync();

        await using var factory = LibVlcFactory.CreateDefault();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.SetAudioOutputDeviceAsync(endpointId, TestContext.Current.CancellationToken);

        var capture = await WasapiLoopbackRecorder.RecordAsync(
            endpointId,
            RecordingSpan,
            async () =>
            {
                await engine.PlayAsync(TestContext.Current.CancellationToken);
                await Task.Delay(RecordingSpan, TestContext.Current.CancellationToken);
            },
            TestContext.Current.CancellationToken);

        await engine.StopAsync(TestContext.Current.CancellationToken);
        return capture;
    }

    /// <summary>
    /// One row per source channel, naming the level its tone reached in the recorded mix. The count of
    /// recorded channels cannot answer this question at all: a downmix has two channels either way,
    /// and only the tones say which of the eight source channels survived the fold.
    /// </summary>
    private static async Task WriteDownmixEvidenceAsync(
        LoopbackCapture capture,
        Dictionary<string, double> levels,
        string fileName)
    {
        var rows = new List<string>
        {
            string.Create(
                CultureInfo.InvariantCulture,
                $"# endpointChannels={capture.ChannelCount} sampleRate={capture.SampleRate} seconds={capture.DurationSeconds:F2}"),
            "sourceSpeaker,toneHz,levelInMixDb,survivedFold",
        };
        rows.AddRange(ChannelToneAnalysis.Surround71.Select(tone => string.Create(
            CultureInfo.InvariantCulture,
            $"{tone.Speaker},{tone.Frequency},{levels[tone.Speaker]:F2},{levels[tone.Speaker] > SilenceFloorDecibels}")));
        await WriteRowsAsync(rows, fileName);
    }

    private static async Task WriteEvidenceAsync(
        LoopbackCapture capture,
        IReadOnlyList<ChannelToneResult> results,
        string fileName)
    {
        var rows = new List<string>
        {
            string.Create(
                CultureInfo.InvariantCulture,
                $"# endpointChannels={capture.ChannelCount} sampleRate={capture.SampleRate} seconds={capture.DurationSeconds:F2}"),
        };
        rows.AddRange(ChannelToneAnalysis.ToCsv(capture, results));
        await WriteRowsAsync(rows, fileName);
    }

    private static async Task WriteRowsAsync(IReadOnlyList<string> rows, string fileName)
    {
        var directory = Path.Combine(MediaToolchain.RepositoryRoot, "artifacts", "test-results", "PLY-004");
        Directory.CreateDirectory(directory);
        await File.WriteAllLinesAsync(
            Path.Combine(directory, fileName), rows, TestContext.Current.CancellationToken);
    }
}
