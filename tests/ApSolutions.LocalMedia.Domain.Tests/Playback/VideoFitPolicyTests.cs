// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// The rule the player draws by. What is asserted is the <b>shape</b>, not the numbers: a box whose
/// ratio differs from the picture's is a stretched picture whatever its size, and that is the defect
/// the owner reported on 2026-08-25 — an episode drawn into a resized window came out taller.
/// </summary>
public sealed class VideoFitPolicyTests
{
    [Theory]
    // Wider surface than picture: the bars go at the sides.
    [InlineData(1920d, 1080d, 1000d, 400d)]
    // Taller surface than picture: the bars go above and below.
    [InlineData(1920d, 1080d, 800d, 900d)]
    // A picture taller than it is wide, which a phone recording is.
    [InlineData(1080d, 1920d, 1000d, 400d)]
    // Exactly the same shape: no bars at all, and still no stretch.
    [InlineData(1600d, 900d, 800d, 450d)]
    public void The_box_always_keeps_the_shape_the_picture_was_decoded_with(
        double frameWidth,
        double frameHeight,
        double surfaceWidth,
        double surfaceHeight)
    {
        var box = VideoFitPolicy.Fit(frameWidth, frameHeight, surfaceWidth, surfaceHeight);

        Assert.Equal(frameWidth / frameHeight, box.Width / box.Height, precision: 9);
        Assert.True(box.Width <= surfaceWidth + 1e-9, "The box never overflows the surface's width.");
        Assert.True(box.Height <= surfaceHeight + 1e-9, "The box never overflows the surface's height.");
    }

    [Fact]
    public void The_bars_are_shared_rather_than_pushed_to_one_side()
    {
        var wide = VideoFitPolicy.Fit(1920, 1080, 1000, 400);
        var tall = VideoFitPolicy.Fit(1920, 1080, 800, 900);

        Assert.Equal(1000 - wide.Width, wide.X * 2, precision: 9);
        Assert.Equal(900 - tall.Height, tall.Y * 2, precision: 9);
        Assert.True(wide.IsPillarboxed);
        Assert.False(wide.IsLetterboxed);
        Assert.True(tall.IsLetterboxed);
        Assert.False(tall.IsPillarboxed);
    }

    [Fact]
    public void A_surface_of_the_same_shape_leaves_no_bars()
    {
        var box = VideoFitPolicy.Fit(1600, 900, 800, 450);

        Assert.False(box.IsLetterboxed);
        Assert.False(box.IsPillarboxed);
        Assert.Equal(800, box.Width, precision: 9);
        Assert.Equal(450, box.Height, precision: 9);
    }

    [Theory]
    [InlineData(0d, 1080d, 800d, 600d)]
    [InlineData(1920d, 0d, 800d, 600d)]
    [InlineData(1920d, 1080d, 0d, 600d)]
    [InlineData(1920d, 1080d, 800d, 0d)]
    [InlineData(-1920d, 1080d, 800d, 600d)]
    [InlineData(1920d, -1080d, 800d, 600d)]
    [InlineData(1920d, 1080d, -800d, 600d)]
    [InlineData(1920d, 1080d, 800d, -600d)]
    public void A_degenerate_side_yields_an_empty_box_rather_than_a_division_by_zero(
        double frameWidth,
        double frameHeight,
        double surfaceWidth,
        double surfaceHeight)
    {
        var box = VideoFitPolicy.Fit(frameWidth, frameHeight, surfaceWidth, surfaceHeight);

        Assert.Equal(default, box);
        Assert.Equal(0, box.Width);
        Assert.Equal(0, box.Height);
        Assert.False(box.IsLetterboxed);
        Assert.False(box.IsPillarboxed);
    }
}
