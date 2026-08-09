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
