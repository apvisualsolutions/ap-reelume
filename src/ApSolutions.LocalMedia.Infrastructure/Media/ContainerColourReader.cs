// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Buffers.Binary;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Infrastructure.Media;

/// <summary>
/// Reads what a video file declares about its colour out of the container's header: Matroska's
/// <c>Colour</c> element, or the MP4 <c>colr</c> and <c>clli</c> boxes of the first video track.
/// </summary>
/// <remarks>
/// <para>
/// It exists because LibVLC 3 publishes no colour information for a track, and without it an HDR10
/// film was played and labelled as standard range. <c>ffprobe</c> reads the same thing, and it is a
/// test tool that is not on the machine of whoever runs the application.
/// </para>
/// <para>
/// It walks the header by skipping from one element to the next by their declared sizes, so it reads
/// a few hundred bytes of a film and never the picture. <b>Anything it does not understand is an
/// answer, not an error</b>: a truncated file, a size that points outside it, a track list that comes
/// after the picture — each is <see cref="SourceColour.Undeclared"/>, which plays the film exactly as
/// it was played before this reader existed.
/// </para>
/// </remarks>
public static class ContainerColourReader
{
    private const uint EbmlMagic = 0x1A45DFA3;
    private const uint Segment = 0x18538067;
    private const uint Tracks = 0x1654AE6B;
    private const uint Cluster = 0x1F43B675;
    private const uint TrackEntry = 0xAE;
    private const uint TrackType = 0x83;
    private const uint Video = 0xE0;
    private const uint Colour = 0x55B0;
    private const uint MatrixCoefficients = 0x55B1;
    private const uint TransferCharacteristics = 0x55BA;
    private const uint ColourPrimaries = 0x55BB;
    private const uint MaxContentLight = 0x55BC;
    private const int VideoTrackType = 1;

    // A visual sample entry's own fields before its child boxes: six reserved bytes, the data
    // reference index and the seventy bytes of ISO/IEC 14496-12 §12.1.3.
    private const int VisualSampleEntryFields = 78;

