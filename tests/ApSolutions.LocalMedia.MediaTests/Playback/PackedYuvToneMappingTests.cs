// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// An HDR10 picture, carried as UYVY, comes out as the light it was graded to have on a standard
/// display — and not as the washed-out grey the plain conversion made of it.
/// </summary>
/// <remarks>
/// The expectations are computed here from SMPTE ST 2084 written out independently of the code under
/// test, for levels below the roll-off's knee, where BT.2390 leaves the light exactly as it was. A
/// test that asked <see cref="HdrToneCurve"/> for its answer would agree with any mistake in it.
/// </remarks>
public sealed class PackedYuvToneMappingTests
{
    private const double M1 = 2610d / 16384d;
    private const double M2 = 2523d / 4096d * 128d;
    private const double C1 = 3424d / 4096d;
    private const double C2 = 2413d / 4096d * 32d;
    private const double C3 = 2392d / 4096d * 32d;

    private static readonly YuvColourMatrix Bt2020 = YuvMatrixPolicy.MatrixFor(YuvColourSpace.Bt2020);
    private static readonly float[] Signal = HdrToneCurve.BuildSignalTable(null);
    private static readonly byte[] Encode = HdrToneCurve.BuildEncodeTable();

    /// <summary>
    /// Greys below the knee: the luma level carries a PQ signal, the signal carries nits, and a
    /// standard display shows those nits as a fraction of its 203-nit white, encoded for gamma 2.4.
    /// </summary>
    [Theory]
    [InlineData(40)]
    [InlineData(70)]
    [InlineData(114)]
    [InlineData(125)]
    public void A_grey_below_the_knee_keeps_its_light(byte luma)
    {
        var signal = (luma - 16) / 219d;
        var p = Math.Pow(signal, 1d / M2);
        var nits = 10000d * Math.Pow(Math.Max(p - C1, 0d) / (C2 - (C3 * p)), 1d / M1);
        var expected = (int)Math.Round(255d * Math.Pow(nits / 203d, 1d / 2.4d));

        var pixel = Convert(128, luma, 128);

        Assert.InRange(pixel[2], expected - 2, expected + 2);
        Assert.InRange(pixel[1], expected - 2, expected + 2);
        Assert.InRange(pixel[0], expected - 2, expected + 2);
        Assert.Equal(255, pixel[3]);
    }

    [Fact]
    public void Black_is_black_and_the_film_s_peak_is_white()
    {
        Assert.Equal([0, 0, 0, 255], Convert(128, 16, 128));

        // 1000 nits is PQ 0.7518, luma 16 + 219 × 0.7518.
        var peak = Convert(128, 181, 128);
        Assert.InRange(peak[0], 253, 255);
        Assert.InRange(peak[1], 253, 255);
        Assert.InRange(peak[2], 253, 255);
    }

    /// <summary>
    /// The defect this conversion replaces, measured: a fifty-nit grey read with the plain conversion
    /// comes out as the PQ signal itself, darker than it was graded — and a highlight comes out
    /// brighter, which is the flat, grey picture an HDR10 film showed.
    /// </summary>
    [Fact]
    public void The_plain_conversion_is_what_the_film_looked_like_before()
    {
        var plain = new byte[8];
        PackedYuvConverter.UyvyToBgra([128, 114, 128, 114], plain, 2, 1, 4, 8, Bt2020);

        var mapped = Convert(128, 114, 128);

        Assert.True(mapped[1] - plain[1] >= 15, $"the plain grey was {plain[1]} and the mapped one {mapped[1]}");
    }

    /// <summary>
    /// A BT.2020 red lies outside what BT.709 can show; after the change of primaries it is still red,
    /// with nothing leaking into the other two channels.
    /// </summary>
    [Fact]
    public void A_BT2020_red_stays_red()
    {
        // PQ 0.449 on red alone: Y' = 0.2627 × 0.449, Cb = −Y'/1.8814, Cr = (0.449 − Y')/1.4746.
        var pixel = Convert(114, 42, 178);

        Assert.True(pixel[2] > 100, $"red came out {pixel[2]}");
        Assert.True(pixel[1] < 20, $"green came out {pixel[1]}");
        Assert.True(pixel[0] < 20, $"blue came out {pixel[0]}");
    }

    /// <summary>The person's own curve still applies, to the incoming luma as it does on the standard path.</summary>
    [Fact]
    public void The_picture_adjustment_reaches_the_HDR_path()
    {
        var toBlack = Enumerable.Repeat(16 << PictureAdjustment.FractionBits, 256).ToArray();
        var bgra = new byte[8];

        PackedYuvConverter.UyvyToBgraToneMapped([128, 125, 128, 125], bgra, 2, 1, 4, 8, Bt2020, Signal, Encode, toBlack);

        Assert.Equal([0, 0, 0, 255, 0, 0, 0, 255], bgra);
    }

    [Fact]
    public void Tables_of_the_wrong_size_are_refused()
    {
        var bgra = new byte[8];

        var shortSignal = Assert.Throws<ArgumentException>(() => PackedYuvConverter.UyvyToBgraToneMapped(
            [128, 125, 128, 125], bgra, 2, 1, 4, 8, Bt2020, new float[256], Encode));
        var shortEncode = Assert.Throws<ArgumentException>(() => PackedYuvConverter.UyvyToBgraToneMapped(
            [128, 125, 128, 125], bgra, 2, 1, 4, 8, Bt2020, Signal, new byte[256]));

        Assert.Equal("signal", shortSignal.ParamName);
        Assert.Equal("signal", shortEncode.ParamName);
    }

    [Fact]
    public void The_shared_guards_hold_on_the_HDR_path_too()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PackedYuvConverter.UyvyToBgraToneMapped(
            new byte[4], new byte[8], 3, 1, 4, 8, Bt2020, Signal, Encode));
        Assert.Throws<ArgumentException>(() => PackedYuvConverter.UyvyToBgraToneMapped(
            new byte[4], new byte[8], 2, 2, 4, 8, Bt2020, Signal, Encode));
    }

    /// <summary>Every row, and the padding of a wider stride left as it was.</summary>
    [Fact]
    public void Every_row_is_converted_and_the_padding_is_left_alone()
    {
        var packed = new byte[] { 128, 114, 128, 114, 9, 9, 128, 16, 128, 16, 9, 9 };
        var bgra = Enumerable.Repeat((byte)7, 24).ToArray();

        PackedYuvConverter.UyvyToBgraToneMapped(packed, bgra, 2, 2, 6, 12, Bt2020, Signal, Encode);

        Assert.True(bgra[1] > 100);
        Assert.Equal([7, 7, 7, 7], bgra[8..12]);
        Assert.Equal([0, 0, 0, 255], bgra[12..16]);
        Assert.Equal([7, 7, 7, 7], bgra[20..24]);
    }

    private static byte[] Convert(byte u, byte luma, byte v)
    {
        var bgra = new byte[8];
        PackedYuvConverter.UyvyToBgraToneMapped([u, luma, v, luma], bgra, 2, 1, 4, 8, Bt2020, Signal, Encode);
        Assert.Equal(bgra[..4], bgra[4..]);
        return bgra[..4];
    }
}
