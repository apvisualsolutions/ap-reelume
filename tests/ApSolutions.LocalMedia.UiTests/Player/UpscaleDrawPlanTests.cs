// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia;
using SkiaSharp;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The decisions PLY-016's drawing takes, asserted away from the canvas that carries them out.
/// </summary>
/// <remarks>
/// This is the half of <see cref="SkiaUpscaleDrawOperation"/> that runs anywhere, split out under
/// this repository's tenth rule so that the machine half can be excluded from coverage without
/// excluding anything that decides. What is asserted here is what a person would see going wrong: a
/// frame drawn by the wrong route, or a sharpening pass that samples inside one source pixel and
/// therefore does nothing.
/// </remarks>
public sealed class UpscaleDrawPlanTests
{
    /// <summary>
    /// Which route each link takes, and the two states that take it down a step.
    /// </summary>
    [Theory]
    // The portable link sharpens when its shader is there and falls to cubic when it is not.
    [InlineData(UpscaleLink.PortableUpscaler, true, true, UpscaleDrawRoute.Sharpened)]
    [InlineData(UpscaleLink.PortableUpscaler, true, false, UpscaleDrawRoute.Cubic)]
    // The cubic link never asks for a shader, so its answer does not depend on one.
    [InlineData(UpscaleLink.BicubicResample, true, true, UpscaleDrawRoute.Cubic)]
    [InlineData(UpscaleLink.BicubicResample, true, false, UpscaleDrawRoute.Cubic)]
    // No Skia canvas means neither link can be drawn, whatever the shader says.
    [InlineData(UpscaleLink.PortableUpscaler, false, true, UpscaleDrawRoute.Unenhanced)]
    [InlineData(UpscaleLink.BicubicResample, false, true, UpscaleDrawRoute.Unenhanced)]
    // And the three links that never come through here.
    [InlineData(UpscaleLink.CompositionBilinear, true, true, UpscaleDrawRoute.Unenhanced)]
    [InlineData(UpscaleLink.VideoProcessorEnhancement, true, true, UpscaleDrawRoute.Unenhanced)]
    [InlineData(UpscaleLink.VendorSuperResolution, true, true, UpscaleDrawRoute.Unenhanced)]
    public void Each_link_takes_the_route_the_canvas_can_actually_draw(
        UpscaleLink link,
        bool canvas,
        bool shader,
        UpscaleDrawRoute expected)
    {
        Assert.Equal(expected, UpscaleDrawPlan.Route(link, canvas, shader, 1280, 720));
    }

    /// <summary>
    /// A degenerate source size is answered before anything divides by it.
    /// </summary>
    /// <remarks>
    /// Not tidiness: every route below the guard divides the destination by the source to find where
    /// one source pixel lands, and a zero there is an infinity handed to a shader.
    /// </remarks>
    [Theory]
    [InlineData(0, 720)]
    [InlineData(1280, 0)]
    [InlineData(-1280, 720)]
    [InlineData(1280, -720)]
    public void A_source_with_no_size_is_drawn_unenhanced_before_anything_divides_by_it(
        int sourceWidth,
        int sourceHeight)
    {
        Assert.Equal(
            UpscaleDrawRoute.Unenhanced,
            UpscaleDrawPlan.Route(
                UpscaleLink.PortableUpscaler,
                canvasAvailable: true,
                shaderAvailable: true,
                sourceWidth,
                sourceHeight));
    }

    /// <summary>
    /// One source pixel measured in destination units, which is what makes the sharpening sharpen.
    /// </summary>
    /// <remarks>
    /// A four-times enlargement has to answer four, not one. Stepping by one would sample inside the
    /// same source pixel on every side of the centre and subtract a blur identical to it — a shader
    /// that costs a frame and changes nothing, which is the shape this whole feature was built to
    /// avoid rather than to repeat.
    /// </remarks>
    [Fact]
    public void One_source_pixel_is_measured_in_destination_units_and_not_in_its_own()
    {
        var (x, y) = UpscaleDrawPlan.SourceStep(new Rect(0, 0, 640, 360), 160, 90);

        Assert.Equal(4f, x);
        Assert.Equal(4f, y);
    }

