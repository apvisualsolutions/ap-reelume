// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

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

        return new VideoSourceCapabilities(
            RecogniseDolbyVision(
                media.CodecDescription(TrackType.Video, video.Codec),
                media.Meta(MetadataType.Description),
                media.Meta(MetadataType.EncodedBy)),
            checked((int)video.Data.Video.Width),
            checked((int)video.Data.Video.Height));
    }

    /// <summary>
    /// Dolby Vision from what LibVLC says about the stream: its codec description and the file's own
    /// description and encoder, any of which may be missing. Apart from the media so it can be
    /// measured with the words themselves — no encoder this suite has writes a Dolby Vision file.
    /// </summary>
    public static HdrFormat RecogniseDolbyVision(params string?[] descriptions)
    {
        ArgumentNullException.ThrowIfNull(descriptions);
        var text = string.Join(' ', descriptions.Select(part => part ?? string.Empty)).ToLowerInvariant();
        return DolbyVisionMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal))
            ? HdrFormat.DolbyVision
            : HdrFormat.None;
    }

    /// <summary>
    /// The picture description with what the file declares about its colour. Dolby Vision, which is
    /// recognised from LibVLC's own description, is never downgraded to the HDR10 it may also carry.
    /// </summary>
    public static VideoSourceCapabilities WithDeclaredColour(VideoSourceCapabilities source, SourceColour colour)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(colour);
        return source.Hdr == HdrFormat.DolbyVision ? source : source with { Hdr = colour.Hdr };
    }

    /// <summary>The LibVLC options that request hardware decoding, and the software fallback set.</summary>
    public static IReadOnlyList<string> AccelerationOptions(bool useHardware) => useHardware
        ? [":avcodec-hw=any"]
        : [":avcodec-hw=none"];
}
