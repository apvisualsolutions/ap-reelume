// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Buffers.Binary;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Media;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Media;

/// <summary>
/// The colour a container declares, read from headers built byte by byte so every shape the reader
/// meets in the wild — and every way a header can be broken — is provoked on purpose.
/// </summary>
/// <remarks>
/// The real samples, whose headers ffmpeg writes, are read in MediaTests next to ffprobe's answer.
/// These are the shapes a sample from one encoder never has: an unknown size, an eight-byte length,
/// a track list after the picture, a box that claims more than the file holds.
/// </remarks>
public sealed class ContainerColourReaderTests
{
    private static readonly SourceColour Hdr10 = new(9, 16, 9, 1000);

    [Fact]
    public void An_HDR10_Matroska_track_declares_its_curve_its_matrix_its_primaries_and_its_peak()
    {
        var file = Matroska(Tracks(AudioTrack(), VideoTrack(ColourElement(Hdr10))));

        Assert.Equal(Hdr10, Read(file));
        Assert.Equal(HdrFormat.Hdr10, Read(file).Hdr);
    }

    [Fact]
    public void A_Matroska_video_track_without_a_colour_element_declares_nothing()
    {
        Assert.Equal(SourceColour.Undeclared, Read(Matroska(Tracks(VideoTrack()))));
    }

    [Fact]
    public void A_Matroska_colour_element_with_only_some_fields_leaves_the_others_undeclared()
    {
        var colour = Element(0x55B0, Unsigned(0x55BA, 1), Element(0x55D0));

        Assert.Equal(new SourceColour(null, 1, null, null), Read(Matroska(Tracks(VideoTrack(colour)))));
    }

    [Fact]
    public void Only_the_first_video_track_decides()
    {
        var file = Matroska(Tracks(
            VideoTrack(ColourElement(new SourceColour(1, 1, 1, null))),
            VideoTrack(ColourElement(Hdr10))));

        Assert.Equal(new SourceColour(1, 1, 1, null), Read(file));
    }

    [Fact]
    public void A_track_entry_without_a_type_is_passed_over()
    {
        var untyped = Element(0xAE, Element(0xE0, ColourElement(Hdr10)));

        Assert.Equal(SourceColour.Undeclared, Read(Matroska(Tracks(untyped))));
    }

    [Fact]
    public void Elements_before_the_track_list_are_skipped_and_a_track_list_after_the_picture_is_not_read()
    {
        var info = Element(0x1549A966, Unsigned(0x2AD7B1, 1000000));
        var cluster = Element(0x1F43B675, Unsigned(0xE7, 0));

        Assert.Equal(Hdr10, Read(Matroska(info, Tracks(VideoTrack(ColourElement(Hdr10))))));
        Assert.Equal(SourceColour.Undeclared, Read(Matroska(cluster, Tracks(VideoTrack(ColourElement(Hdr10))))));
    }

    /// <summary>A live recording writes its segment with an unknown size, all ones; it runs to the end.</summary>
    [Fact]
    public void A_segment_of_unknown_size_runs_to_the_end_of_the_file()
    {
        var file = Concat(EbmlHeader(), Id(0x18538067), [0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], Tracks(VideoTrack(ColourElement(Hdr10))));

        Assert.Equal(Hdr10, Read(file));
    }

    [Fact]
    public void A_file_with_no_segment_declares_nothing()
    {
        Assert.Equal(SourceColour.Undeclared, Read(EbmlHeader()));
    }

    [Fact]
    public void A_length_byte_of_zero_or_a_file_cut_short_is_an_answer_and_not_an_error()
    {
        var zeroLength = Concat(EbmlHeader(), [0x18, 0x53, 0x80, 0x67, 0x00]);
        var cutInsideAHeader = Concat(EbmlHeader(), Id(0x18538067), [0x01, 0x00]);

        Assert.Equal(SourceColour.Undeclared, Read(zeroLength));
        Assert.Equal(SourceColour.Undeclared, Read(cutInsideAHeader));
        Assert.Equal(SourceColour.Undeclared, Read([0x1A, 0x45]));
    }