    /// <summary>A picture drawn at its own size steps by exactly one.</summary>
    [Fact]
    public void A_picture_drawn_at_its_own_size_steps_by_exactly_one()
    {
        var (x, y) = UpscaleDrawPlan.SourceStep(new Rect(0, 0, 160, 90), 160, 90);

        Assert.Equal(1f, x);
        Assert.Equal(1f, y);
    }

    /// <summary>
    /// One source pixel is not the same in both axes unless the box says so.
    /// </summary>
    /// <remarks>
    /// A non-square row, because every other row here is isotropic and returning the pair swapped
    /// survived the whole suite. It is equivalent in production today — <c>VideoFitPolicy.Fit</c>
    /// keeps the picture's shape, so the two steps are always equal — but that equivalence belongs to
    /// another piece, and a step that only works because something else happens to be square is a
    /// step nobody has actually checked.
    /// </remarks>
    [Fact]
    public void One_source_pixel_is_measured_separately_in_each_axis()
    {
        var (x, y) = UpscaleDrawPlan.SourceStep(new Rect(0, 0, 640, 180), 160, 90);

        Assert.Equal(4f, x);
        Assert.Equal(2f, y);
    }

    /// <summary>
    /// The byte order is the one the engine hands its frames over in.
    /// </summary>
    /// <remarks>
    /// Named and asserted because nothing else in this tree could see it change: the other order
    /// swaps red and blue in every enlarged video, and until 2026-09-13 every test picture was grey
    /// or pure green — the single colour identical in both orders. The behavioural half of this lives
    /// in <c>SkiaUpscaleDrawOperationTests</c>, which draws something red; this half is what stops
    /// the value being edited with nothing pointing at it.
    /// </remarks>
    [Fact]
    public void The_byte_order_is_the_one_the_engine_hands_its_frames_over_in()
    {
        Assert.Equal(SKColorType.Bgra8888, UpscaleDrawPlan.SourceColourType);
    }

    /// <summary>
    /// The resampling kernel is the sharper of the two cubics, which is the whole point.
    /// </summary>
    /// <remarks>
    /// Mitchell and Catmull-Rom are both cubic and Mitchell is the softer, so swapping them makes the
    /// enlargement worse in exactly the dimension being improved — and the swap survived the whole
    /// suite while the comment beside it argued the opposite. Catmull-Rom is B=0, C=0.5.
    /// </remarks>
    [Fact]
    public void The_resampling_kernel_is_the_sharper_of_the_two_cubics()
    {
        Assert.Equal(new SKSamplingOptions(SKCubicResampler.CatmullRom), UpscaleDrawPlan.Resampling);
        Assert.NotEqual(new SKSamplingOptions(SKCubicResampler.Mitchell), UpscaleDrawPlan.Resampling);
    }

    /// <summary>
    /// What feeds the sharpening pass is sampled linearly, not cubically and not by nearest.
    /// </summary>
    /// <remarks>
    /// Two sharpenings stacked overshoot into a visible outline, and nearest hands the shader a
    /// staircase to sharpen. The ramp gate already kills the nearest case; this names the choice.
    /// </remarks>
    [Fact]
    public void What_feeds_the_sharpening_pass_is_sampled_linearly()
    {
        Assert.Equal(new SKSamplingOptions(SKFilterMode.Linear), UpscaleDrawPlan.SharpeningSource);
    }

    /// <summary>
    /// The sampler's matrix carries the letterbox offset as well as the scale.
    /// </summary>
    /// <remarks>
    /// The box a picture is drawn in is not the surface: a 4:3 episode on a 16:9 display sits inside
    /// bars. A matrix that only scaled would sample from the top-left corner of the window and slide
    /// the whole picture up and left by the height of the bar.
    /// </remarks>
    [Fact]
    public void The_samplers_matrix_carries_the_letterbox_offset_as_well_as_the_scale()
    {
        var matrix = UpscaleDrawPlan.SourceToDestination(new Rect(40, 20, 640, 360), 160, 90);

        Assert.Equal(4f, matrix.ScaleX);
        Assert.Equal(4f, matrix.ScaleY);
        Assert.Equal(40f, matrix.TransX);
        Assert.Equal(20f, matrix.TransY);
    }
}
