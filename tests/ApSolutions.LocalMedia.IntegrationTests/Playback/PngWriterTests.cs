// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Playback;

/// <summary>
/// The PNG a course's picture is written as, checked byte by byte rather than by eye.
/// </summary>
/// <remarks>
/// It exists because the route that had LibVLC write the file does not survive a machine with no
/// screen — measured on 2026-09-03, one red build with no frame at all on a hosted runner. The
/// frames the callback path hands over need no video output, so what was missing was an encoder.
/// <para>
/// <b>Nothing here decodes a video</b>, which is the whole point: this is bytes in and bytes out,
/// and it is the half of the thumbnail that a machine without a decoder can still check.
/// </para>
/// </remarks>
public sealed class PngWriterTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ap-reelume-png-" + Guid.NewGuid().ToString("N"));

    public PngWriterTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>What it writes is a PNG: the signature, the header, and the end marker.</summary>
    /// <remarks>
    /// Asserted against the format rather than against a golden file. A golden PNG would pin this to
    /// one compressor's output, and the framework's deflate is free to improve.
    /// </remarks>
    [Fact]
    public void It_writes_something_a_reader_will_open()
    {
        var path = Write(4, 3);
        var bytes = File.ReadAllBytes(path);

        Assert.Equal<byte[]>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], bytes[..8]);

        // The header names the size the caller asked for, which is what a picture written with the
        // width and height the wrong way round would fail.
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(bytes, 12, 4));
        Assert.Equal(4, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
        Assert.Equal(8, bytes[24]);
        Assert.Equal(6, bytes[25]);

        Assert.Contains("IDAT", System.Text.Encoding.ASCII.GetString(bytes));
        Assert.EndsWith("IEND", System.Text.Encoding.ASCII.GetString(bytes[^8..^4]), StringComparison.Ordinal);
    }

    /// <summary>
    /// Every chunk's checksum is the one PNG asks for, computed over the type and the data.
    /// </summary>
    /// <remarks>
    /// <b>The one detail a reader rejects the whole file over</b>, and the one a test written against
    /// «it has the right bytes at the front» would never see. The CRC covers the type and the data
    /// and NOT the length, which is exactly the mistake a hand-written encoder makes.
    /// </remarks>
    [Fact]
    public void Every_chunk_carries_the_checksum_the_format_demands()
    {
        var bytes = File.ReadAllBytes(Write(5, 2));

        var at = 8;
        var chunks = 0;
        while (at < bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(at, 4));
            var stored = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at + 8 + length, 4));

            Assert.Equal(Crc32(bytes.AsSpan(at + 4, 4 + length)), stored);
            chunks++;
            at += 12 + length;
        }

        // Exactly the three a picture needs, and no trailing rubbish: `at` landing past the end
        // would mean a length that lied.
        Assert.Equal(3, chunks);
        Assert.Equal(bytes.Length, at);
    }

    /// <summary>
    /// The pixels come out red-first, which is the swap that decides whether faces are blue.
    /// </summary>
    /// <remarks>
    /// LibVLC hands over RV32 — blue first — and PNG reads red first. Asserted by decompressing the
    /// image data rather than by looking at it, because the two orders produce equally valid files
    /// and only one of them is the right picture.
    /// </remarks>
    [Fact]
    public void The_channels_come_out_in_the_order_png_reads_them()
    {
        // One pixel, unmistakable: blue 10, green 20, red 30, alpha 40 as LibVLC lays it out.
        var path = Path.Combine(_root, "one.png");
        PngWriter.WriteBgra([10, 20, 30, 40], 1, 1, 4, path);

        var raw = Decompress(File.ReadAllBytes(path));

        // The filter byte, then red, green, blue, alpha.
        Assert.Equal<byte[]>([0, 30, 20, 10, 40], raw);
    }

    /// <summary>
    /// A decoder's padded rows are read as padded, not as if the padding were picture.
    /// </summary>
    /// <remarks>
    /// A decoder aligns its rows, so the bytes per row are not always four times the width. Read as
    /// if they were, every row after the first starts a few bytes late and the picture shears
    /// diagonally — which looks like a decoding fault rather than like this.
    /// </remarks>
    [Fact]
    public void Padded_rows_are_read_as_padded()
    {
        // Two rows of one pixel each, in a buffer whose rows are eight bytes: four of picture and
        // four of padding that must never be read.
        var pixels = new byte[]
        {
            1, 2, 3, 255, 99, 99, 99, 99,
            4, 5, 6, 255, 99, 99, 99, 99,
        };

        var path = Path.Combine(_root, "padded.png");
        PngWriter.WriteBgra(pixels, 1, 2, 8, path);

        var raw = Decompress(File.ReadAllBytes(path));

        Assert.Equal<byte[]>([0, 3, 2, 1, 255, 0, 6, 5, 4, 255], raw);
    }

    /// <summary>Everything it cannot write is refused rather than written wrong.</summary>
    /// <remarks>
    /// A stride under the width is the one that would otherwise read past the end of a row and into
    /// the next, which produces a file rather than an error — the worst of the outcomes.
    /// </remarks>
    [Fact]
    public void What_it_cannot_write_is_refused()
    {
        var path = Path.Combine(_root, "never.png");

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PngWriter.WriteBgra(new byte[16], 0, 1, 4, path));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PngWriter.WriteBgra(new byte[16], 1, 0, 4, path));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PngWriter.WriteBgra(new byte[16], 4, 1, 8, path));
        _ = Assert.Throws<ArgumentException>(() => PngWriter.WriteBgra(new byte[4], 2, 2, 8, path));
        _ = Assert.Throws<ArgumentException>(() => PngWriter.WriteBgra(new byte[16], 1, 1, 4, "  "));

        Assert.False(File.Exists(path), "a refused picture left a file behind.");
    }

    private string Write(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i * 7);
        }

        var path = Path.Combine(_root, $"{width}x{height}.png");
        PngWriter.WriteBgra(pixels, width, height, width * 4, path);
        return path;
    }

    /// <summary>The image data, decompressed, as a reader would.</summary>
    private static byte[] Decompress(byte[] png)
    {
        var at = 8;
        while (at < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at, 4));
            if (System.Text.Encoding.ASCII.GetString(png, at + 4, 4) == "IDAT")
            {
                using var source = new MemoryStream(png, at + 8, length);
                using var inflate = new System.IO.Compression.ZLibStream(
                    source,
                    System.IO.Compression.CompressionMode.Decompress);
                using var target = new MemoryStream();
                inflate.CopyTo(target);
                return target.ToArray();
            }

            at += 12 + length;
        }

        throw new InvalidOperationException("The file carries no image data at all.");
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
