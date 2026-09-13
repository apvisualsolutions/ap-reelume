// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Runtime.InteropServices;
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
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// That PLY-016's drawing operation never leaves the screen blank, whichever way it fails.
/// </summary>
/// <remarks>
/// <para>
/// This is the file that exists for the failure nobody would be watching for. A frame is what a
/// person is looking at, so an operation that returns without drawing is a black picture — and the
/// two ways it can happen are both invisible from here: a card whose driver will not compile the
/// shader, and a renderer that is not Skia at all. Neither can be provoked on the machine writing
/// the code, so the fall is asserted by handing the operation the state those failures produce.
/// </para>
/// <para>
/// The claim is measured as ink and not as a returned value. «It fell to the next link» is only worth
/// anything if a picture came out at the other end, and a boolean saying so is the kind of assertion
/// that stays green while the screen is black.
/// </para>
/// </remarks>
public sealed class SkiaUpscaleDrawOperationTests
{
    private const int SourceWidth = 160;
    private const int SourceHeight = 90;
    private const int SurfaceWidth = 640;
    private const int SurfaceHeight = 360;

    /// <summary>
    /// A shader that would not compile falls to the cubic link and still draws the picture.
    /// </summary>
    /// <remarks>
    /// The state is handed in rather than provoked: a null effect is exactly what
    /// <see cref="UpscaleShaderSource.TryCompile"/> answers on a renderer whose driver rejects the
    /// SkSL, and it is the state the portable link cannot detect for itself.
    /// </remarks>
    [AvaloniaFact]
    public void A_shader_that_would_not_compile_falls_to_the_next_link_and_still_draws()
    {
        var ink = Draw(UpscaleLink.PortableUpscaler, withShader: false);

        AssertThePictureArrived(ink, "a null effect");
    }

    /// <summary>The cubic link on its own draws the picture too.</summary>
    /// <remarks>
    /// Reached here and not through the surface, because the surface only asks for it on a renderer
    /// that compiles no shader — which this machine is not, and a link nothing ever exercises is a
    /// link nobody has seen work.
    /// </remarks>
    [AvaloniaFact]
    public void The_cubic_link_draws_the_picture_on_its_own()
    {
        var ink = Draw(UpscaleLink.BicubicResample, withShader: false);

        AssertThePictureArrived(ink, "the cubic link");
    }

    /// <summary>And the portable link with its shader in place, which is the ordinary case.</summary>
    /// <remarks>
    /// The positive control of the two above: without it, «the picture arrived» would hold for an
    /// operation that ignored the link it was given and always drew the same way.
    /// </remarks>
    [AvaloniaFact]
    public void The_portable_link_draws_the_picture_with_its_shader_in_place()
    {
        var ink = Draw(UpscaleLink.PortableUpscaler, withShader: true);

        AssertThePictureArrived(ink, "the portable link");
    }

    /// <summary>
    /// A link this operation does not draw falls all the way to the composition's own bitmap.
    /// </summary>
    /// <remarks>
    /// The last step of the fall, and the only one reachable from here: a renderer with no Skia
    /// canvas needs a platform this application does not have, but a link the operation was never
    /// meant to draw takes the same exit. What matters is that the exit ends in a picture and not in
    /// a return.
    /// </remarks>
    [AvaloniaFact]
    public void A_link_this_operation_does_not_draw_still_ends_with_the_picture_on_screen()
    {
        var ink = Draw(UpscaleLink.CompositionBilinear, withShader: true);

        AssertThePictureArrived(ink, "a link it does not draw");
    }

    /// <summary>
    /// The portable link and the cubic link do not draw the same pixels, which is what stops the
    /// tests above from passing on an operation that ignores the link it was given.
    /// </summary>
    [AvaloniaFact]
    public void The_portable_link_and_the_cubic_link_do_not_draw_the_same_pixels()
    {
        var portable = Draw(UpscaleLink.PortableUpscaler, withShader: true);
        var cubic = Draw(UpscaleLink.BicubicResample, withShader: false);

        Assert.NotEqual(portable, cubic);
    }

