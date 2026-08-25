// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>High dynamic range format announced by the source.</summary>
public enum HdrFormat
{
    None,
    Hdr10,

    /// <summary>Recognised so it can be refused explicitly; the MVP never plays it.</summary>
    DolbyVision,
}

/// <summary>How the picture reaches the display.</summary>
public enum VideoOutputPath
{
    Sdr,
    Hdr10Passthrough,
    SdrToneMapped,
}

/// <summary>What the source announces about its picture.</summary>
public sealed record VideoSourceCapabilities(HdrFormat Hdr, int Width, int Height);

/// <summary>What the display can do and what it is doing right now.</summary>
public sealed record DisplayCapabilities(bool SupportsHdr10, bool HdrEnabled);

/// <summary>The output the engine will use, and why.</summary>
public sealed record VideoOutputDecision(
    VideoOutputPath Path,
    HdrFormat SourceHdr,
    bool DisplaySupportsHdr,
    bool HardwareAccelerationRequested,
    bool HardwareAccelerationActive,
    bool FellBackToSoftware,
    PlaybackFailureCode? UnsupportedReason)
{
    /// <summary>The observable capabilities the interface shows; never a promise, always a report.</summary>
    public PlaybackCapabilities ToCapabilities() => new(
        HardwareAccelerationRequested,
        HardwareAccelerationActive,
        SourceHdr,
        DisplaySupportsHdr,
        Path);
}

/// <summary>
/// Chooses the output path from the real state of the source and the display. Hardware acceleration
/// is a request that may fail; failing changes only the decoder, never the output path, and never
/// stops playback. Dolby Vision is answered with an explicit unsupported capability.
/// </summary>
public static class VideoOutputPolicy
{
    /// <summary>The paths the application can actually produce.</summary>
    public static IReadOnlyList<VideoOutputPath> SelectablePaths { get; } =
        [VideoOutputPath.Sdr, VideoOutputPath.Hdr10Passthrough, VideoOutputPath.SdrToneMapped];

    public static VideoOutputDecision Decide(
        VideoSourceCapabilities source,
        DisplayCapabilities display,
        bool hardwareRequested,
        bool hardwareAvailable)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(display);

        var active = hardwareRequested && hardwareAvailable;
        var fellBack = hardwareRequested && !hardwareAvailable;
        var displayHdr = display is { SupportsHdr10: true, HdrEnabled: true };

        var (path, unsupported) = source.Hdr switch
        {
            // Dolby Vision is tone mapped as ordinary HDR metadata would be, but the reason is
            // reported so the interface can say the format itself is out of scope.
            HdrFormat.DolbyVision => (VideoOutputPath.SdrToneMapped, (PlaybackFailureCode?)PlaybackFailureCode.UnsupportedCapability),
            HdrFormat.Hdr10 when displayHdr => (VideoOutputPath.Hdr10Passthrough, null),
            HdrFormat.Hdr10 => (VideoOutputPath.SdrToneMapped, null),
            _ => (VideoOutputPath.Sdr, null),
        };

        return new VideoOutputDecision(
            path,
            source.Hdr,
            display.SupportsHdr10,
            hardwareRequested,
            active,
            fellBack,
            unsupported);
    }
}

/// <summary>
/// Where the picture is drawn inside the space it was given, in device-independent units.
/// </summary>
public readonly record struct VideoFrameBox(double X, double Y, double Width, double Height)
{
    /// <summary>True when the box leaves bars, which is what proves the shape was preserved.</summary>
    public bool IsLetterboxed => Y > 0;

    /// <summary>True when the box leaves bars at the sides instead of above and below.</summary>
    public bool IsPillarboxed => X > 0;
}

/// <summary>
/// Places the decoded picture inside the surface that shows it, keeping the shape it was decoded
/// with. Stretching to the surface is what the player did until 2026-08-25: a 16:9 episode drawn
/// into a window somebody had made taller came out taller too, and so did the picture-in-picture.
/// </summary>
/// <remarks>
/// The rule is the one every player uses: scale by whichever axis runs out first and centre what is
/// left, so the bars are shared rather than pushed to one side. Nothing here rounds — the caller
/// draws in device-independent units and the renderer owns the pixel grid.
/// </remarks>
public static class VideoFitPolicy
{
    /// <summary>
    /// The box a picture of <paramref name="frameWidth"/> by <paramref name="frameHeight"/> occupies
    /// inside a surface of <paramref name="surfaceWidth"/> by <paramref name="surfaceHeight"/>.
    /// A degenerate input on either side yields an empty box rather than a division by zero.
    /// </summary>
    public static VideoFrameBox Fit(
        double frameWidth,
        double frameHeight,
        double surfaceWidth,
        double surfaceHeight)
    {
        if (frameWidth <= 0 || frameHeight <= 0 || surfaceWidth <= 0 || surfaceHeight <= 0)
        {
            return default;
        }

        var scale = Math.Min(surfaceWidth / frameWidth, surfaceHeight / frameHeight);
        var width = frameWidth * scale;
        var height = frameHeight * scale;
        return new VideoFrameBox(
            (surfaceWidth - width) / 2,
            (surfaceHeight - height) / 2,
            width,
            height);
    }
}
