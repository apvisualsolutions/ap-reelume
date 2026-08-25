// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The picture keeps the shape it was decoded with, whatever the window has been dragged to.
/// </summary>
/// <remarks>
/// It was drawn across the whole of its bounds, so a 16:9 episode in a window somebody had made
/// taller came out taller — reported by the owner on 2026-08-25, in the player and in the
/// picture-in-picture alike. The arithmetic is <c>VideoFitPolicy</c>'s and is measured there; what is
/// measured here is that the surface pays it, which is the half a policy cannot assert about itself.
/// </remarks>
public sealed class VideoLetterboxTests
{
    [AvaloniaTheory]
    // A surface wider than the picture: bars at the sides, and the picture as tall as it can be.
    [InlineData(800d, 200d)]
    // A surface taller than the picture: bars above and below.
    [InlineData(300d, 600d)]
    public void The_picture_never_fills_a_surface_of_another_shape(double width, double height)
    {
        var source = new FlatFrames(320, 180);
        var surface = new VideoFrameView { FrameSource = source, Width = width, Height = height };
        var window = Mount(surface, width, height);
        source.Publish();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        // Where the bars fall is what says the shape survived: the centre carries the picture and
        // one of the two edges does not.
        var centre = ReadPixel(frame!, width / 2, height / 2);
        var edge = width / height > 320.0 / 180
            ? ReadPixel(frame!, 2, height / 2)
            : ReadPixel(frame!, width / 2, 2);
        Assert.Equal(FlatFrames.Colour, centre);
        Assert.NotEqual(FlatFrames.Colour, edge);
        window.Close();
    }

    [AvaloniaFact]
    public void A_surface_of_the_same_shape_carries_the_picture_to_its_own_edges()
    {
        var source = new FlatFrames(320, 180);
        var surface = new VideoFrameView { FrameSource = source, Width = 640, Height = 360 };
        var window = Mount(surface, 640, 360);
        source.Publish();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(FlatFrames.Colour, ReadPixel(frame!, 2, 2));
        Assert.Equal(FlatFrames.Colour, ReadPixel(frame!, 637, 357));
        window.Close();
    }

    /// <summary>
    /// A surface with no size of its own, which is what one is between being built and laid out.
    /// </summary>
    [AvaloniaFact]
    public void A_surface_with_no_room_draws_nothing_rather_than_dividing_by_it()
    {
        var source = new FlatFrames(320, 180);
        var surface = new VideoFrameView { FrameSource = source, Width = 0, Height = 0 };
        var window = Mount(surface, 0, 0);
        source.Publish();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, surface.Bounds.Width);
        Assert.NotNull(window.CaptureRenderedFrame());
        window.Close();
    }

    [AvaloniaFact]
    public void A_frame_with_no_pixels_at_all_is_refused_before_a_bitmap_is_built()
    {
        var source = new FlatFrames(0, 0);
        var surface = new VideoFrameView { FrameSource = source, Width = 320, Height = 180 };
        var window = Mount(surface, 320, 180);
        source.Publish();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(window.CaptureRenderedFrame());
        surface.Dispose();
        surface.Dispose();
        window.Close();
    }

    /// <summary>
    /// The surface is placed at the window's own origin, so a pixel of the picture is a pixel of the
    /// captured frame and nothing has to be translated between them.
    /// </summary>
    private static Window Mount(VideoFrameView surface, double width, double height)
    {
        surface.HorizontalAlignment = HorizontalAlignment.Left;
        surface.VerticalAlignment = VerticalAlignment.Top;
        var window = new Window
        {
            Width = width + 40,
            Height = height + 40,
            Padding = new Thickness(0),
            Content = surface,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static (byte Blue, byte Green, byte Red) ReadPixel(WriteableBitmap frame, double x, double y)
    {
        using var buffer = frame.Lock();
        var column = Math.Clamp((int)x, 0, buffer.Size.Width - 1);
        var row = Math.Clamp((int)y, 0, buffer.Size.Height - 1);
        var pixel = new byte[4];
        System.Runtime.InteropServices.Marshal.Copy(
            buffer.Address + (row * buffer.RowBytes) + (column * 4),
            pixel,
            0,
            4);
        return (pixel[0], pixel[1], pixel[2]);
    }

    private sealed class FlatFrames(int width, int height) : IVideoFrameSource
    {
        public static (byte Blue, byte Green, byte Red) Colour => (0, 255, 0);

        public event EventHandler<VideoFrameEventArgs>? FrameRendered;

        public void Publish()
        {
            var stride = Math.Max(1, width * 4);
            var pixels = new byte[stride * Math.Max(1, height)];
            for (var offset = 0; offset + 3 < pixels.Length; offset += 4)
            {
                pixels[offset] = Colour.Blue;
                pixels[offset + 1] = Colour.Green;
                pixels[offset + 2] = Colour.Red;
                pixels[offset + 3] = 255;
            }

            FrameRendered?.Invoke(this, new VideoFrameEventArgs(pixels, width, height, stride));
        }
    }
}