    /// <summary>
    /// The operation is never hit-tested and never equal to another, which is load-bearing.
    /// </summary>
    /// <remarks>
    /// Avalonia skips redrawing an operation equal to the one already on screen, so an
    /// <c>Equals</c> that ever said yes would freeze the picture on whichever frame arrived first.
    /// And the transport bar sits above the video: an operation that answered a hit test would take
    /// clicks meant for play and pause, which is the defect the status badge's own comment records
    /// from 2026-08-15.
    /// </remarks>
    [AvaloniaFact]
    public void The_operation_is_never_hit_tested_and_never_equal_to_another()
    {
        using var fallback = new WriteableBitmap(
            new PixelSize(1, 1),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        var operation = new SkiaUpscaleDrawOperation(
            new byte[4],
            1,
            1,
            4,
            new Rect(0, 0, 10, 10),
            UpscaleLink.BicubicResample,
            effect: null,
            fallback);

        Assert.False(operation.HitTest(new Point(5, 5)));
        Assert.False(operation.Equals(operation));
        Assert.Equal(new Rect(0, 0, 10, 10), operation.Bounds);
        operation.Dispose();
    }

    /// <summary>
    /// A red picture comes back red, on both routes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The whole suite was blind to this until 2026-09-13.</b> Naming the other byte order swaps
    /// red and blue in every enlarged video, and the mutant survived all 1,437 tests because every
    /// test picture here was grey and the only one with colour anywhere was pure green — the single
    /// colour that is identical in both orders. The gate auditor found it.
    /// </para>
    /// <para>
    /// The capture's own order is read from the frame rather than assumed, because it is one of two
    /// and the scan would otherwise be measuring the harness instead of the operation.
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(UpscaleLink.PortableUpscaler)]
    [InlineData(UpscaleLink.BicubicResample)]
    [InlineData(UpscaleLink.CompositionBilinear)]
    public void A_red_picture_comes_back_red_and_not_blue(UpscaleLink link)
    {
        var (bytes, rowBytes, format) = DrawSolid(link, blue: 0, green: 0, red: 255);

        // Rgba8888 puts red at byte 0 and blue at byte 2; Bgra8888 is the other way round.
        var redFirst = format == PixelFormat.Rgba8888;
        Assert.True(
            redFirst || format == PixelFormat.Bgra8888,
            $"The capture came back as {format}, and this scan only holds for those two.");
        var redAt = redFirst ? 0 : 2;
        var blueAt = redFirst ? 2 : 0;

        var middle = ((SurfaceHeight / 2) * rowBytes) + ((SurfaceWidth / 2) * 4);
        Assert.True(
            bytes[middle + redAt] > 200,
            $"The middle of a red picture reads {bytes[middle + redAt]} on the red channel, so the "
                + "colour did not survive being enlarged.");
        Assert.True(
            bytes[middle + blueAt] < 60,
            $"The middle of a red picture reads {bytes[middle + blueAt]} on the blue channel, so red "
                + "and blue are swapped — the byte order handed to Skia is the wrong one.");
    }

