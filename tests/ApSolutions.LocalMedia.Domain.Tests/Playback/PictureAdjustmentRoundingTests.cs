// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// That the tone curve paints the level it asked for, to the nearest one it can.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file is what is left of an attempt that went wrong, and both halves are worth keeping.</b>
/// On 2026-09-13 the owner raised gamma to 1.5 and saw flat patches around lettering. The cause was
/// real and it is quantisation: a curve maps 256 levels onto 256 levels, so wherever it is flatter than
/// one-to-one two neighbouring inputs land on the same output and a gradient loses a step.
/// </para>
/// <para>
/// <b>The textbook answer is dithering, it was built, and he rejected it in twenty minutes</b> —
/// «ahora aparecen un montón de cuadraditos en toda la imagen». Measured afterwards, the reason is
/// ordering and not amplitude: the sharpening does <i>not</i> amplify it, the pattern stays one level
/// wide. But the pattern lives in the resolution of the decoded frame and the screen is four times
/// that, so an 8×8 cell becomes a 32×32 block of screen pixels — one level spread over an area large
/// enough to read as banding of its own. <b>Dither has to be the last step before the screen and there
/// it was the first.</b> `ENG-023` carries that ordering; it needs the curve to move behind the
/// scaling, which is a different piece of work.
/// </para>
/// <para>
/// <b>What survived is the rounding, which was its own defect.</b> The old table truncated, so a curve
/// asking for 100,9 painted 100 — and a control whose shift is not a whole number of levels left the
/// whole picture half a level dark. That is fixed, measured here, and it introduces no pattern at all
/// because every pixel of a given level gets the same answer.
/// </para>
/// </remarks>
public sealed class PictureAdjustmentRoundingTests
{
    /// <summary>
    /// Every level is painted within half a level of what the curve asked for.
    /// </summary>
    /// <remarks>
    /// Half a level is the whole claim: it is what «nearest» means, and it is the most a single
    /// eight-bit value can promise. The rows cover the three controls because the defect belonged to
    /// all three — the owner asked on 2026-09-13 whether brightness had it too, and it did, in its own
    /// form: 0.1 is 25,5 levels, so truncation put the entire picture half a level down.
    /// </remarks>
    [Theory]
    [InlineData(0d, 1d, 1.5d)]
    [InlineData(0d, 1d, 2.2d)]
    [InlineData(0d, 1d, 0.6d)]
    [InlineData(0d, 1.5d, 1d)]
    [InlineData(0d, 0.7d, 1d)]
    [InlineData(0.1d, 1d, 1d)]
    [InlineData(-0.3d, 1d, 1d)]
    [InlineData(0.1d, 1.3d, 1.5d)]
    public void Every_level_is_painted_within_half_a_level_of_what_the_curve_asked_for(
        double brightness,
        double contrast,
        double gamma)
    {
        var adjustment = new PictureAdjustment(brightness, contrast, gamma);
        var curve = adjustment.BuildLookup();

        var worst = 0d;
        var worstLevel = -1;
        for (var level = 0; level < 256; level++)
        {
            var painted = PictureAdjustment.Quantise(curve[level]);
            var asked = adjustment.ExactLevel(level);
            if (Math.Abs(painted - asked) > worst)
            {
                worst = Math.Abs(painted - asked);
                worstLevel = level;
            }
        }

        // Half a level plus a hair, and the hair is not slack: a curve can ask for exactly the midpoint
        // between two levels — brightness 0.1 does it at level 130, measured — and then half a level
        // of error is the correct answer rather than a defect. The hair is there because 0.1 is not
        // exact in binary, so that tie lands on 0,5000000000000284 instead of 0,5. The control below
        // fails by nearly a whole level, so nothing rides on this tolerance.
        Assert.True(
            worst <= 0.5 + 1e-9,
            $"At brightness {brightness}, contrast {contrast}, gamma {gamma}, level {worstLevel} is "
                + $"painted {worst:F4} levels away from what the curve asked for.");
    }

    /// <summary>
    /// The instrument control: truncating instead of rounding has to fail the same measurement.
    /// </summary>
    /// <remarks>
    /// Otherwise the test above passes because the curve happens to land on whole numbers, not because
    /// anything rounds — and a gate that would pass with the mechanism removed is measuring the scene.
    /// The three rows are the three shapes of the defect: a bent curve, a pivoted one, and a shifted
    /// one.
    /// </remarks>
    [Theory]
    [InlineData(0d, 1d, 1.5d)]
    [InlineData(0d, 1.5d, 1d)]
    [InlineData(0.1d, 1d, 1d)]
    public void Truncating_instead_of_rounding_is_what_put_the_picture_half_a_level_down(
        double brightness,
        double contrast,
        double gamma)
    {
        var adjustment = new PictureAdjustment(brightness, contrast, gamma);
        var curve = adjustment.BuildLookup();

        var worst = 0d;
        for (var level = 0; level < 256; level++)
        {
            var truncated = curve[level] >> PictureAdjustment.FractionBits;
            worst = Math.Max(worst, Math.Abs(truncated - adjustment.ExactLevel(level)));
        }

        Assert.True(
            worst > 0.5,
            $"Truncated, at brightness {brightness} contrast {contrast} gamma {gamma}, the worst level "
                + $"is only {worst:F2} away from the curve — so this scene cannot show the defect the "
                + "test above guards and that test is passing on the scene rather than on the rounding.");
    }

    /// <summary>
    /// Neutral is the identity exactly, which is what PLY-018 promises by name.
    /// </summary>
    /// <remarks>
    /// <b>This is the one the change could have broken, and the reason the fixed point is scaled by
    /// 255.</b> FFmpeg's table reaches the identity by truncating <c>256 × v</c>, which is a scale of
    /// 256/255 papering over the rounding; rounded, that same table would send level 254 to 255. At
    /// <c>255 × v</c> the neutral curve lands on exact whole numbers and rounding cannot move it.
    /// </remarks>
    [Fact]
    public void Neutral_is_the_identity_after_rounding_and_not_merely_near_it()
    {
        var curve = PictureAdjustment.Neutral.BuildLookup();

        for (var level = 0; level < 256; level++)
        {
            Assert.Equal(level << PictureAdjustment.FractionBits, curve[level]);
            Assert.Equal(level, PictureAdjustment.Quantise(curve[level]));
        }
    }

    /// <summary>
    /// A level of the curve carries a fraction, which is the point of the fixed point.
    /// </summary>
    /// <remarks>
    /// Asserted because a table that happened to hold whole levels shifted up would satisfy every test
    /// above and carry no fraction at all — the rounding would then be rounding nothing, and the defect
    /// would be back with every gate green. So this names a level whose curve value is measured to lie
    /// between two whole ones.
    /// </remarks>
    [Fact]
    public void The_curve_keeps_a_fraction_of_a_level_and_does_not_merely_shift_whole_ones()
    {
        var curve = new PictureAdjustment(Brightness: 0d, Contrast: 1d, Gamma: 1.5d).BuildLookup();

        var fractional = 0;
        for (var level = 0; level < 256; level++)
        {
            if ((curve[level] & (PictureAdjustment.HalfLevel * 2 - 1)) != 0)
            {
                fractional++;
            }
        }

        Assert.True(
            fractional > 200,
            $"Only {fractional} of 256 levels carry a fraction, so the table is whole levels wearing a "
                + "shift and the rounding above has nothing to round.");
    }
}
