// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// PLY-016 decides here, and only here. The expensive half of the feature talks to a graphics card;
/// this half runs anywhere, so it is the half that is asserted — which picture is worth enlarging,
/// and to exactly what size.
/// </summary>
/// <remarks>
/// The target is <b>the box the picture occupies on screen</b>, never the screen. Enlarging a 720p
/// episode to 4K while the window is 1280 wide spends a graphics card to throw the result away, and
/// aiming at the surface instead of the box is the shape that defect takes.
/// </remarks>
public sealed class UpscalePolicyTests
{
    /// <summary>The case the feature exists for: 720p filling a 4K display.</summary>
    [Fact]
    public void A_picture_smaller_than_its_box_is_enlarged_to_that_box()
    {
        var decision = UpscalePolicy.Decide(1280, 720, 3840, 2160);

        Assert.True(decision.ShouldUpscale);
        Assert.Equal(3840, decision.TargetWidth);
        Assert.Equal(2160, decision.TargetHeight);
    }

    /// <summary>
    /// The control that has to stay silent. A picture already as large as the space it is drawn in
    /// has nothing to gain, and a policy that enlarged it anyway would light the indicator over
    /// every 4K file.
    /// </summary>
    [Theory]
    // Exactly the size of its box.
    [InlineData(3840, 2160, 3840, 2160)]
    // Larger than its box: shrinking is the renderer's job, not this one's.
    [InlineData(3840, 2160, 1920, 1080)]
    // One axis short is not enough when the box is reached on the other; the shape decides.
    [InlineData(1920, 1080, 1920, 2160)]
    public void A_picture_that_already_fills_its_box_is_left_alone(
        int sourceWidth,
        int sourceHeight,
        int surfaceWidth,
        int surfaceHeight)
    {
        var decision = UpscalePolicy.Decide(sourceWidth, sourceHeight, surfaceWidth, surfaceHeight);

        Assert.False(decision.ShouldUpscale);
    }

    /// <summary>
    /// The one that catches aiming at the surface. A 4:3 picture on a 16:9 display is drawn in a
    /// box narrower than the display, and enlarging to the display's width would stretch it.
    /// </summary>
    [Fact]
    public void The_target_is_the_box_and_never_the_surface_around_it()
    {
        var decision = UpscalePolicy.Decide(640, 480, 3840, 2160);

        Assert.True(decision.ShouldUpscale);
        Assert.Equal(2880, decision.TargetWidth);
        Assert.Equal(2160, decision.TargetHeight);
    }

    /// <summary>
    /// The exact target of every shape, and exact on purpose.
    /// </summary>
    /// <remarks>
    /// This used to allow a ratio within 0.01, which sounded careful and was twelve times looser than
    /// the one pixel of rounding it claimed to allow — a pixel on 2160 is 0.0008. Measured on
    /// 2026-09-12: snapping the width to a 16-pixel grid turns a PAL episode's 1800×1440 into
    /// 1792×1440 — eight pixels of visible squashing — and passed the band with room to spare. A
    /// shape is preserved by construction or it is not, so the number is asserted.
    /// </remarks>
    [Theory]
    [InlineData(1280, 720, 3840, 2160, 3840, 2160)]
    [InlineData(640, 480, 3840, 2160, 2880, 2160)]
    [InlineData(720, 576, 2560, 1440, 1800, 1440)]
    [InlineData(1080, 1920, 3840, 2160, 1215, 2160)]
    public void The_target_is_exact_and_keeps_the_shape_the_picture_was_decoded_with(
        int sourceWidth,
        int sourceHeight,
        int surfaceWidth,
        int surfaceHeight,
        int expectedWidth,
        int expectedHeight)
    {
        var decision = UpscalePolicy.Decide(sourceWidth, sourceHeight, surfaceWidth, surfaceHeight);

        Assert.True(decision.ShouldUpscale);
        Assert.Equal(expectedWidth, decision.TargetWidth);
        Assert.Equal(expectedHeight, decision.TargetHeight);
    }

    /// <summary>
    /// The half-pixel box, which is the only case that exercises the rounding at all.
    /// </summary>
    /// <remarks>
    /// Every other case here lands on whole pixels, so replacing both <c>Math.Round</c> calls with
    /// truncation left the suite green — measured 2026-09-12. A 1288-wide surface gives a box of
    /// exactly 1288×724.5: rounding away from zero answers 725, truncation 724, and banker's
    /// rounding 724.
    /// </remarks>
    [Fact]
    public void A_box_that_lands_on_half_a_pixel_rounds_away_from_zero()
    {
        var decision = UpscalePolicy.Decide(1280, 720, 1288, 2160);

        Assert.True(decision.ShouldUpscale);
        Assert.Equal(1288, decision.TargetWidth);
        Assert.Equal(725, decision.TargetHeight);
    }

    /// <summary>
    /// Nothing about a degenerate size is measurable, so nothing is claimed. A window dragged shut
    /// reports a zero here before it reports anything else.
    /// </summary>
    [Theory]
    [InlineData(0, 720, 3840, 2160)]
    [InlineData(1280, 0, 3840, 2160)]
    [InlineData(1280, 720, 0, 2160)]
    [InlineData(1280, 720, 3840, 0)]
    [InlineData(-1280, 720, 3840, 2160)]
    public void A_degenerate_size_enlarges_nothing(
        int sourceWidth,
        int sourceHeight,
        int surfaceWidth,
        int surfaceHeight)
    {
        var decision = UpscalePolicy.Decide(sourceWidth, sourceHeight, surfaceWidth, surfaceHeight);

        Assert.False(decision.ShouldUpscale);
        Assert.Equal(0, decision.TargetWidth);
        Assert.Equal(0, decision.TargetHeight);
    }
}