    /// <summary>
    /// A picture drawn inside letterbox bars stays inside them, instead of sliding up and left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The suite could not see this until 2026-09-13</b>, and the reason is worth keeping: the two
    /// scenes were orthogonal. The one with an edge in it drew at the window's origin with no bars, and
    /// the ones with bars drew a flat colour, where moving the sampling point changes nothing. So
    /// dropping the translation out of the sampler's matrix survived all 1,437 tests — and a 4:3
    /// episode on a 16:9 display would have slid up and left inside its own box.
    /// </para>
    /// <para>
    /// The edge is asserted a few pixels either side of where it has to land rather than exactly on
    /// it, because the enlargement spreads it over two and an assertion on the middle pixel would be
    /// measuring the ramp instead of the position.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void A_picture_drawn_inside_bars_keeps_its_edge_where_the_bars_put_it()
    {
        const int offsetX = 80;
        const int offsetY = 40;
        var (bytes, rowBytes, _) = Capture(
            EdgePixels(),
            SourceWidth * 4,
            UpscaleLink.PortableUpscaler,
            withShader: true,
            offset: new Point(offsetX, offsetY));

        var row = offsetY + (SurfaceHeight / 2);
        var edge = offsetX + (SurfaceWidth / 2);
        var dark = bytes[(row * rowBytes) + ((edge - 10) * 4) + 1];
        var light = bytes[(row * rowBytes) + ((edge + 10) * 4) + 1];

        Assert.True(
            dark < 8,
            $"Ten pixels left of where the edge belongs reads {dark}, so the picture is not where the "
                + "bars put it — the sampler's matrix has lost its translation and the whole frame "
                + $"has slid by {offsetX} pixels.");
        Assert.True(
            light > 247,
            $"Ten pixels right of where the edge belongs reads {light}, so the picture is not where "
                + "the bars put it.");
    }

    /// <summary>
    /// The sharpening overshoots, and by how much is bounded rather than left to taste.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the gate the black-and-white scene cannot be.</b> An unsharp mask pushes the light
    /// side of an edge lighter and the dark side darker; on a picture that is already 0 and 255 the
    /// excess clamps away invisibly, so raising the strength from 0.6 to 1.0 changes nothing a
    /// saturated edge can show. It is this tree's own lesson — «un primario saturado no mide» — and
    /// the gate auditor found it here on 2026-09-13.
    /// </para>
    /// <para>
    /// So the edge is grey against grey, 64 against 192, where there is room on both sides for the
    /// overshoot to be seen. Measured: at a strength of 0.6 the profile reaches 47 and 209, which is
    /// 17 past each flat level; at 1.0 it reaches 36 and 220, which is 28.
    /// </para>
    /// <para>
    /// <b>The ceiling was cut to 14 for half an hour and then put back, and why is worth keeping.</b>
    /// The owner reported tonal squares around lettering, this step measured 16, and the obvious move
    /// was to make the bound tighter than what he had objected to. It was the wrong move because the
    /// diagnosis was wrong: he then found that <b>his gamma was at 1.5 and at 1.0 the squares stop</b>,
    /// so the artefact was banding from an 8-bit tone curve (`ENG-021`) and this step was never in it.
    /// A ceiling set to exclude somebody else's defect excludes good settings for nothing.
    /// </para>
    /// <para>
    /// <b>And this is what pins the resampling coefficient</b>, which no other gate can see: the step
    /// scales straight with it — 9 levels at <c>C = 0.5</c>, 13 at 0.7, 16 at 0.85 — while the fidelity
    /// gate next door pulls the other way, rewarding the largest. Neither alone would hold a middle.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_sharpening_overshoot_stays_inside_what_reads_as_an_edge_and_not_an_outline()
    {
        const byte dark = 64;
        const byte light = 192;
        const int mostItMayOvershoot = 22;

        var (bytes, rowBytes, _) = Capture(
            EdgePixels(dark, light),
            SourceWidth * 4,
            UpscaleLink.PortableUpscaler,
            withShader: true);

        var row = SurfaceHeight / 2;
        var darkest = 255;
        var lightest = 0;
        for (var column = 0; column < SurfaceWidth; column++)
        {
            var value = bytes[(row * rowBytes) + (column * 4) + 1];
            darkest = Math.Min(darkest, value);
            lightest = Math.Max(lightest, value);
        }

        Assert.True(
            darkest < dark,
            $"The darkest pixel across the edge is {darkest}, which is not below the flat level of "
                + $"{dark}, so no sharpening happened and this bound is guarding nothing.");
        Assert.True(
            dark - darkest <= mostItMayOvershoot,
            $"The sharpening undershoots by {dark - darkest} levels against a bound of "
                + $"{mostItMayOvershoot}. Measured 2026-09-13: 17 at a strength of 0.6 and 28 at 1.0, "
                + "and 28 is what draws an outline around everything.");
        Assert.True(
            lightest - light <= mostItMayOvershoot,
            $"The sharpening overshoots by {lightest - light} levels against a bound of "
                + $"{mostItMayOvershoot}.");
    }

