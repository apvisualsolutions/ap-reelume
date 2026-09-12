// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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

    /// <summary>How many pixels across the edge come back neither black nor white.</summary>
    private static int Ramp(BitmapInterpolationMode mode) =>
        Profile(mode).Count(value => value > 8 && value < 247);

    /// <summary>
    /// How many pixels across a hard black-to-white edge come back neither black nor white. A
    /// filter that enlarges by smearing leaves more of them.
    /// </summary>
    private static int[] Profile(BitmapInterpolationMode mode)
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
        RenderOptions.SetBitmapInterpolationMode(surface, mode);

        var window = new Window
        {
            Width = SurfaceWidth + 40,
            Height = SurfaceHeight + 40,
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
            var row = SurfaceHeight / 2;

            // Ten pixels either side of where the source's hard edge lands, which is the middle.
            var values = new int[20];
            for (var offset = 0; offset < values.Length; offset++)
            {
                values[offset] = ReadGreen(buffer, (SurfaceWidth / 2) - 10 + offset, row);
            }

            return values;
        }
        finally
        {
            window.Close();
        }
    }

    private static byte ReadGreen(ILockedFramebuffer buffer, int column, int row)
    {
        var pixel = new byte[4];
        System.Runtime.InteropServices.Marshal.Copy(
            buffer.Address + (Math.Clamp(row, 0, buffer.Size.Height - 1) * buffer.RowBytes)
                + (Math.Clamp(column, 0, buffer.Size.Width - 1) * 4),
            pixel,
            0,
            4);
        return pixel[1];
    }

    /// <summary>Black on the left, white on the right, with the hardest edge a picture can carry.</summary>
    private sealed class EdgeFrames(int width, int height) : IVideoFrameSource
    {
        public event EventHandler<VideoFrameEventArgs>? FrameRendered;

        public void Publish()
        {
            var stride = width * 4;
            var pixels = new byte[stride * height];
            for (var row = 0; row < height; row++)
            {
                for (var column = 0; column < width; column++)
                {
                    var value = (byte)(column < width / 2 ? 0 : 255);
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
