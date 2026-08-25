// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// The conversion the engine pays for subtitles. What matters is that the three colours a person
/// would notice survive the trip and that no argument can walk off the end of a buffer.
/// </summary>
public sealed class PackedYuvConverterTests
{
    [Theory]
    // Black, white, and the three primaries, in limited-range BT.601 as a decoder emits them.
    [InlineData(16, 128, 128, 0, 0, 0)]
    [InlineData(235, 128, 128, 255, 255, 255)]
    [InlineData(82, 90, 240, 255, 0, 0)]
    [InlineData(145, 54, 34, 0, 255, 0)]
    [InlineData(41, 240, 110, 0, 0, 255)]
    public void The_colours_a_person_would_notice_survive_the_conversion(
        byte luma,
        byte u,
        byte v,
        byte red,
        byte green,
        byte blue)
    {
        // One pair of pixels, which is the smallest picture the packed format can hold.
        var packed = new byte[] { u, luma, v, luma };
        var bgra = new byte[8];

        PackedYuvConverter.UyvyToBgra(packed, bgra, width: 2, height: 1, sourceStride: 4, destinationStride: 8);

        for (var pixel = 0; pixel < 2; pixel++)
        {
            Assert.InRange(bgra[(pixel * 4) + 2], red - 3, red + 3);
            Assert.InRange(bgra[(pixel * 4) + 1], green - 3, green + 3);
            Assert.InRange(bgra[pixel * 4], blue - 3, blue + 3);
            Assert.Equal(255, bgra[(pixel * 4) + 3]);
        }
    }

    [Fact]
    public void Every_row_is_converted_and_the_padding_of_a_wider_stride_is_left_alone()
    {
        const int Width = 4;
        const int Height = 3;
        const int SourceStride = 12;
        const int DestinationStride = 20;
        var packed = new byte[SourceStride * Height];
        var bgra = new byte[DestinationStride * Height];
        for (var row = 0; row < Height; row++)
        {
            for (var pair = 0; pair < Width / 2; pair++)
            {
                var at = (row * SourceStride) + (pair * 4);
                packed[at] = 128;
                packed[at + 1] = 235;
                packed[at + 2] = 128;
                packed[at + 3] = 235;
            }
        }

        PackedYuvConverter.UyvyToBgra(packed, bgra, Width, Height, SourceStride, DestinationStride);

        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                Assert.Equal(255, bgra[(row * DestinationStride) + (column * 4)]);
            }

            // What sits past the picture in a wider destination row is nobody's business here, and
            // writing into it would be how a bitmap of another size gets a stripe down its side.
            for (var spare = Width * 4; spare < DestinationStride; spare++)
            {
                Assert.Equal(0, bgra[(row * DestinationStride) + spare]);
            }
        }
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(1920, 1920)]
    [InlineData(1921, 1920)]
    [InlineData(0, 2)]
    [InlineData(-4, 2)]
    public void A_width_is_paired_because_the_format_carries_one_chroma_for_every_two_pixels(
        int width,
        int expected) =>
        Assert.Equal(expected, PackedYuvConverter.AlignWidth(width));

    [Theory]
    [InlineData(0, 1, 4, 8)]
    [InlineData(3, 1, 8, 16)]
    [InlineData(2, 0, 4, 8)]
    [InlineData(2, 1, 3, 8)]
    [InlineData(2, 1, 4, 7)]
    public void A_geometry_the_buffers_cannot_hold_is_refused_rather_than_written_past(
        int width,
        int height,
        int sourceStride,
        int destinationStride) =>
        Assert.ThrowsAny<ArgumentException>(() => PackedYuvConverter.UyvyToBgra(
            new byte[16],
            new byte[32],
            width,
            height,
            sourceStride,
            destinationStride));

    [Fact]
    public void Fewer_rows_than_the_conversion_was_told_to_read_is_refused()
    {
        var thrown = Assert.Throws<ArgumentException>(() => PackedYuvConverter.UyvyToBgra(
            new byte[4],
            new byte[64],
            width: 2,
            height: 4,
            sourceStride: 4,
            destinationStride: 8));

        Assert.Equal("source", thrown.ParamName);
    }
}
