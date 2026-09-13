// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// How a picture smaller than its surface is enlarged, measured in ink rather than in settings.
/// </summary>
/// <remarks>
/// <para>
/// The owner reported on 2026-09-12 that video «still looks bad», and the first guess was cheap:
/// Avalonia's default interpolation is <c>LowQuality</c> and nothing in this tree sets anything
/// else, so asking for <c>HighQuality</c> looked like a one-line win. <b>Measured, it is not.</b>
/// Across a hard edge enlarged four times the profiles are
/// <c>1,34,93,162,221,254</c> for high quality and <c>31,95,159,223</c> for low: the same four-pixel
/// ramp, with high quality a shade <i>softer</i> if anything.
/// </para>
/// <para>
/// So this file archives a negative result and guards it. The softness of an enlarged picture is not
/// a renderer setting anybody forgot to flip — it is what enlarging without a real upscaler looks
/// like, and removing it is `PLY-016`'s job and nothing else's. The day one of Avalonia's modes
/// becomes genuinely sharper than another, the first test here goes red and somebody revisits.
/// </para>
/// </remarks>
public sealed class VideoUpscaleQualityTests
{
    /// <summary>A four-times enlargement, which is 1080p on a 4K screen and worse for 720p.</summary>
    private const int SourceWidth = 160;
    private const int SourceHeight = 90;
    private const int SurfaceWidth = 640;
    private const int SurfaceHeight = 360;

    /// <summary>
    /// No interpolation mode this renderer offers makes an enlarged picture meaningfully sharper.
    /// </summary>
    [AvaloniaFact]
    public void Asking_for_a_better_filter_buys_nothing_so_sharpness_has_to_come_from_elsewhere()
    {
        var sharp = Ramp(BitmapInterpolationMode.HighQuality);
        var soft = Ramp(BitmapInterpolationMode.LowQuality);

        Assert.True(
            Math.Abs(sharp - soft) <= 1,
            $"High quality now ramps over {sharp} pixels against low quality's {soft}. They used to "
            + "be the same, which is why no mode is set: if one is now sharper, set it and delete this.");
    }

    /// <summary>
    /// The positive control, and without it the test above would pass on a harness that never
    /// enlarged anything: turning interpolation off entirely has to produce a hard step.
    /// </summary>
    [AvaloniaFact]
    public void Turning_interpolation_off_gives_the_hard_step_that_proves_the_others_are_smoothing()
    {
        var none = Ramp(BitmapInterpolationMode.None);
        var soft = Ramp(BitmapInterpolationMode.LowQuality);

        Assert.Equal(0, none);
        Assert.True(soft >= 3, $"Low quality ramped over only {soft} pixels, so nothing was enlarged.");
    }

    /// <summary>
    /// And that nobody quietly set one on the surface <b>the application actually draws</b>.
    /// </summary>
    /// <remarks>
    /// This used to build a bare <c>VideoFrameView</c> with no parent and no template, which is the
    /// one place a mode would never be set. Measured 2026-09-12: adding
    /// <c>RenderOptions.BitmapInterpolationMode="HighQuality"</c> to the surface in
    /// <c>PlayerView.axaml</c> left all 1340 tests in this suite green.
    /// </remarks>
    [AvaloniaFact]
    public void The_surface_the_player_really_draws_asks_for_no_particular_filter()
    {
        var view = new PlayerView();
        var surface = view.GetVisualDescendants().OfType<VideoFrameView>().SingleOrDefault()
            ?? view.FindControl<VideoFrameView>("VideoSurface");
        Assert.NotNull(surface);

        Assert.Equal(
            BitmapInterpolationMode.Unspecified,
            RenderOptions.GetBitmapInterpolationMode(surface!));
    }

