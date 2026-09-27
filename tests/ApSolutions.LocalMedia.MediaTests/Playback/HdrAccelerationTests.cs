// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;
using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// HDR and acceleration are reported from what actually happened on this machine. A capability the
/// hardware does not have is recorded as a hardware block, never as a simulated pass.
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class HdrAccelerationTests
{
    // Measured on 2026-09-27 on the two samples: 33.0 levels with the conversion and 16.1 without it
    // (what is left is the BT.2020 matrix alone), the same in two runs each. Halfway between.
    private const double MeanGreenDifferenceFloor = 24d;

    /// <summary>
    /// The container's own declaration, read the way the player reads it, agrees with ffprobe — which
    /// is a second, independent reader, and the one this suite trusted before the player had its own.
    /// </summary>
    [Theory]
    [InlineData("mkv-hevc-hdr10", "smpte2084", 16, 9, 9)]
    [InlineData("mkv-hevc-sdr", "bt709", 1, 1, 1)]
    public async Task The_player_reads_the_same_colour_ffprobe_reads(
        string id,
        string ffprobeTransfer,
        int transfer,
        int matrix,
        int primaries)
    {
        var path = await CodecMatrixTests.RequireSampleAsync(MediaManifest.Require(id));

        // No skip on a missing curve: it skipped on the server for months because ffmpeg 9.0 left it out
        // of the container, and the recipe now tags the frames. A build that loses it again is red.
        Assert.Equal(ffprobeTransfer, await ReadColourTransferAsync(path));
        var declared = ContainerColourReader.Read(path);
        Assert.Equal(transfer, declared.Transfer);
        Assert.Equal(matrix, declared.Matrix);
        Assert.Equal(primaries, declared.Primaries);
    }

    /// <summary>
    /// Dolby Vision is recognised from LibVLC's own description and stays what it is when its base
    /// layer also declares PQ; anything else takes what the file declares.
    /// </summary>
    [Fact]
    public void The_declared_colour_never_downgrades_Dolby_Vision()
    {
        var pq = new SourceColour(9, 16, 9, null);

        Assert.Equal(
            HdrFormat.DolbyVision,
            LibVlcVideoCapabilities.WithDeclaredColour(new VideoSourceCapabilities(HdrFormat.DolbyVision, 1, 1), pq).Hdr);
        Assert.Equal(
            HdrFormat.Hdr10,
            LibVlcVideoCapabilities.WithDeclaredColour(new VideoSourceCapabilities(HdrFormat.None, 1, 1), pq).Hdr);
        Assert.Equal(
            HdrFormat.None,
            LibVlcVideoCapabilities.WithDeclaredColour(new VideoSourceCapabilities(HdrFormat.None, 1, 1), SourceColour.Undeclared).Hdr);
    }

    [Theory]
    [InlineData("Dolby Vision", null, null, HdrFormat.DolbyVision)]
    [InlineData("HEVC", null, "x265 dvhe.08.06", HdrFormat.DolbyVision)]
    [InlineData(null, "DOVI profile 8", null, HdrFormat.DolbyVision)]
    [InlineData("H.265/HEVC", null, "Lavf61", HdrFormat.None)]
    [InlineData(null, null, null, HdrFormat.None)]
    public void Dolby_Vision_is_recognised_from_what_LibVLC_says_about_the_stream(
        string? codec,
        string? description,
        string? encodedBy,
        HdrFormat expected) =>
        Assert.Equal(expected, LibVlcVideoCapabilities.RecogniseDolbyVision(codec, description, encodedBy));

    /// <summary>
    /// The frames themselves go through the conversion. The two samples carry the same pixels — the
    /// HDR10 one only tags them as PQ — so the first frame of each, read the way each declares, has to
    /// differ pixel by pixel: the curve lifts the mid-tones and sinks the shadows. The average is no
    /// measure of it, and that was measured: the two movements cancel, 125.5 against 124.0.
    /// </summary>
    [Fact]
    public async Task The_frames_of_an_HDR10_film_come_out_converted()
    {
        var hdr = await FirstFrameAsync("mkv-hevc-hdr10");
        var sdr = await FirstFrameAsync("mkv-hevc-sdr");
        Assert.Equal(sdr.Length, hdr.Length);

        var difference = 0L;
        for (var at = 1; at < hdr.Length; at += 4)
        {
            difference += Math.Abs(hdr[at] - sdr[at]);
        }

        var mean = difference / (hdr.Length / 4d);
        Assert.True(mean > MeanGreenDifferenceFloor, $"The HDR10 frame differs from the same pixels in SDR by {mean:F1} levels of green on average.");
    }

    private static async Task<byte[]> FirstFrameAsync(string id)
    {
        var path = await CodecMatrixTests.RequireSampleAsync(MediaManifest.Require(id));
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        var first = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.FrameRendered += (_, frame) => first.TrySetResult(frame.Pixels.ToArray());

        await engine.OpenAsync(new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path), TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        var pixels = await first.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await engine.StopAsync(TestContext.Current.CancellationToken);
        return pixels;
    }

    /// <summary>A file with no picture has nothing to describe, and is played as standard range.</summary>
    [Fact]
    public async Task A_file_with_no_picture_is_described_as_standard_range()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var path = await MediaToolchain.EnsureSampleAsync(
            "hdr/audio-only.mka",
            "-f lavfi -i sine=frequency=440:duration=1 -c:a flac",
            TestContext.Current.CancellationToken);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        await engine.OpenAsync(new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path), TestContext.Current.CancellationToken);

        Assert.Equal(HdrFormat.None, engine.Capabilities!.SourceHdr);
        Assert.Equal(VideoOutputPath.Sdr, engine.Capabilities.OutputPath);
        Assert.False(engine.CarriesToneMapping);
        await engine.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The same for MP4, whose declaration lives in a <c>colr</c> box and not an element.</summary>
    [Fact]
    public async Task An_HDR10_MP4_is_read_from_its_colour_box()
    {
        Assert.SkipUnless(MediaToolchain.HasEncoder("libx265"), "libx265 is not available to write the MP4 sample.");
        var path = await MediaToolchain.EnsureSampleAsync(
            "hdr/mp4-hevc-hdr10.mp4",
            "-f lavfi -i testsrc2=size=320x240:rate=15:duration=1 -an "
            + "-vf setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc "
            + "-c:v libx265 -preset ultrafast -x265-params log-level=error -pix_fmt yuv420p10le -tag:v hvc1 "
            + "-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc -movflags +write_colr",
            TestContext.Current.CancellationToken);

        Assert.Equal("smpte2084", await ReadColourTransferAsync(path));
        Assert.Equal(new SourceColour(9, 16, 9, null), ContainerColourReader.Read(path) with { MaxContentLight = null });
    }

    /// <summary>
    /// Nothing tells the engine the film is HDR10 but the film: it reads the curve, brings the picture
    /// down, and says so. And a display with HDR switched on changes nothing, because this output
    /// draws eight-bit standard range and cannot carry HDR to it.
    /// </summary>
    [Theory]
    [InlineData("mkv-hevc-hdr10", HdrFormat.Hdr10)]
    [InlineData("mkv-hevc-sdr", HdrFormat.None)]
    public async Task The_engine_recognises_HDR10_from_the_file_and_brings_it_down(string id, HdrFormat hdr)
    {
        var sample = MediaManifest.Require(id);
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        var display = new FixedDisplay(new DisplayCapabilities(SupportsHdr10: true, HdrEnabled: true));
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory, display);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        _ = await CodecMatrixTests.WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(150));

        Assert.NotNull(engine.Capabilities);
        Assert.Equal(hdr, engine.Capabilities!.SourceHdr);
        Assert.True(engine.Capabilities.DisplaySupportsHdr);
        Assert.Equal(
            hdr == HdrFormat.Hdr10 ? VideoOutputPath.SdrToneMapped : VideoOutputPath.Sdr,
            engine.Capabilities.OutputPath);
        Assert.Equal(hdr == HdrFormat.Hdr10, engine.CarriesToneMapping);
        Assert.True(engine.DecodedFrameCount > 0, $"'{id}' decoded no frame.");
        await engine.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task An_HDR10_source_on_an_SDR_display_is_tone_mapped_and_still_decodes()
    {
        var sample = MediaManifest.Require("mkv-hevc-hdr10");
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        var display = new FixedDisplay(new DisplayCapabilities(SupportsHdr10: false, HdrEnabled: false));
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory, display);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        _ = await CodecMatrixTests.WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(150));

        Assert.Equal(VideoOutputPath.SdrToneMapped, engine.Capabilities!.OutputPath);
        Assert.False(engine.Capabilities.DisplaySupportsHdr);
        Assert.True(engine.DecodedFrameCount > 0);
        await engine.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The embedded engine decodes in software, and says so rather than claiming otherwise.
    /// </summary>
    /// <remarks>
    /// This used to assert that a fresh engine decoded in hardware and stepped down only when asked
    /// to. It does not any more, and the reason is subtitles: VLC draws them into the picture before
    /// it reaches this engine's buffer, and with D3D11VA the picture at that moment is a graphics
    /// card surface it has no routine to draw onto — measured on 2026-08-25, 67 001 bytes of picture
    /// changing in software and zero in hardware. The engine records the step down when it is built,
    /// and reports neither requested nor active — a surface it cannot compose onto is not something
    /// it asks for, so the «hardware acceleration was not available» warning belongs to a machine
    /// that could not give it and not to a decision this engine took.
    /// </remarks>
    [Fact]
    public async Task The_embedded_engine_decodes_in_software_and_reports_it_from_the_first_open()
    {
        var sample = MediaManifest.Require("mkv-hevc-sdr");
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        // Already stepped down, so asking again changes nothing: the step is taken once, when the
        // engine is built, and it is the same one a failing decoder would have taken.
        Assert.True(engine.HasFallenBackToSoftware);
        Assert.False(engine.TryFallBackToSoftware());

        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        var advanced = await CodecMatrixTests.WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(150));

        // Neither requested nor active, and the pair is the point: the engine does not ask for a
        // graphics-card surface, because it composes subtitles into the picture it hands out and a
        // surface cannot be asked to do that. Reporting the caller's wish would light the «hardware
        // acceleration was not available» warning over every session forever — a complaint about
        // the machine where the truth is a decision.
        Assert.False(engine.Capabilities!.HardwareAccelerationRequested);
        Assert.False(engine.Capabilities.HardwareAccelerationActive);
        Assert.True(advanced, "Playback stopped while decoding in software.");
        Assert.True(engine.DecodedFrameCount > 0);
        await engine.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_hardware_actually_present_is_recorded_for_the_evidence_matrix()
    {
        var gpus = await QueryAsync("Win32_VideoController", "Name");
        var report = Path.Combine(
            MediaToolchain.RepositoryRoot,
            "artifacts",
            "test-results",
            "hdr-acceleration",
            "green",
            "hardware-observed.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        await File.WriteAllLinesAsync(
            report,
            [
                "kind,value",
                .. gpus.Select(name => FormattableString.Invariant($"gpu,{name}")),
            ],
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(gpus);
    }

    private static async Task<string> ReadColourTransferAsync(string path)
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var probe = Path.Combine(Path.GetDirectoryName(MediaToolchain.EncoderPath!)!, "ffprobe.exe");
        Assert.SkipWhen(!File.Exists(probe), "ffprobe was not found beside ffmpeg; the HDR probe is unavailable.");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(
                probe,
                $"-v error -select_streams v:0 -show_entries stream=color_transfer -of csv=p=0 \"{path}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        _ = process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return output.Trim();
    }

    private static async Task<IReadOnlyList<string>> QueryAsync(string className, string property)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(
                "powershell",
                $"-NoProfile -Command \"Get-CimInstance {className} | Select-Object -ExpandProperty {property}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        _ = process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private sealed class FixedDisplay(DisplayCapabilities capabilities) : IDisplayCapabilityProvider
    {
        public DisplayCapabilities GetCurrentDisplay() => capabilities;
    }
}
