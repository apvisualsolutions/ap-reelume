// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// Takes the packed 4:2:2 picture LibVLC publishes wherever it has to go: the 32-bit BGRA the shell
/// draws, and the YUY2 a Direct3D 11 video processor takes in.
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
/// hardware overlay has always used. The picture is limited-range, so luma is offset by 16 and both
/// chroma planes by 128, and the coefficients that scale what is left carry the 255/219 and 255/224
/// gains of that range.
/// </para>
/// <para>
/// Which coefficients is not this class's decision, and until 2026-09-12 it was nobody's: BT.601 was
/// written in here and every high definition picture — which is BT.709 — came out wrong, pure red
/// landing 22 levels short. The matrix now arrives as an argument, from
/// <see cref="YuvMatrixPolicy"/>, and this class does the arithmetic and no choosing.
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
    /// <param name="matrix">The coefficients the picture was encoded with, chosen by the caller.</param>
    /// <param name="lumaLookup">
    /// The 256 levels each incoming luma is turned into before anything else happens, from
    /// <see cref="PictureAdjustment"/>, or empty to leave it alone. It rides on this loop rather
    /// than getting one of its own because the loop already walks every pixel: what it adds is one
    /// indexed read out of a table that fits in level-one cache.
    /// </param>
    public static void UyvyToBgra(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int width,
        int height,
        int sourceStride,
        int destinationStride,
        YuvColourMatrix matrix,
        ReadOnlySpan<byte> lumaLookup = default)
    {
        // The width is paired rather than merely positive: the format carries one chroma sample for
        // every two pixels, so an odd width names a pixel with no partner. Refusing it here is what
        // lets the loop below be the loop and nothing else — the caller aligns first, and AlignWidth
        // is the one place that decides how.
        ArgumentOutOfRangeException.ThrowIfNotEqual(width, AlignWidth(width));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceStride, width * SourceBytesPerPixel);
        ArgumentOutOfRangeException.ThrowIfLessThan(destinationStride, width * DestinationBytesPerPixel);
        if (source.Length < sourceStride * height || destination.Length < destinationStride * height)
        {
            throw new ArgumentException(
                "The conversion was given fewer rows than it was told to convert.",
                nameof(source));
        }

        // A table of any other size is an indexed read off the end of it, which paints whatever
        // follows in memory and does so differently every run. Empty means «leave the luma alone».
        if (lumaLookup.Length is not (0 or 256))
        {
            throw new ArgumentException(
                "A luma lookup has one entry per level or none at all.",
                nameof(lumaLookup));
        }

        var adjusted = lumaLookup.Length == 256;
        var pairs = width / 2;
        for (var row = 0; row < height; row++)
        {
            var read = source.Slice(row * sourceStride, pairs * 4);
            var write = destination.Slice(row * destinationStride, pairs * 8);
            for (var pair = 0; pair < pairs; pair++)
            {
                var at = pair * 4;
                var u = read[at] - 128;
                var firstLuma = (adjusted ? lumaLookup[read[at + 1]] : read[at + 1]) - 16;
                var v = read[at + 2] - 128;
                var secondLuma = (adjusted ? lumaLookup[read[at + 3]] : read[at + 3]) - 16;
                WritePixel(write[(pair * 8)..], firstLuma, u, v, matrix);
                WritePixel(write[((pair * 8) + 4)..], secondLuma, u, v, matrix);
            }
        }
    }

    /// <summary>
    /// Writes <paramref name="height"/> rows of UYVY into <paramref name="destination"/> as YUY2,
    /// which is the same picture with every neighbouring pair of bytes swapped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two formats carry identical samples in a different order: UYVY is U, Y0, V, Y1 and YUY2
    /// is Y0, U, Y1, V — «the same as the YUY2 format except the byte order is reversed», in the
    /// words of Microsoft's own <i>Recommended 8-Bit YUV Formats for Video Rendering</i>. So there is
    /// no colour conversion here at all, no matrix, and nothing to clamp.
    /// </para>
    /// <para>
    /// It exists because <b>DXGI has no UYVY</b>. A Direct3D 11 video processor takes
    /// <c>DXGI_FORMAT_YUY2</c> (107, «width must be even», mapped Y0→R8, U0→G8, Y1→B8, V0→A8), and
    /// there is no format in the enumeration for the bytes LibVLC publishes. Asking LibVLC for YUY2
    /// instead is not the way round it: measured on 2026-08-25, that takes the whole subtitle out of
    /// the frame.
    /// </para>
    /// <para>
    /// And this <b>removes</b> work rather than adding it. Every frame already walks these same
    /// bytes to make BGRA — a multiply, three clamps and twice the bytes written per pixel — so the
    /// baseline this is measured against is not zero: it is what stops being paid.
    /// </para>
    /// </remarks>
    public static void UyvyToYuy2(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int width,
        int height,
        int sourceStride,
        int destinationStride)
    {
        // The same guards as the BGRA conversion, and for the same reason: without them a short
        // stride or an odd width walks off the end of a row and the slice below reports it as an
        // index, which reads like a bug in here rather than a caller handing over a geometry the
        // buffers never held. Both strides are measured against the same two bytes a pixel —
        // source and destination are the identical packed 4:2:2 picture, only reordered.
        ArgumentOutOfRangeException.ThrowIfNotEqual(width, AlignWidth(width));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceStride, width * SourceBytesPerPixel);
        ArgumentOutOfRangeException.ThrowIfLessThan(destinationStride, width * SourceBytesPerPixel);

        // Two throws and not one «or», because the name is the whole message: while both halves
        // raised the same exception naming the source, a caller with a short destination was told
        // to look at the wrong buffer and the second half could be deleted without a test noticing.
        if (source.Length < sourceStride * height)
        {
            throw new ArgumentException(
                "The swap was given fewer rows than it was told to read.",
                nameof(source));
        }

        if (destination.Length < destinationStride * height)
        {
            throw new ArgumentException(
                "The swap was given fewer rows than it was told to write.",
                nameof(destination));
        }

        // This one has no counterpart in the BGRA conversion and needs none: that one writes twice
        // the bytes it reads, so nobody is tempted to run it in place. This one writes exactly as
        // many, and in place it destroys the picture quietly — the first byte written is the second
        // byte still to be read.
        if (source.Overlaps(destination))
        {
            throw new ArgumentException(
                "The swap cannot run in place: it would read bytes it has already overwritten.",
                nameof(destination));
        }

        var pairs = width / 2;
        for (var row = 0; row < height; row++)
        {
            var read = source.Slice(row * sourceStride, pairs * 4);
            var write = destination.Slice(row * destinationStride, pairs * 4);
            for (var pair = 0; pair < pairs; pair++)
            {
                var at = pair * 4;
                write[at] = read[at + 1];
                write[at + 1] = read[at];
                write[at + 2] = read[at + 3];
                write[at + 3] = read[at + 2];
            }
        }
    }

    private static void WritePixel(Span<byte> destination, int luma, int u, int v, YuvColourMatrix matrix)
    {
        var scaled = matrix.Luma * luma;
        destination[0] = Clamp((scaled + (matrix.BlueFromU * u) + 128) >> 8);
        destination[1] = Clamp((scaled - (matrix.GreenFromU * u) - (matrix.GreenFromV * v) + 128) >> 8);
        destination[2] = Clamp((scaled + (matrix.RedFromV * v) + 128) >> 8);
        destination[3] = 255;
    }

    private static byte Clamp(int value) => value switch
    {
        < 0 => 0,
        > 255 => 255,
        _ => (byte)value,
    };
}