    /// <summary>
    /// The enhancement is already on before anybody touches anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// «Without the user touching anything» is the whole of what the owner asked for, and it rests on
    /// one default value. <b>This test exists because nothing else here can see that value.</b> Every
    /// other test in this file sets the property explicitly, so flipping the default to <c>false</c>
    /// would leave all of them green with the feature off for everybody — measured by flipping it.
    /// </para>
    /// <para>
    /// Asserted on the ink and not on the property: a surface nobody configured has to ramp over
    /// fewer pixels than the composition gives. A property somebody reads out of the view proves the
    /// value is stored, not that a picture is better.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_enhancement_is_already_on_before_anybody_touches_anything()
    {
        var untouched = Ramp(BitmapInterpolationMode.Unspecified, upscale: null);
        var composition = Ramp(BitmapInterpolationMode.LowQuality, upscale: false);

        // The floor, and it is not decoration: measured 2026-09-13 with the enhanced route drawing
        // NOTHING AT ALL, this test stayed green while nine others went red. Without it, the one test
        // holding up «it ships switched on» would call a black screen switched on.
        Assert.True(
            untouched > 0,
            $"The untouched surface ramps over {untouched} pixels, and zero is what «nothing was "
                + "drawn» looks like — an enlarged hard edge cannot come back perfectly hard.");
        Assert.True(
            untouched < composition,
            $"A surface nobody configured ramps over {untouched} pixels against the composition's "
                + $"{composition}, so the enhancement ships switched off and nobody will ever find "
                + "the switch. The default of VideoFrameView.IsUpscaleEnabledProperty is the one "
                + "thing this depends on.");
    }

    /// <summary>
    /// «Switched off, nothing changes», measured in ink rather than promised in a comment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The half of PLY-016's acceptance criterion a person notices first when it is wrong. Asserted
    /// against <c>LowQuality</c> and not against a remembered number: the claim is «the same as the
    /// composition gives», so the composition is what it is compared to.
    /// </para>
    /// <para>
    /// <b>The second assertion is here because a mutant survived the first one.</b> Ignoring the
    /// switch entirely left «off» and «the composition» measuring the same thing — both enhanced —
    /// so a purely relative test called a broken switch correct. Requiring that on and off actually
    /// differ is what cannot be fooled by both sides moving together.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Turned_off_an_enlarged_frame_ramps_over_exactly_what_the_composition_gives()
    {
        var off = Ramp(BitmapInterpolationMode.Unspecified, upscale: false);
        var composition = Ramp(BitmapInterpolationMode.LowQuality);
        var on = Ramp(BitmapInterpolationMode.Unspecified, upscale: true);

        Assert.Equal(composition, off);

        // The same floor as its sibling, for the same measured reason: `on` coming back as zero
        // would satisfy «they differ» while meaning nothing was drawn at all.
        Assert.True(on > 0, $"The enhanced surface ramps over {on} pixels, so nothing was drawn.");
        Assert.NotEqual(on, off);
    }

    /// <summary>
    /// The case the feature exists for: the enhancement leaves a narrower ramp than the composition.
    /// </summary>
    /// <remarks>
    /// Four is what bilinear measures here and what every mode this renderer offers measures, so
    /// three or fewer is the whole of the improvement being claimed. The guard underneath is the one
    /// this file already carries: a ramp of zero means nothing was enlarged, not that the edge came
    /// back perfect.
    /// </remarks>
    [AvaloniaFact]
    public void A_picture_below_its_box_is_enlarged_over_a_narrower_ramp_than_the_composition_gives()
    {
        var enhanced = Ramp(BitmapInterpolationMode.Unspecified, upscale: true);
        var composition = Ramp(BitmapInterpolationMode.LowQuality);

        Assert.True(
            enhanced > 0,
            $"The enhanced ramp measured {enhanced}, and zero is what «nothing was drawn» looks "
                + "like too — an enlarged hard edge cannot come back perfectly hard.");
        Assert.True(
            enhanced < composition,
            $"The enhancement ramps over {enhanced} pixels against the composition's {composition}, "
                + "so it is not sharpening anything a person would see. Measured 2026-09-13 at a "
                + "strength of 0.6: 2 against 4, profile 31,95,159,223 becoming 3,86,169,252. At 0.3 "
                + "the ramp stays at 4, so the strength is what carries this and not the shader "
                + "merely running.");
    }

