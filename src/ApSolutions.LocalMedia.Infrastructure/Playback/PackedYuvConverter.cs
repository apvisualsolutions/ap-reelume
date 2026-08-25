// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// Turns the packed 4:2:2 picture LibVLC publishes into the 32-bit BGRA the shell draws.
/// </summary>
/// <remarks>
/// <para>
/// The engine asks LibVLC for <c>UYVY</c> rather than <c>RV32</c>, and the reason is subtitles.
/// Measured on 2026-08-25 against a real episode: with <c>RV32</c>, <c>RGBA</c>, <c>ARGB</c>,
/// <c>RV24</c>, <c>YUY2</c>, <c>VYUY</c> and <c>YVYU</c>, <b>not one byte</b> of the published frame
/// changed when a subtitle covering the whole film was switched on — while with <c>UYVY</c> 61 687
/// bytes changed, and the picture written to disk showed the line. The memory output tells the core
/// it can take subpictures itself for the other formats, and the display callback LibVLC hands a
/// managed application has no parameter to receive one, so every subtitle was dropped without a
/// word. That is why the owner saw subtitles in VLC and none here.
/// </para>
/// <para>
/// The price is this conversion and half the horizontal chroma resolution, which is what every
/// hardware overlay has always used. The coefficients are the integer BT.601 full-swing ones; the
/// picture is limited-range, so luma is offset by 16 and both chroma planes by 128.
/// </para>
/// </remarks>
public static class PackedYuvConverter
{
    /// <summary>Bytes one pixel of the packed source occupies.</summary>
    public const int SourceBytesPerPixel = 2;

    /// <summary>Bytes one pixel of the destination occupies.</summary>
    public const int DestinationBytesPerPixel = 4;

    /// <summary>
    /// A width LibVLC can publish as <c>UYVY</c>: the format pairs neighbouring pixels, so an odd
    /// one has no partner. Never below two, which is the smallest picture the pairing allows.
    /// </summary>
    public static int AlignWidth(int width) => width < 2 ? 2 : width - (width % 2);

    /// <summary>
    /// Writes <paramref name="height"/> rows of UYVY into <paramref name="destination"/> as BGRA.
    /// </summary>
    /// <param name="source">The packed picture, at least <paramref name="sourceStride"/> per row.</param>
    /// <param name="destination">The BGRA picture, at least <paramref name="destinationStride"/> per row.</param>
    public static void UyvyToBgra(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int width,
        int height,
        int sourceStride,
        int destinationStride)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceStride, width * SourceBytesPerPixel);
        ArgumentOutOfRangeException.ThrowIfLessThan(destinationStride, width * DestinationBytesPerPixel);
        if (source.Length < sourceStride * height || destination.Length < destinationStride * height)
        {
            throw new ArgumentException(
                "The conversion was given fewer rows than it was told to convert.",
                nameof(source));
        }

        var pairs = width / 2;
        for (var row = 0; row < height; row++)
        {
            var read = source.Slice(row * sourceStride, pairs * 4);
            var write = destination.Slice(row * destinationStride, pairs * 8);
            for (var pair = 0; pair < pairs; pair++)
            {
                var at = pair * 4;
                var u = read[at] - 128;
                var firstLuma = read[at + 1] - 16;
                var v = read[at + 2] - 128;
                var secondLuma = read[at + 3] - 16;
                WritePixel(write[(pair * 8)..], firstLuma, u, v);
                WritePixel(write[((pair * 8) + 4)..], secondLuma, u, v);
            }

            // An odd width has a luma with no partner. It cannot happen through AlignWidth, and
            // repeating the neighbour rather than leaving a black column is what keeps a caller
            // that bypassed the alignment from drawing a stripe down the edge of the picture.
            if (width % 2 == 1 && pairs > 0)
            {
                var previous = destination.Slice((row * destinationStride) + ((width - 2) * 4), 4);
                previous.CopyTo(destination.Slice((row * destinationStride) + ((width - 1) * 4), 4));
            }
        }
    }

    private static void WritePixel(Span<byte> destination, int luma, int u, int v)
    {
        var scaled = 298 * luma;
        destination[0] = Clamp((scaled + (516 * u) + 128) >> 8);
        destination[1] = Clamp((scaled - (100 * u) - (208 * v) + 128) >> 8);
        destination[2] = Clamp((scaled + (409 * v) + 128) >> 8);
        destination[3] = 255;
    }

    private static byte Clamp(int value) => value switch
    {
        < 0 => 0,
        > 255 => 255,
        _ => (byte)value,
    };
}