    private static void AssertThePictureArrived(byte[] ink, string what)
    {
        var greens = ink.Where((_, index) => index % 4 == 1).ToArray();

        Assert.True(
            greens.Any(value => value < 8),
            $"With {what} the frame has no dark side, so no picture was drawn.");
        Assert.True(
            greens.Any(value => value > 247),
            $"With {what} the frame has no light side, so no picture was drawn.");
    }

    /// <summary>The captured frame of a hard black-to-white edge, drawn by one operation.</summary>
    private static byte[] Draw(UpscaleLink link, bool withShader) =>
        Capture(EdgePixels(), SourceWidth * 4, link, withShader).Bytes;

    /// <summary>Dark on the left, light on the right, in the engine's own byte order.</summary>
    private static byte[] EdgePixels(byte dark = 0, byte light = 255)
    {
        var stride = SourceWidth * 4;
        var pixels = new byte[stride * SourceHeight];
        for (var row = 0; row < SourceHeight; row++)
        {
            for (var column = 0; column < SourceWidth; column++)
            {
                var value = column < SourceWidth / 2 ? dark : light;
                var at = (row * stride) + (column * 4);
                pixels[at] = value;
                pixels[at + 1] = value;
                pixels[at + 2] = value;
                pixels[at + 3] = 255;
            }
        }

        return pixels;
    }

    /// <summary>
    /// The captured frame of one flat colour, given in the byte order the engine uses.
    /// </summary>
    private static (byte[] Bytes, int RowBytes, PixelFormat? Format) DrawSolid(
        UpscaleLink link,
        byte blue,
        byte green,
        byte red)
    {
        var stride = SourceWidth * 4;
        var pixels = new byte[stride * SourceHeight];
        for (var at = 0; at < pixels.Length; at += 4)
        {
            pixels[at] = blue;
            pixels[at + 1] = green;
            pixels[at + 2] = red;
            pixels[at + 3] = 255;
        }

        return Capture(pixels, stride, link, withShader: true);
    }

    /// <summary>One operation, drawn on its own in a window, and what came out.</summary>
    private static (byte[] Bytes, int RowBytes, PixelFormat? Format) Capture(
        byte[] pixels,
        int stride,
        UpscaleLink link,
        bool withShader,
        Point offset = default)
    {
        using var fallback = new WriteableBitmap(
            new PixelSize(SourceWidth, SourceHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        using (var buffer = fallback.Lock())
        {
            Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
        }

        using var effect = withShader ? UpscaleShaderSource.TryCompile(out _) : null;
        Assert.True(
            !withShader || effect is not null,
            "The shader did not compile, so the positive control is measuring the fall it is meant "
                + "to be the control for.");

        var host = new OperationHost(new SkiaUpscaleDrawOperation(
            pixels,
            SourceWidth,
            SourceHeight,
            stride,
            new Rect(offset.X, offset.Y, SurfaceWidth, SurfaceHeight),
            link,
            effect,
            fallback))
        {
            Width = SurfaceWidth + offset.X,
            Height = SurfaceHeight + offset.Y,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var window = new Window
        {
            Width = SurfaceWidth + offset.X,
            Height = SurfaceHeight + offset.Y,
            Padding = new Thickness(0),
            Content = host,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        try
        {
            using var locked = frame!.Lock();
            var bytes = new byte[locked.RowBytes * frame.PixelSize.Height];
            Marshal.Copy(locked.Address, bytes, 0, bytes.Length);

            return (bytes, locked.RowBytes, locked.Format);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A control whose whole job is to emit one operation.</summary>
    private sealed class OperationHost(SkiaUpscaleDrawOperation operation) : Control
    {
        public override void Render(DrawingContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.Custom(operation);
        }
    }
}
