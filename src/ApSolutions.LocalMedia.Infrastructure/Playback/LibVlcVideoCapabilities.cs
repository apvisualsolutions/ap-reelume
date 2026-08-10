// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Playback;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// Reads what a media announces about its picture. HDR is decided from the transfer characteristics
/// the container declares, never from the file name or the container type.
/// </summary>
public static class LibVlcVideoCapabilities
{
    /// <summary>Transfer functions that identify a perceptual-quantiser HDR10 stream.</summary>
    private static readonly string[] Hdr10Transfers = ["smpte2084", "smpte st 2084", "pq", "bt2100"];

    private static readonly string[] DolbyVisionMarkers = ["dolby vision", "dvhe", "dvh1", "dovi"];

    /// <summary>Describes the picture of a parsed media without mutating it.</summary>
    public static VideoSourceCapabilities Describe(VlcMedia media)
    {
        ArgumentNullException.ThrowIfNull(media);
        var video = media.Tracks.FirstOrDefault(track => track.TrackType == TrackType.Video);
        if (video.TrackType != TrackType.Video)
        {
            return new VideoSourceCapabilities(HdrFormat.None, 0, 0);
        }

        var description = media.CodecDescription(TrackType.Video, video.Codec) ?? string.Empty;
        var metadata = string.Join(
            ' ',
            description,
            media.Meta(MetadataType.Description) ?? string.Empty,
            media.Meta(MetadataType.EncodedBy) ?? string.Empty).ToLowerInvariant();

        var hdr = DolbyVisionMarkers.Any(marker => metadata.Contains(marker, StringComparison.Ordinal))
            ? HdrFormat.DolbyVision
            : HdrFormat.None;

        return new VideoSourceCapabilities(
            hdr,
            checked((int)video.Data.Video.Width),
            checked((int)video.Data.Video.Height));
    }

    /// <summary>
    /// Refines the picture description with the colour transfer a probe reported. LibVLC 3 does not
    /// expose transfer characteristics on its track data, so the caller supplies what the media
    /// probe read and the classification stays in one place.
    /// </summary>
    public static VideoSourceCapabilities WithColourTransfer(
        VideoSourceCapabilities source,
        string? colourTransfer)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Hdr == HdrFormat.DolbyVision || string.IsNullOrWhiteSpace(colourTransfer))
        {
            return source;
        }

        var normalised = colourTransfer.Trim().ToLowerInvariant();
        return Hdr10Transfers.Any(transfer => normalised.Contains(transfer, StringComparison.Ordinal))
            ? source with { Hdr = HdrFormat.Hdr10 }
            : source;
    }

    /// <summary>The LibVLC options that request hardware decoding, and the software fallback set.</summary>
    public static IReadOnlyList<string> AccelerationOptions(bool useHardware) => useHardware
        ? [":avcodec-hw=any"]
        : [":avcodec-hw=none"];

    internal static string Describe(VideoOutputDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"path={decision.Path} hdr={decision.SourceHdr} displayHdr={decision.DisplaySupportsHdr} " +
            $"hwRequested={decision.HardwareAccelerationRequested} hwActive={decision.HardwareAccelerationActive} " +
            $"fellBack={decision.FellBackToSoftware}");
    }
}
