// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
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
    [Fact]
    public async Task An_HDR10_source_is_recognised_from_its_transfer_characteristics()
    {
        var sample = MediaManifest.Require("mkv-hevc-hdr10");
        var path = await CodecMatrixTests.RequireSampleAsync(sample);

        var transfer = await ReadColourTransferAsync(path);

        // Some ffmpeg builds mux the sample without its colour-transfer metadata; a sample that does
        // not carry what the recipe asked for cannot exercise the recognition path.
        Assert.SkipWhen(
            string.IsNullOrEmpty(transfer) || transfer == "unknown",
            "The generated HDR sample carries no colour-transfer metadata on this encoder build.");

        Assert.Equal("smpte2084", transfer);
        var described = LibVlcVideoCapabilities.WithColourTransfer(
            new VideoSourceCapabilities(HdrFormat.None, 320, 240),
            transfer);
        Assert.Equal(HdrFormat.Hdr10, described.Hdr);
    }

    [Fact]
    public async Task An_SDR_source_is_never_promoted_to_HDR()
    {
        var sample = MediaManifest.Require("mkv-hevc-sdr");
        var path = await CodecMatrixTests.RequireSampleAsync(sample);

        var transfer = await ReadColourTransferAsync(path);
        var described = LibVlcVideoCapabilities.WithColourTransfer(
            new VideoSourceCapabilities(HdrFormat.None, 320, 240),
            transfer);

        Assert.Equal(HdrFormat.None, described.Hdr);
    }

    [Theory]
    [InlineData("mkv-hevc-hdr10", HdrFormat.Hdr10)]
    [InlineData("mkv-hevc-sdr", HdrFormat.None)]
    public async Task The_engine_reports_the_path_it_took_for_the_display_it_is_on(string id, HdrFormat hdr)
    {
        var sample = MediaManifest.Require(id);
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        var display = new FixedDisplay(new DisplayCapabilities(SupportsHdr10: true, HdrEnabled: true));
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory, display);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path, sourceHdr: hdr),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        _ = await CodecMatrixTests.WaitForPositionAsync(engine, TimeSpan.FromMilliseconds(150));

        Assert.NotNull(engine.Capabilities);
        Assert.Equal(hdr, engine.Capabilities!.SourceHdr);
        Assert.True(engine.Capabilities.DisplaySupportsHdr);
        Assert.Equal(
            hdr == HdrFormat.Hdr10 ? VideoOutputPath.Hdr10Passthrough : VideoOutputPath.Sdr,
            engine.Capabilities.OutputPath);
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
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path, sourceHdr: HdrFormat.Hdr10),
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
    /// so the request stays the caller's and what is announced as active stays true.
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

        Assert.True(engine.Capabilities!.HardwareAccelerationRequested);
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
            "T22",
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
