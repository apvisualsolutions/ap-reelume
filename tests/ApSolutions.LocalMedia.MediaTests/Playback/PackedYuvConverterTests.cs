// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// The conversion the engine pays for subtitles. What matters is that the three colours a person
/// would notice survive the trip and that no argument can walk off the end of a buffer.
/// </summary>
public sealed class PackedYuvConverterTests
{
    private static readonly YuvColourMatrix Bt601 = YuvMatrixPolicy.MatrixFor(YuvColourSpace.Bt601);

    [Theory]
    // Black, white, and the three primaries, in limited-range BT.601 as a decoder emits them.
    [InlineData(16, 128, 128, 0, 0, 0)]
    [InlineData(235, 128, 128, 255, 255, 255)]
    [InlineData(82, 90, 240, 255, 0, 0)]
    [InlineData(145, 54, 34, 0, 255, 0)]
    [InlineData(41, 240, 110, 0, 0, 255)]
    // And two colours that saturate no channel, which is what actually measures the coefficients.
    // A saturated primary clamps two of its three channels to 0 or 255, so a coefficient can be
    // doubled and the row still passes — measured: doubling the blue coefficient left all ten rows
    // above green and the whole suite with it. These two land every channel in mid-range, and each
    // of the four chroma coefficients doubled moves one of them by more than the tolerance.
    [InlineData(135, 91, 81, 63, 191, 64)]
    [InlineData(96, 161, 149, 127, 63, 160)]
    public void The_colours_a_person_would_notice_survive_the_conversion(
        byte luma,
        byte u,
        byte v,
        byte red,
        byte green,
        byte blue) =>
        AssertPrimarySurvives(YuvColourSpace.Bt601, luma, u, v, red, green, blue);

    [Theory]
    // The same colours in limited-range BT.709, which is what anything 720 lines or taller is
    // encoded in. Every sample was read on 2026-09-12 out of ffmpeg converting the same pictures
    // with each matrix in turn.
    //
    // Black and white are here for completeness and measure nothing about the matrix: their chroma
    // is 128, so every chroma coefficient multiplies zero and the two matrices produce identical
    // bytes. The rows that tell the two apart are the five below them.
    [InlineData(16, 128, 128, 0, 0, 0)]
    [InlineData(235, 128, 128, 255, 255, 255)]
    [InlineData(63, 102, 240, 255, 0, 0)]
    [InlineData(173, 42, 26, 0, 255, 0)]
    [InlineData(32, 240, 118, 0, 0, 255)]
    [InlineData(149, 85, 77, 63, 191, 64)]
    [InlineData(88, 164, 152, 127, 63, 160)]
    public void The_same_colours_survive_the_high_definition_matrix_the_pictures_really_use(
        byte luma,
        byte u,
        byte v,
        byte red,
        byte green,
        byte blue) =>
        AssertPrimarySurvives(YuvColourSpace.Bt709, luma, u, v, red, green, blue);

    [Fact]
    public void Decoding_a_high_definition_sample_with_the_standard_definition_matrix_is_the_defect_being_fixed()
    {
        // The control that gives the two theories above their meaning. Pure red in BT.709 run
        // through the BT.601 matrix is what the player did until now, and it is not a rounding
        // difference: 22 levels of red. Without this, a converter that quietly ignored its matrix
        // would pass both theories on the rows where the two happen to agree.
        var packed = new byte[] { 102, 63, 240, 63 };
        var wrong = new byte[8];
        var right = new byte[8];

        PackedYuvConverter.UyvyToBgra(
            packed, wrong, width: 2, height: 1, sourceStride: 4, destinationStride: 8,
            YuvMatrixPolicy.MatrixFor(YuvColourSpace.Bt601));
        PackedYuvConverter.UyvyToBgra(
            packed, right, width: 2, height: 1, sourceStride: 4, destinationStride: 8,
            YuvMatrixPolicy.MatrixFor(YuvColourSpace.Bt709));

        Assert.InRange(wrong[2], 228, 238);
        Assert.InRange(right[2], 252, 255);
        Assert.True(right[2] - wrong[2] >= 15, $"red only moved {right[2] - wrong[2]} levels");
    }