    /// <summary>
    /// A picture already as large as its box is drawn by the composition, byte for byte.
    /// </summary>
    /// <remarks>
    /// The product's own negative control. It is asserted on the whole frame rather than on a ramp
    /// because «identical» is the claim: a chain that spent a shader here and happened to land on
    /// the same ramp width would pass a narrower test.
    /// </remarks>
    [AvaloniaFact]
    public void A_picture_already_as_large_as_its_box_is_drawn_by_the_composition_byte_for_byte()
    {
        var off = Capture(
            SurfaceWidth,
            SurfaceHeight,
            SurfaceWidth,
            SurfaceHeight,
            BitmapInterpolationMode.Unspecified,
            upscale: false);
        var on = Capture(
            SurfaceWidth,
            SurfaceHeight,
            SurfaceWidth,
            SurfaceHeight,
            BitmapInterpolationMode.Unspecified,
            upscale: true);

        Assert.Equal(off.Format, on.Format);
        Assert.Equal(off.RowBytes, on.RowBytes);

        // The instrument floor, and its first draft was wrong in a way worth keeping: it demanded
        // more than two distinct values, and a hard edge drawn at its own size has exactly two.
        // What proves the capture is this scene is that BOTH sides of the edge are in it — read on
        // the green channel, because every alpha byte is 255 and would satisfy «white» on its own.
        var greens = off.Bytes.Where((_, index) => index % 4 == 1).ToArray();
        Assert.True(
            greens.Any(value => value < 8) && greens.Any(value => value > 247),
            "The captured frame has no black side or no white side, so it is not the edge this scene "
                + "paints and the comparison below would hold for two blank pictures.");
        Assert.Equal(off.Bytes, on.Bytes);
    }

