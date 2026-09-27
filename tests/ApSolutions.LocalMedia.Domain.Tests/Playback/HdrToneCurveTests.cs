// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Playback;

using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// The curve that brings an HDR10 picture down to what a standard display shows.
/// </summary>
/// <remarks>
/// The reference values are written out here from the standards and not taken from the code under
/// test: SMPTE ST 2084 for the PQ curve, Rec. ITU-R BT.2390 for the roll-off, BT.2087 for the
/// change of primaries. A test that computed its expectation with the same functions would agree
/// with any mistake in them.
/// </remarks>
public sealed class HdrToneCurveTests
{
    // SMPTE ST 2084, written independently of HdrToneCurve.
    private const double M1 = 2610d / 16384d;
    private const double M2 = 2523d / 4096d * 128d;
    private const double C1 = 3424d / 4096d;
    private const double C2 = 2413d / 4096d * 32d;
    private const double C3 = 2392d / 4096d * 32d;

    private static double ReferencePq(double nits)
    {
        var y = Math.Pow(nits / 10000d, M1);
        return Math.Pow((C1 + (C2 * y)) / (1d + (C3 * y)), M2);
    }

    [Theory]
    [InlineData(100d, 0.5081)]
    [InlineData(1000d, 0.7518)]
    [InlineData(10000d, 1d)]
    public void The_PQ_curve_matches_the_published_code_values(double nits, double expected)
    {
        Assert.Equal(expected, HdrToneCurve.NitsToPq(nits), 4);
        Assert.Equal(nits, HdrToneCurve.PqToNits(HdrToneCurve.NitsToPq(nits)), 6);
    }

    [Fact]
    public void Black_stays_black()
    {
        Assert.Equal(0d, HdrToneCurve.PqToNits(0d), 6);
        Assert.Equal(0d, HdrToneCurve.Map(0d, HdrToneCurve.DefaultSourcePeakNits), 6);
    }

    /// <summary>
    /// Below the knee the curve changes nothing: a shadow of fifty nits is still fifty nits, now a
    /// quarter of the standard display's white.
    /// </summary>
    [Fact]
    public void Below_the_knee_the_light_is_kept_as_it_was()
    {
        var mapped = HdrToneCurve.Map(ReferencePq(50d), HdrToneCurve.DefaultSourcePeakNits);

        Assert.Equal(50d / HdrToneCurve.SdrPeakNits, mapped, 4);
    }

    /// <summary>
    /// The brightest the film can be lands on the display's white, and nothing brighter is cut
    /// short of it or pushed past it.
    /// </summary>
    [Fact]
    public void The_source_peak_lands_on_white_and_anything_above_it_stays_there()
    {
        Assert.Equal(1d, HdrToneCurve.Map(ReferencePq(1000d), 1000d), 3);
        Assert.Equal(1d, HdrToneCurve.Map(ReferencePq(4000d), 1000d), 3);
    }

    /// <summary>
    /// The roll-off compresses highlights instead of clipping them: between the knee and the peak
    /// the light still rises, only more slowly than it did.
    /// </summary>
    [Fact]
    public void Highlights_are_compressed_and_never_clipped_or_reversed()
    {
        var table = HdrToneCurve.BuildSignalTable(null);

        Assert.Equal(HdrToneCurve.SignalLevels, table.Length);
        for (var level = 1; level < table.Length; level++)
        {
            Assert.True(table[level] >= table[level - 1], $"The curve falls at level {level}.");
        }

        var sixHundred = HdrToneCurve.Map(ReferencePq(600d), 1000d);
        Assert.True(sixHundred is > 0.9 and < 1d, $"600 nits mapped to {sixHundred}, which is clipped or not compressed.");
        Assert.True(sixHundred < 600d / HdrToneCurve.SdrPeakNits);
    }

    [Theory]
    [InlineData(null, 1000)]
    [InlineData(4000, 4000)]
    [InlineData(150, 1000)]
    [InlineData(20000, 1000)]
    public void The_film_s_own_peak_is_used_when_it_declares_a_believable_one(int? declared, int expected)
    {
        Assert.Equal(expected, HdrToneCurve.SourcePeakNits(declared));
    }

    /// <summary>A brighter declared peak leaves more room: the same level comes out darker.</summary>
    [Fact]
    public void The_declared_peak_reaches_the_table()
    {
        var level = (int)Math.Round(ReferencePq(800d) * (HdrToneCurve.SignalLevels - 1));

        Assert.True(HdrToneCurve.BuildSignalTable(4000)[level] < HdrToneCurve.BuildSignalTable(1000)[level]);
    }

    [Fact]
    public void The_encoding_is_the_inverse_of_the_display_s_gamma_of_2_4()
    {
        var table = HdrToneCurve.BuildEncodeTable();

        Assert.Equal(HdrToneCurve.EncodeLevels, table.Length);
        Assert.Equal(0, table[0]);
        Assert.Equal(255, table[^1]);
        Assert.Equal(191, table[(HdrToneCurve.EncodeLevels - 1) / 2]);
    }

    /// <summary>
    /// The change of primaries keeps white white: each row of BT.2087's matrix adds up to one.
    /// </summary>
    [Fact]
    public void The_change_of_primaries_keeps_white_white()
    {
        var m = HdrToneCurve.Bt2020ToBt709;

        Assert.Equal(9, m.Count);
        for (var row = 0; row < 3; row++)
        {
            Assert.Equal(1d, m[row * 3] + m[(row * 3) + 1] + m[(row * 3) + 2], 3);
        }

        Assert.Equal(1.6605, m[0], 3);
        Assert.Equal(1.1187, m[8], 3);
    }

    [Theory]
    [InlineData(16, HdrFormat.Hdr10)]
    [InlineData(18, HdrFormat.None)]
    [InlineData(1, HdrFormat.None)]
    [InlineData(null, HdrFormat.None)]
    public void Only_the_PQ_curve_is_HDR10(int? transfer, HdrFormat expected)
    {
        Assert.Equal(expected, new SourceColour(9, transfer, 9, null).Hdr);
    }
}