    private static void AssertPrimarySurvives(
        YuvColourSpace space,
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

        PackedYuvConverter.UyvyToBgra(
            packed,
            bgra,
            width: 2,
            height: 1,
            sourceStride: 4,
            destinationStride: 8,
            YuvMatrixPolicy.MatrixFor(space));

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

        PackedYuvConverter.UyvyToBgra(
            packed, bgra, Width, Height, SourceStride, DestinationStride, Bt601);

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
    // The same blind spot the YUY2 theory below was copied from, found on 2026-09-12 and fixed in
    // both: every row named a width of two, where a guard that dropped the width entirely still
    // refuses everything these rows ask about. These two rows fail the moment it does.
    [InlineData(4, 1, 4, 16)]
    [InlineData(4, 1, 8, 8)]
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
            destinationStride,
            Bt601));

    [Fact]
    public void The_pairs_are_swapped_into_the_order_Direct3D_names_for_YUY2()
    {
        // UYVY as LibVLC publishes it is U, Y0, V, Y1; YUY2 as dxgiformat.h names it is Y0, U, Y1,
        // V — «the same as the YUY2 format except the byte order is reversed», in Microsoft's own
        // words, and DXGI spells the destination out as Y0→R8, U0→G8, Y1→B8, V0→A8.
        //
        // Four values that differ from each other, because the swap has five wrong answers that a
        // repeated value hides: a straight copy, a reversal, a rotation either way, and swapping
        // the halves of the macropixel rather than its pairs.
        var packed = new byte[] { 200, 30, 100, 60 };
        var yuy2 = new byte[4];

        PackedYuvConverter.UyvyToYuy2(
            packed, yuy2, width: 2, height: 1, sourceStride: 4, destinationStride: 4);

        Assert.Equal(new byte[] { 30, 200, 60, 100 }, yuy2);
    }

    [Fact]
    public void Every_row_is_swapped_and_what_sits_past_the_picture_never_travels()
    {
        // Three strides that all differ, which is the shape this really runs in: the decoder's
        // buffer is wider than the picture, and a mapped texture's row pitch is wider again and by
        // a different amount.
        const int Width = 4;
        const int Height = 3;
        const int SourceStride = 12;
        const int DestinationStride = 10;
        var packed = new byte[SourceStride * Height];
        var yuy2 = new byte[DestinationStride * Height];
        for (var row = 0; row < Height; row++)
        {
            // A different value in every byte of the picture, so a loop that reads or writes the
            // wrong row lands on a number that belongs to another one.
            for (var at = 0; at < Width * PackedYuvConverter.SourceBytesPerPixel; at++)
            {
                packed[(row * SourceStride) + at] = (byte)(1 + (row * 20) + at);
            }

            for (var spare = Width * PackedYuvConverter.SourceBytesPerPixel; spare < SourceStride; spare++)
            {
                packed[(row * SourceStride) + spare] = 0xEE;
            }
        }

        PackedYuvConverter.UyvyToYuy2(packed, yuy2, Width, Height, SourceStride, DestinationStride);

        for (var row = 0; row < Height; row++)
        {
            for (var pair = 0; pair < Width / 2; pair++)
            {
                var read = (row * SourceStride) + (pair * 4);
                var write = (row * DestinationStride) + (pair * 4);
                Assert.Equal(packed[read + 1], yuy2[write]);
                Assert.Equal(packed[read], yuy2[write + 1]);
                Assert.Equal(packed[read + 3], yuy2[write + 2]);
                Assert.Equal(packed[read + 2], yuy2[write + 3]);
            }

            // The padding the decoder left behind is not picture, and a texture that received it
            // would show a stripe down its side.
            for (var spare = Width * PackedYuvConverter.SourceBytesPerPixel; spare < DestinationStride; spare++)
            {
                Assert.Equal(0, yuy2[(row * DestinationStride) + spare]);
            }
        }
    }

    [Fact]
    public void Swapping_twice_gives_the_original_back_because_the_swap_is_its_own_inverse()
    {
        var packed = new byte[] { 200, 30, 100, 60, 12, 240, 90, 7 };
        var once = new byte[8];
        var twice = new byte[8];

        PackedYuvConverter.UyvyToYuy2(
            packed, once, width: 4, height: 1, sourceStride: 8, destinationStride: 8);
        PackedYuvConverter.UyvyToYuy2(
            once, twice, width: 4, height: 1, sourceStride: 8, destinationStride: 8);

        Assert.Equal(packed, twice);

        // The control without which the line above is passed by a conversion that copies and does
        // nothing else: «twice gives the original back» is true of doing nothing too. And «not a
        // copy» is still not enough on its own — swapping the halves, reversing the four bytes, or
        // swapping only one of the two pairs are all their own inverse as well — so the single pass
        // is named byte by byte rather than merely declared different.
        Assert.NotEqual(packed, once);
        Assert.Equal(new byte[] { 30, 200, 60, 100, 240, 12, 7, 90 }, once);
    }

    [Theory]
    [InlineData(0, 1, 4, 4)]
    [InlineData(3, 1, 8, 8)]
    [InlineData(2, 0, 4, 4)]
    [InlineData(2, 1, 3, 4)]
    [InlineData(2, 1, 4, 3)]
    // And the same two strides at a width of four, which is what actually measures the guards. At a
    // width of two the bar is four bytes either way, so «at least one macropixel» and «at least one
    // row» are the same number and a guard that ignored the width would pass every row above. With
    // the width dropped, a stride of 4 for a 4-pixel row stops being refused and the loop writes
    // row 1's bytes on top of row 0's.
    [InlineData(4, 1, 4, 8)]
    [InlineData(4, 1, 8, 4)]
    public void A_geometry_the_swap_cannot_hold_is_refused_rather_than_written_past(
        int width,
        int height,
        int sourceStride,
        int destinationStride) =>
        Assert.ThrowsAny<ArgumentException>(() => PackedYuvConverter.UyvyToYuy2(
            new byte[16],
            new byte[16],
            width,
            height,
            sourceStride,
            destinationStride));

    [Fact]
    public void Fewer_rows_than_the_swap_was_told_to_read_is_refused()
    {
        // The two strides differ on purpose: with both at 4 this passes just as happily when the
        // guard measures the source against the destination's stride, which is a different
        // question with the same answer only while they happen to agree.
        var thrown = Assert.Throws<ArgumentException>(() => PackedYuvConverter.UyvyToYuy2(
            new byte[16],
            new byte[32],
            width: 2,
            height: 4,
            sourceStride: 8,
            destinationStride: 4));

        Assert.Equal("source", thrown.ParamName);
    }

    [Fact]
    public void Fewer_rows_than_the_swap_was_told_to_write_is_refused()
    {
        // And this one names «destination», which is the whole of its point: while both halves
        // threw the same exception with the same name, a reader could not tell which buffer was
        // short and the guard on this one could be deleted without a test noticing.
        var thrown = Assert.Throws<ArgumentException>(() => PackedYuvConverter.UyvyToYuy2(
            new byte[32],
            new byte[4],
            width: 2,
            height: 4,
            sourceStride: 4,
            destinationStride: 4));

        Assert.Equal("destination", thrown.ParamName);
    }

    [Fact]
    public void Converting_a_buffer_on_top_of_itself_is_refused_rather_than_half_done()
    {
        // Unlike the BGRA conversion, this one produces exactly as many bytes as it reads, which is
        // what tempts somebody into saving the copy. It cannot survive it: the first byte written
        // is the second byte still to be read. Measured before this guard existed — {200, 30, 100,
        // 60} came back {30, 30, 60, 60}, luma duplicated over chroma, silently.
        var buffer = new byte[] { 200, 30, 100, 60 };

        var thrown = Assert.Throws<ArgumentException>(() => PackedYuvConverter.UyvyToYuy2(
            buffer, buffer, width: 2, height: 1, sourceStride: 4, destinationStride: 4));

        Assert.Equal("destination", thrown.ParamName);
    }

    [Fact]
    public void Fewer_rows_than_the_conversion_was_told_to_read_is_refused()
    {
        var thrown = Assert.Throws<ArgumentException>(() => PackedYuvConverter.UyvyToBgra(
            new byte[4],
            new byte[64],
            width: 2,
            height: 4,
            sourceStride: 4,
            destinationStride: 8,
            Bt601));

        Assert.Equal("source", thrown.ParamName);
    }
}