    [Fact]
    public void A_file_that_is_neither_container_declares_nothing()
    {
        Assert.Equal(SourceColour.Undeclared, Read("RIFF\0\0\0\0AVI LIST"u8.ToArray()));
    }

    [Fact]
    public void An_HDR10_MP4_declares_its_colour_box_and_its_light_level()
    {
        var file = Mp4(Box("trak", Media("vide", VisualEntry(Colr("nclx", 9, 16, 9), Box("clli", [0x03, 0xE8, 0x01, 0x90])))));

        Assert.Equal(Hdr10, Read(file));
    }

    /// <summary>QuickTime writes <c>nclc</c>, the same three codes without the range flag.</summary>
    [Fact]
    public void A_QuickTime_colour_box_is_read_like_an_MP4_one()
    {
        var file = Mp4(Box("trak", Media("vide", VisualEntry(Colr("nclc", 1, 1, 1)))));

        Assert.Equal(new SourceColour(1, 1, 1, null), Read(file));
    }

    [Fact]
    public void An_MP4_colour_box_carrying_an_ICC_profile_declares_nothing()
    {
        var file = Mp4(Box("trak", Media("vide", VisualEntry(Colr("prof", 9, 16, 9)))));

        Assert.Equal(SourceColour.Undeclared, Read(file));
    }

    [Fact]
    public void The_sound_track_of_an_MP4_is_passed_over_for_the_picture()
    {
        var file = Mp4(
            Box("trak", Media("soun", VisualEntry(Colr("nclx", 1, 1, 1)))),
            Box("trak", Box("tkhd", new byte[4])),
            Box("trak", Media("vide", VisualEntry(Colr("nclx", 9, 16, 9)))));

        Assert.Equal(new SourceColour(9, 16, 9, null), Read(file));
    }

    [Fact]
    public void A_video_track_with_no_sample_description_declares_nothing()
    {
        var file = Mp4(Box("trak", Box("mdia", Handler("vide"), Box("minf", Box("stbl", Box("stsd", new byte[8]))))));

        Assert.Equal(SourceColour.Undeclared, Read(file));
    }

    [Fact]
    public void An_MP4_without_a_video_track_declares_nothing()
    {
        Assert.Equal(SourceColour.Undeclared, Read(Mp4(Box("trak", Media("soun", VisualEntry())))));
    }

    /// <summary>
    /// A size of one moves the real size to eight more bytes, a size of zero runs to the end, and a
    /// size smaller than its own header ends the walk rather than looping on it.
    /// </summary>
    [Fact]
    public void The_three_special_box_sizes_are_honoured()
    {
        var track = Box("trak", Media("vide", VisualEntry(Colr("nclx", 9, 16, 9))));
        var largeMoov = Concat(U32(1), "moov"u8.ToArray(), U64(16 + track.Length), track);
        var openMoov = Concat(U32(0), "moov"u8.ToArray(), track);
        var broken = Concat(Ftyp(), U32(4), "free"u8.ToArray(), Box("moov", track));

        Assert.Equal(new SourceColour(9, 16, 9, null), Read(Concat(Ftyp(), largeMoov)));
        Assert.Equal(new SourceColour(9, 16, 9, null), Read(Concat(Ftyp(), openMoov)));
        Assert.Equal(SourceColour.Undeclared, Read(broken));
    }