    /// <summary>What the file at <paramref name="path"/> declares, or nothing if it cannot be read.</summary>
    public static SourceColour Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Read(stream);
        }
        catch (IOException)
        {
            return SourceColour.Undeclared;
        }
        catch (UnauthorizedAccessException)
        {
            return SourceColour.Undeclared;
        }
    }

    /// <summary>What the container in <paramref name="stream"/> declares; the stream must be seekable.</summary>
    public static SourceColour Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            var head = ReadBytes(stream, 0, 8);
            if (BinaryPrimitives.ReadUInt32BigEndian(head) == EbmlMagic)
            {
                return ReadMatroska(stream);
            }

            return head.AsSpan(4).SequenceEqual("ftyp"u8) ? ReadMp4(stream) : SourceColour.Undeclared;
        }
        catch (EndOfStreamException)
        {
            return SourceColour.Undeclared;
        }
    }

    private static SourceColour ReadMatroska(Stream stream)
    {
        var segment = Children(stream, 0, stream.Length).FirstOrDefault(element => element.Id == Segment);
        foreach (var child in Children(stream, segment.Start, segment.End))
        {
            if (child.Id == Cluster)
            {
                // The picture has started and no track list came before it.
                break;
            }

            if (child.Id != Tracks)
            {
                continue;
            }

            foreach (var entry in Children(stream, child.Start, child.End).Where(element => element.Id == TrackEntry))
            {
                var fields = Children(stream, entry.Start, entry.End).ToList();
                if (fields.Find(field => field.Id == TrackType) is not { Id: TrackType } type
                    || ReadUnsigned(stream, type) != VideoTrackType)
                {
                    continue;
                }

                var colour = fields.Where(field => field.Id == Video)
                    .SelectMany(video => Children(stream, video.Start, video.End))
                    .FirstOrDefault(field => field.Id == Colour);
                return colour.Id == Colour ? ReadMatroskaColour(stream, colour) : SourceColour.Undeclared;
            }
        }

        return SourceColour.Undeclared;
    }

    private static SourceColour ReadMatroskaColour(Stream stream, Element colour)
    {
        int? primaries = null, transfer = null, matrix = null, light = null;
        foreach (var field in Children(stream, colour.Start, colour.End))
        {
            switch (field.Id)
            {
                case MatrixCoefficients: matrix = ReadUnsigned(stream, field); break;
                case TransferCharacteristics: transfer = ReadUnsigned(stream, field); break;
                case ColourPrimaries: primaries = ReadUnsigned(stream, field); break;
                case MaxContentLight: light = ReadUnsigned(stream, field); break;
            }
        }

        return new SourceColour(primaries, transfer, matrix, light);
    }

    private static SourceColour ReadMp4(Stream stream)
    {
        var tracks = Boxes(stream, 0, stream.Length)
            .Where(box => box.Id == Tag("moov"))
            .SelectMany(moov => Boxes(stream, moov.Start, moov.End))
            .Where(box => box.Id == Tag("trak"));
        foreach (var trak in tracks)
        {
            var mdia = Boxes(stream, trak.Start, trak.End).FirstOrDefault(box => box.Id == Tag("mdia"));
            var handler = Boxes(stream, mdia.Start, mdia.End).FirstOrDefault(box => box.Id == Tag("hdlr"));
            if (handler.Id != Tag("hdlr") || ReadTag(stream, handler.Start + 8) != Tag("vide"))
            {
                continue;
            }

            // The first video track decides, as it does for Matroska. The sample descriptions start
            // after a full box header and an entry count, eight bytes in all.
            var descriptions = Descend(stream, mdia, "minf", "stbl", "stsd");
            var entry = Boxes(stream, descriptions.Start + 8, descriptions.End).FirstOrDefault();
            return entry.Id == 0 ? SourceColour.Undeclared : ReadMp4Colour(stream, entry);
        }

        return SourceColour.Undeclared;
    }

    private static SourceColour ReadMp4Colour(Stream stream, Element entry)
    {
        var colour = SourceColour.Undeclared;
        int? light = null;
        foreach (var box in Boxes(stream, entry.Start + VisualSampleEntryFields, entry.End))
        {
            if (box.Id == Tag("clli"))
            {
                light = BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(stream, box.Start, 2));
            }
            else if (box.Id == Tag("colr") && ReadTag(stream, box.Start) is var kind && (kind == Tag("nclx") || kind == Tag("nclc")))
            {
                var codes = ReadBytes(stream, box.Start + 4, 6);
                colour = new SourceColour(
                    BinaryPrimitives.ReadUInt16BigEndian(codes),
                    BinaryPrimitives.ReadUInt16BigEndian(codes.AsSpan(2)),
                    BinaryPrimitives.ReadUInt16BigEndian(codes.AsSpan(4)),
                    null);
            }
        }

        return colour with { MaxContentLight = light };
    }

    private static Element Descend(Stream stream, Element from, params string[] path)
    {
        foreach (var name in path)
        {
            from = Boxes(stream, from.Start, from.End).FirstOrDefault(box => box.Id == Tag(name));
        }

        return from;
    }

    /// <summary>The EBML elements between two offsets, each with its data range.</summary>
    private static IEnumerable<Element> Children(Stream stream, long start, long end)
    {
        var at = start;
        while (at < end)
        {
            var id = ReadVariable(stream, ref at, keepMarker: true);
            var size = ReadVariable(stream, ref at, keepMarker: false);

            // An unknown size runs to the end of its parent, which is what live recordings write.
            var elementEnd = size < 0 ? end : Math.Min(at + size, end);
            yield return new Element((uint)id, at, elementEnd);
            at = elementEnd;
        }
    }

    /// <summary>The MP4 boxes between two offsets, each with its data range.</summary>
    private static IEnumerable<Element> Boxes(Stream stream, long start, long end)
    {
        var at = start;
        while (at + 8 <= end)
        {
            var header = ReadBytes(stream, at, 8);
            long size = BinaryPrimitives.ReadUInt32BigEndian(header);
            var dataStart = at + 8;
            if (size == 1)
            {
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(ReadBytes(stream, dataStart, 8));
                dataStart += 8;
            }
            else if (size == 0)
            {
                size = end - at;
            }

            if (size < dataStart - at)
            {
                yield break;
            }

            var boxEnd = Math.Min(at + size, end);
            yield return new Element(BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4)), dataStart, boxEnd);
            at = boxEnd;
        }
    }

    /// <summary>
    /// One EBML variable-length integer: its length is the count of leading zero bits of the first
    /// byte plus one. An identifier keeps its marker bit; a size drops it, and all ones is «unknown».
    /// </summary>
    private static long ReadVariable(Stream stream, ref long at, bool keepMarker)
    {
        var first = ReadBytes(stream, at, 1)[0];
        var length = System.Numerics.BitOperations.LeadingZeroCount((uint)first) - 23;
        if (length > 8)
        {
            throw new EndOfStreamException("An EBML length byte of zero declares no length.");
        }

        var bytes = ReadBytes(stream, at, length);
        at += length;
        var marker = 1L << (7 * length);
        long value = 0;
        foreach (var part in bytes)
        {
            value = (value << 8) | part;
        }

        if (keepMarker)
        {
            return value;
        }

        value &= marker - 1;
        return value == marker - 1 ? -1 : value;
    }

    private static int ReadUnsigned(Stream stream, Element element)
    {
        long value = 0;
        foreach (var part in ReadBytes(stream, element.Start, (int)Math.Min(element.End - element.Start, 4)))
        {
            value = (value << 8) | part;
        }

        return (int)Math.Min(value, int.MaxValue);
    }

    private static uint ReadTag(Stream stream, long at) => BinaryPrimitives.ReadUInt32BigEndian(ReadBytes(stream, at, 4));

    private static uint Tag(string name) =>
        ((uint)name[0] << 24) | ((uint)name[1] << 16) | ((uint)name[2] << 8) | name[3];

    private static byte[] ReadBytes(Stream stream, long at, int count)
    {
        var buffer = new byte[count];
        stream.Position = at;
        stream.ReadExactly(buffer);
        return buffer;
    }

    private readonly record struct Element(uint Id, long Start, long End);
}