    /// <summary>
    /// The surface stops listening, and lets go of its frame, when it is closed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The enhancement gave this surface two things to own that it did not have before — a compiled
    /// shader and the last frame's pixels — and a player is opened and closed once per film. The
    /// shader is deliberately <b>not</b> released here; the reason is written where it is held.
    /// </para>
    /// <para>
    /// <b>What is asserted is that it stopped listening, and the first draft asserted the wrong
    /// thing.</b> It required the screen to go blank, and disposing does not invalidate the visual:
    /// the last composed frame simply stays there, so a surface that had gone on listening would
    /// have passed. Publishing a <i>mirrored</i> edge is what tells the two apart — if the frame
    /// still reached the surface, the black side would now be on the right.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_surface_stops_listening_and_lets_go_of_its_frame_when_it_is_closed()
    {
        var source = new EdgeFrames(SourceWidth, SourceHeight);
        var surface = new VideoFrameView
        {
            FrameSource = source,
            Width = SurfaceWidth,
            Height = SurfaceHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var window = new Window
        {
            Width = SurfaceWidth,
            Height = SurfaceHeight,
            Padding = new Thickness(0),
            Content = surface,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            // Two frames, because the shader is compiled on the first pass and has to be FOUND on
            // the second: a surface that recompiled it every frame would spend the budget the whole
            // enhancement is measured against. Two captures alone would not do it — nothing
            // invalidated the visual in between, so the second one hands back the same composed
            // frame without the surface drawing again.
            source.Publish();
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();
            source.Publish();
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();

            Assert.True(
                HasBothSides(window),
                "Nothing was drawn before the surface was disposed, so the silence afterwards proves "
                    + "nothing.");

            var darkOnTheLeft = GreenAt(window, 10, SurfaceHeight / 2);
            Assert.True(darkOnTheLeft < 8, $"The left side reads {darkOnTheLeft}, so it is not dark.");

            surface.Dispose();
            source.Publish(mirrored: true);
            Dispatcher.UIThread.RunJobs();

            var afterwards = GreenAt(window, 10, SurfaceHeight / 2);
            Assert.True(
                afterwards < 8,
                $"The left side reads {afterwards} after the surface was disposed and a mirrored "
                    + "frame was published, so the surface is still listening to an engine that "
                    + "outlives it.");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Whether the window carries both sides of the edge this scene paints.</summary>
    private static bool HasBothSides(Window window)
    {
        using var frame = window.CaptureRenderedFrame();
        if (frame is null)
        {
            return false;
        }

        using var buffer = frame.Lock();
        var bytes = new byte[buffer.RowBytes * frame.PixelSize.Height];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);
        var greens = bytes.Where((_, index) => index % 4 == 1).ToArray();

        return greens.Any(value => value < 8) && greens.Any(value => value > 247);
    }

    /// <summary>The green channel of one pixel of what is on screen.</summary>
    private static byte GreenAt(Window window, int column, int row)
    {
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("the headless backend returned no frame.");
        using var buffer = frame.Lock();
        var pixel = new byte[4];
        System.Runtime.InteropServices.Marshal.Copy(
            buffer.Address + (row * buffer.RowBytes) + (column * 4),
            pixel,
            0,
            4);

        return pixel[1];
    }

    /// <summary>How many pixels across the edge come back neither black nor white.</summary>
    /// <remarks>
    /// <paramref name="upscale"/> defaults to <c>false</c> so the three tests above keep measuring
    /// what they were written to measure — <b>Avalonia's own filter</b>. The enhancement ships on,
    /// so leaving it on here would have quietly turned those three into measurements of `PLY-016`'s
    /// chain instead, and the hard-step control would have gone red for the wrong reason.
    /// </remarks>
    private static int Ramp(BitmapInterpolationMode mode, bool? upscale = false) =>
        Profile(mode, upscale).Count(value => value > 8 && value < 247);

    /// <summary>
    /// How many pixels across a hard black-to-white edge come back neither black nor white. A
    /// filter that enlarges by smearing leaves more of them.
    /// </summary>
    private static int[] Profile(BitmapInterpolationMode mode, bool? upscale = false)
    {
        var (bytes, rowBytes, _) = Capture(
            SourceWidth,
            SourceHeight,
            SurfaceWidth,
            SurfaceHeight,
            mode,
            upscale);

        var row = SurfaceHeight / 2;

        // Ten pixels either side of where the source's hard edge lands, which is the middle.
        var values = new int[20];
        for (var offset = 0; offset < values.Length; offset++)
        {
            values[offset] = bytes[(row * rowBytes) + (((SurfaceWidth / 2) - 10 + offset) * 4) + 1];
        }

        return values;
    }

    /// <summary>
    /// The whole captured frame, for the questions a single row cannot answer — «byte for byte the
    /// same» being the one the acceptance criterion asks for.
    /// </summary>
    private static (byte[] Bytes, int RowBytes, PixelFormat? Format) Capture(
        int sourceWidth,
        int sourceHeight,
        int surfaceWidth,
        int surfaceHeight,
        BitmapInterpolationMode mode,
        bool? upscale)
    {
        var source = new EdgeFrames(sourceWidth, sourceHeight);

        // A null leaves the property alone, which is the only way to measure what somebody who never
        // opened a settings panel actually sees.
        var surface = new VideoFrameView
        {
            FrameSource = source,
            Width = surfaceWidth,
            Height = surfaceHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        if (upscale is { } asked)
        {
            surface.IsUpscaleEnabled = asked;
        }
        RenderOptions.SetBitmapInterpolationMode(surface, mode);

        var window = new Window
        {
            Width = surfaceWidth + 40,
            Height = surfaceHeight + 40,
            Padding = new Thickness(0),
            Content = surface,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        source.Publish();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        try
        {
            using var buffer = frame!.Lock();
            var bytes = new byte[buffer.RowBytes * frame.PixelSize.Height];
            System.Runtime.InteropServices.Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

            return (bytes, buffer.RowBytes, buffer.Format);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Black on the left, white on the right, with the hardest edge a picture can carry.</summary>
    private sealed class EdgeFrames(int width, int height) : IVideoFrameSource
    {
        public event EventHandler<VideoFrameEventArgs>? FrameRendered;

        public void Publish(bool mirrored = false)
        {
            var stride = width * 4;
            var pixels = new byte[stride * height];
            for (var row = 0; row < height; row++)
            {
                for (var column = 0; column < width; column++)
                {
                    var dark = mirrored ? column >= width / 2 : column < width / 2;
                    var value = (byte)(dark ? 0 : 255);
                    var at = (row * stride) + (column * 4);
                    pixels[at] = value;
                    pixels[at + 1] = value;
                    pixels[at + 2] = value;
                    pixels[at + 3] = 255;
                }
            }

            FrameRendered?.Invoke(this, new VideoFrameEventArgs(pixels, width, height, stride));
        }
    }
}
