// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.IO.Compression;

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// Writes a PNG from raw pixels, without a drawing library.
/// </summary>
/// <remarks>
/// <b>It exists because the alternative asked LibVLC to write the file, and that route does not
/// survive a machine with no screen.</b> The first version of the course thumbnail used
/// <c>TakeSnapshot</c>, which works here and failed on a hosted runner — no frame at all, measured
/// on 2026-09-03. The frames the callback path hands over need no video output of any kind, and the
/// spike had already measured them arriving in 137 ms, so what was missing was only somebody to
/// encode them.
/// <para>
/// Sixty lines against a package: this repository has one declared image dependency and it is none,
/// and adding one to save a header, a CRC and a call to the compressor already in the framework
/// would be paying in supply chain for something the framework does. What it writes is the plainest
/// PNG there is — one image, eight bits a channel, RGBA, no interlacing — which is what every reader
/// handles first and best.
/// </para>
/// </remarks>
public static class PngWriter
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Writes <paramref name="bgra"/> as a PNG at <paramref name="path"/>.
    /// </summary>
    /// <param name="bgra">
    /// The pixels as LibVLC hands them over in RV32: four bytes each, blue first. They are swapped
    /// on the way out, because PNG reads red first and a picture written the other way round is a
    /// picture where every face is blue.
    /// </param>
    /// <param name="width">Pixels across.</param>
    /// <param name="height">Rows.</param>
    /// <param name="stride">
    /// Bytes per row as the decoder laid them out, which is <b>not</b> always width times four: a
    /// decoder aligns its rows, and reading them as if it had not shears the picture diagonally.
    /// </param>
    /// <param name="path">Where to write it.</param>
    public static void WriteBgra(ReadOnlySpan<byte> bgra, int width, int height, int stride, string path)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, width * 4);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (bgra.Length < stride * height)
        {
            throw new ArgumentException(
                $"The picture is {bgra.Length} bytes and {height} rows of {stride} need {stride * height}.",
                nameof(bgra));
        }

        // One filter byte per row — zero, «none» — then the row in RGBA. Every PNG row carries one,
        // and a stream written without them decodes as noise rather than as a wrong colour.
        var raw = new byte[height * ((width * 4) + 1)];
        var at = 0;
        for (var y = 0; y < height; y++)
        {
            raw[at++] = 0;
            var row = bgra.Slice(y * stride, width * 4);
            for (var x = 0; x < width; x++)
            {
                raw[at++] = row[(x * 4) + 2];
                raw[at++] = row[(x * 4) + 1];
                raw[at++] = row[x * 4];
                raw[at++] = row[(x * 4) + 3];
            }
        }

        using var file = File.Create(path);
        file.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(header.Slice(4, 4), height);
        header[8] = 8;   // bits per channel
        header[9] = 6;   // colour type: truecolour with alpha
        header[10] = 0;  // deflate, the only compression PNG has
        header[11] = 0;  // the only filter method
        header[12] = 0;  // not interlaced
        WriteChunk(file, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        WriteChunk(file, "IDAT", compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(file, "IEND", []);
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);

        Span<byte> name = stackalloc byte[4];
        for (var i = 0; i < 4; i++)
        {
            name[i] = (byte)type[i];
        }

        stream.Write(name);
        stream.Write(data);

        // The CRC covers the type and the data but not the length, which is the one detail a reader
        // rejects the whole file over.
        var crc = Crc32(name, data);
        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, crc);
        stream.Write(checksum);
    }

    /// <summary>The CRC-32 PNG specifies, computed without a table because it is run four times.</summary>
    private static uint Crc32(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var crc = 0xFFFFFFFFu;
        crc = Accumulate(crc, first);
        crc = Accumulate(crc, second);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint Accumulate(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc;
    }
}