    [Fact]
    public void A_path_that_cannot_be_opened_declares_nothing()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "film.mkv");

        Assert.Equal(SourceColour.Undeclared, ContainerColourReader.Read(missing));

        // A folder refuses to be opened as a file with access denied — measured, and only without a
        // trailing separator: with one, the same call fails as a missing path instead.
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Assert.Equal(SourceColour.Undeclared, ContainerColourReader.Read(folder.TrimEnd(Path.DirectorySeparatorChar)));
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    [Fact]
    public void A_file_on_disk_is_read_like_a_stream()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".mkv");
        File.WriteAllBytes(path, Matroska(Tracks(VideoTrack(ColourElement(Hdr10)))));
        try
        {
            Assert.Equal(Hdr10, ContainerColourReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SourceColour Read(byte[] file) => ContainerColourReader.Read(new MemoryStream(file));

    // Matroska, with every size written in eight bytes except where a test says otherwise.
    private static byte[] Matroska(params byte[][] children) =>
        Concat(EbmlHeader(), Element(0x18538067, children));

    private static byte[] EbmlHeader() => Element(0x1A45DFA3, Unsigned(0x4282, 0x6D6174726F736B61));

    private static byte[] Tracks(params byte[][] entries) => Element(0x1654AE6B, entries);

    private static byte[] VideoTrack(params byte[][] videoChildren) =>
        Element(0xAE, Unsigned(0xD7, 1), Unsigned(0x83, 1), Element(0xE0, [Unsigned(0xB0, 320), .. videoChildren]));

    private static byte[] AudioTrack() => Element(0xAE, Unsigned(0x83, 2), Element(0xE1));

    private static byte[] ColourElement(SourceColour colour) => Element(
        0x55B0,
        [
            .. new (uint Id, int? Value)[]
            {
                (0x55B1, colour.Matrix),
                (0x55BA, colour.Transfer),
                (0x55BB, colour.Primaries),
                (0x55BC, colour.MaxContentLight),
            }
            .Where(field => field.Value is not null)
            .Select(field => Unsigned(field.Id, field.Value!.Value)),
        ]);

    private static byte[] Element(uint id, params byte[][] children)
    {
        var body = Concat(children);
        var size = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(size, (ulong)body.Length);
        size[0] = 0x01;
        return Concat(Id(id), size, body);
    }

    /// <summary>A small unsigned element, its size in the one-byte form.</summary>
    private static byte[] Unsigned(uint id, long value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        var used = bytes.SkipWhile(part => part == 0).ToArray();
        return Concat(Id(id), [(byte)(0x80 | used.Length)], used);
    }

    private static byte[] Id(uint id)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, id);
        return [.. bytes.SkipWhile(part => part == 0)];
    }

    // MP4.
    private static byte[] Mp4(params byte[][] tracks) => Concat(Ftyp(), Box("moov", tracks));

    private static byte[] Ftyp() => Box("ftyp", "isom\0\0\x02\0"u8.ToArray());

    private static byte[] Media(string handler, byte[] entry) =>
        Box("mdia", Box("mdhd", new byte[24]), Handler(handler), Box("minf", Box("stbl", Box("stsd", new byte[8], entry))));

    private static byte[] Handler(string kind) =>
        Box("hdlr", new byte[8], System.Text.Encoding.ASCII.GetBytes(kind), new byte[12]);

    private static byte[] VisualEntry(params byte[][] children) => Box("hvc1", [new byte[78], .. children]);

    private static byte[] Colr(string kind, int primaries, int transfer, int matrix)
    {
        var codes = new byte[7];
        BinaryPrimitives.WriteUInt16BigEndian(codes, (ushort)primaries);
        BinaryPrimitives.WriteUInt16BigEndian(codes.AsSpan(2), (ushort)transfer);
        BinaryPrimitives.WriteUInt16BigEndian(codes.AsSpan(4), (ushort)matrix);
        return Box("colr", System.Text.Encoding.ASCII.GetBytes(kind), codes);
    }

    private static byte[] Box(string type, params byte[][] children)
    {
        var body = Concat(children);
        return Concat(U32(8 + body.Length), System.Text.Encoding.ASCII.GetBytes(type), body);
    }

    private static byte[] U32(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)value);
        return bytes;
    }

    private static byte[] U64(long value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, (ulong)value);
        return bytes;
    }

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];
}
