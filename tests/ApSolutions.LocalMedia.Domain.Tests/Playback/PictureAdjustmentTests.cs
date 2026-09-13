// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// The arithmetic behind the picture controls. What matters is that the neutral setting is exactly
/// nothing — not nearly nothing — and that raising gamma lifts what is crushed into black without
/// touching what is already bright.
/// </summary>
public sealed class PictureAdjustmentTests
{
    [Fact]
    public void The_neutral_setting_builds_the_identity_so_leaving_it_alone_changes_no_byte()
    {
        // The acceptance criterion of PLY-018, and the only one a person can check by looking: with
        // the controls untouched the picture has to be the picture. «Almost identity» would drift
        // every frame through a conversion that runs on every pixel of every frame.
        var table = PictureAdjustment.Neutral.BuildLookup();

        Assert.Equal(256, table.Length);
        for (var value = 0; value < 256; value++)
        {
            Assert.Equal((byte)value, table[value]);
        }
    }

    [Fact]
    public void Raising_gamma_lifts_the_shadows_and_leaves_both_ends_where_they_are()
    {
        // 1.6 is the value measured against a real dark episode on 2026-09-12 and judged by eye:
        // the curtains and the armour come back, and the picture does not wash out.
        var table = new PictureAdjustment(Brightness: 0d, Contrast: 1d, Gamma: 1.6d).BuildLookup();

        // Level 0 stays 0 and level 255 stays 255, which a gamma curve pins by construction.
        //
        // <b>And that is NOT «black stays black», which is what this comment claimed until an audit
        // measured it.</b> A limited-range picture never contains level 0: its black is 16, and this
        // curve sends it to 45 — which the conversion then turns into 34 on screen, because it takes
        // the 16 back off and applies the range gain. So the letterbox bars DO lift towards grey.
        // It is what FFmpeg's eq does and what the owner looked at and approved, so it is the
        // behaviour and not a defect; but a comment denying it would have the next reader trust a
        // curve that pins an end no video ever reaches.
        Assert.Equal(0, table[0]);
        Assert.Equal(255, table[255]);
        Assert.InRange(table[16], 42, 48);

        // And everything in between goes up, which is the whole point.
        Assert.True(table[32] > 32 + 20, $"deep shadow only reached {table[32]}");
        Assert.True(table[64] > 64 + 25, $"shadow only reached {table[64]}");
        Assert.True(table[128] > 128 + 25, $"midtone only reached {table[128]}");

        // Monotonic, because a curve that crosses itself inverts detail somewhere.
        for (var value = 1; value < 256; value++)
        {
            Assert.True(
                table[value] >= table[value - 1],
                $"the curve went backwards at {value}: {table[value - 1]} then {table[value]}");
        }
    }

    [Fact]
    public void Lowering_gamma_darkens_instead_of_lifting()
    {
        // The negative control. Without it, «raising gamma lifts» is passed by a table that lifts
        // whatever it is given.
        var table = new PictureAdjustment(Brightness: 0d, Contrast: 1d, Gamma: 0.6d).BuildLookup();

        Assert.True(table[128] < 128, $"the midtone rose to {table[128]} with gamma below one");
        Assert.Equal(0, table[0]);
        Assert.Equal(255, table[255]);
    }

    [Theory]
    // Brightness moves every level the same way; contrast pivots around the middle.
    [InlineData(0.2d, 1d, 1d, 128, 179)]
    [InlineData(-0.2d, 1d, 1d, 128, 76)]
    // Contrast at 1.5 pushes the midtone nowhere — it IS the pivot — and pulls the ends apart.
    [InlineData(0d, 1.5d, 1d, 128, 128)]
    [InlineData(0d, 1.5d, 1d, 64, 32)]
    [InlineData(0d, 1.5d, 1d, 192, 223)]
    // Both dials at once, and this row is the only thing in the tree that pins the ORDER of the two.
    // Every row above moves one dial, and with one dial moving, «contrast times (level minus half)
    // plus brightness» and «contrast times (level minus half plus brightness)» agree everywhere.
    // They do not agree here: this lands 129 the right way round and 1 the wrong way, and the wrong
    // way sends white to half light and greys the letterbox bars. It is FFmpeg's order, which is the
    // whole reason the arithmetic was copied rather than invented.
    [InlineData(-0.5d, 2d, 1d, 192, 129)]
    public void Brightness_shifts_and_contrast_pivots_around_the_middle(
        double brightness,
        double contrast,
        double gamma,
        int input,
        int expected)
    {
        var table = new PictureAdjustment(brightness, contrast, gamma).BuildLookup();

        Assert.InRange(table[input], expected - 2, expected + 2);
    }

    [Theory]
    // Settings that push the bottom of the range below black, which the arithmetic has to pin
    // rather than carry on with: Math.Pow of a negative base answers NaN for any gamma that is not
    // 1, and a NaN cast to byte is 0 with no error anywhere — the shadows would come out as
    // whatever the conversion happened to leave behind, differently on another runtime.
    [InlineData(-0.5d, 2d, 1d)]
    [InlineData(-0.5d, 2d, 1.6d)]
    [InlineData(-0.9d, 1d, 1d)]
    public void Levels_pushed_below_black_are_pinned_there_and_the_curve_still_climbs(
        double brightness,
        double contrast,
        double gamma)
    {
        var table = new PictureAdjustment(brightness, contrast, gamma).BuildLookup();

        Assert.Equal(0, table[0]);

        // Monotonic all the way, which is what a wrapped-around cast breaks: a byte that came from
        // a negative double is any value at all, and one bright pixel in the middle of the shadows
        // is what it looks like on screen.
        for (var value = 1; value < 256; value++)
        {
            Assert.True(
                table[value] >= table[value - 1],
                $"the curve went backwards at {value}: {table[value - 1]} then {table[value]}");
        }

        // And the table still tells black from white, or one of all zeroes would pass the two
        // above. It is deliberately not «the top is still bright»: at −0.9 the whole picture IS
        // nearly black and level 255 lands at 25, which is the setting working, not failing.
        Assert.True(
            table[255] > table[0],
            $"the table stopped telling black from white: {table[0]} and {table[255]}");
    }

    [Theory]
    [InlineData(-1.1d, 1d, 1d)]
    [InlineData(1.1d, 1d, 1d)]
    [InlineData(0d, 0.4d, 1d)]
    [InlineData(0d, 2.1d, 1d)]
    [InlineData(0d, 1d, 0.4d)]
    [InlineData(0d, 1d, 3.1d)]
    [InlineData(0d, 1d, 0d)]
    [InlineData(double.NaN, 1d, 1d)]
    [InlineData(0d, double.NaN, 1d)]
    [InlineData(0d, 1d, double.NaN)]
    public void A_setting_outside_what_a_person_can_ask_for_is_refused_rather_than_clamped(
        double brightness,
        double contrast,
        double gamma) =>
        Assert.ThrowsAny<ArgumentException>(() => new PictureAdjustment(brightness, contrast, gamma));

    [Fact]
    public void The_neutral_setting_knows_it_is_neutral_and_a_touched_one_knows_it_is_not()
    {
        // What lets the conversion skip the table entirely, so the default costs nothing at all.
        Assert.True(PictureAdjustment.Neutral.IsNeutral);
        Assert.False(new PictureAdjustment(0d, 1d, 1.6d).IsNeutral);
        Assert.False(new PictureAdjustment(0.1d, 1d, 1d).IsNeutral);
        Assert.False(new PictureAdjustment(0d, 1.2d, 1d).IsNeutral);
    }
}
