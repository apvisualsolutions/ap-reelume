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
using Avalonia.Threading;
using SkiaSharp;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// Where <see cref="VideoFrameView"/> gets its compiled shader, and how many times it asks (ENG-017).
/// </summary>
/// <remarks>
/// <para>
/// <b>The surface compiled the shader once and nothing said so.</b> The gate auditor found it on
/// 2026-09-13 while PLY-016 was being written: deleting the <c>_effectAsked</c> sentinel compiles the
/// SkSL on every frame — about 173,000 of them in a two-hour film — and the whole interface suite
/// stayed green, because the compiler was a static call behind a private field and nothing outside
/// the class could count anything. The answer was never a louder comment: it is the seam these tests
/// drive, <see cref="VideoFrameView.ShaderCompiler"/>.
/// </para>
/// <para>
/// <b>Counting alone would be a blind gate, and the way it goes blind is worth knowing.</b> A
/// surface that drew once would report one compilation and pass for the opposite reason. Publishing
/// a frame does not by itself prove the surface drew again either — nothing invalidates the visual
/// between two captures, so the second hands back the same composed frame. So the count travels with
/// a floor that reads the ink: three frames of three different greys, and the three captures have to
/// differ from one another. If they do not, the scene never drew three times and the count means
/// nothing.
/// </para>
/// </remarks>
public sealed class VideoFrameViewShaderTests
{
    // Four times the picture, which is what puts the chain on its enlarging path: PLY-016 does
    // nothing to a frame that is not being enlarged, and the shader would never be asked for.
    private const int SourceWidth = 160;
    private const int SourceHeight = 90;
    private const int SurfaceWidth = 640;
    private const int SurfaceHeight = 360;

    [AvaloniaFact]
    public void The_shader_is_compiled_once_for_the_life_of_the_surface()
    {
        var compiler = new CountingCompiler();
        var frames = new FlatFrames(SourceWidth, SourceHeight);
        var window = Open(frames, compiler.Compile);

        try
        {
            var first = Draw(window, frames, level: 40);
            var second = Draw(window, frames, level: 140);
            var third = Draw(window, frames, level: 240);

            // The floor. Without it the count passes on a scene that drew once.
            Assert.False(
                first.AsSpan().SequenceEqual(second) || second.AsSpan().SequenceEqual(third),
                "The three captures are not three different frames, so the surface did not draw "
                    + "three times and the compilation count below is measuring nothing.");

            Assert.Equal(1, compiler.Calls);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// That the fallback is the application's own compiler and not some other shader, asserted the
    /// only way a test can see it: the pixels a surface with no compiler draws are the pixels a
    /// surface handed <see cref="UpscaleShaderSource"/> draws, and both differ from the ones drawn
    /// when the shader is refused.
    /// </summary>
    /// <remarks>
    /// The third surface is the control. Without it this would be two halves of the same mistake
    /// agreeing with each other — if the seam quietly dropped the shader on both, the two captures
    /// would still match, and the test would pass while the enhancement did nothing.
    /// </remarks>
    [AvaloniaFact]
    public void A_surface_with_no_compiler_of_its_own_draws_what_the_applications_compiler_draws()
    {
        var asked = DrawEdgeOnce(compiler: null);
        var explicitly = DrawEdgeOnce(() => UpscaleShaderSource.TryCompile(out _));
        var refused = DrawEdgeOnce(() => null);

        Assert.True(
            asked.AsSpan().SequenceEqual(explicitly),
            "A surface that was handed no compiler drew something other than the application's own "
                + "shader, so the fallback is not what it claims to be.");
        Assert.False(
            asked.AsSpan().SequenceEqual(refused),
            "The shader and the link below it paint the same pixels here, so this comparison cannot "
                + "tell a working seam from a dropped one.");
    }

    /// <summary>
    /// What the owner would see on a machine whose driver rejects the SkSL: the film, drawn by the
    /// link below. The arm exists because of that machine and cannot be reached on one where the
    /// shader compiles, which is what the seam is for.
    /// </summary>
    [AvaloniaFact]
    public void A_surface_whose_shader_will_not_compile_draws_the_frame_anyway()
    {
        var frames = new FlatFrames(SourceWidth, SourceHeight);
        var window = Open(frames, () => null);

        try
        {
            var painted = Draw(window, frames, level: 200);

            Assert.Contains(painted, value => value > 190);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window Open(IVideoFrameSource frames, Func<SKRuntimeEffect?>? compiler)
    {
        var surface = new VideoFrameView
        {
            FrameSource = frames,
            IsUpscaleEnabled = true,
            ShaderCompiler = compiler,
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
        return window;
    }

    /// <summary>Publishes one frame and hands back the pixels that reached the screen.</summary>
    private static byte[] Draw(Window window, FlatFrames frames, byte level)
    {
        frames.Publish(level);
        Dispatcher.UIThread.RunJobs();
        return Capture(window);
    }

    private static byte[] DrawEdgeOnce(Func<SKRuntimeEffect?>? compiler)
    {
        var frames = new EdgeFrames(SourceWidth, SourceHeight);
        var window = Open(frames, compiler);

        try
        {
            frames.Publish();
            Dispatcher.UIThread.RunJobs();
            return Capture(window);
        }
        finally
        {
            window.Close();
        }
    }

    private static byte[] Capture(Window window)
    {
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("the headless backend returned no frame.");
        using var buffer = frame.Lock();
        var bytes = new byte[buffer.RowBytes * frame.PixelSize.Height];
        Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Counts what it was asked for. <see cref="Volatile"/> because the surface draws under its own
    /// lock and the assertion reads from the test's thread.
    /// </summary>
    private sealed class CountingCompiler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public SKRuntimeEffect? Compile()
        {
            Interlocked.Increment(ref _calls);
            return UpscaleShaderSource.TryCompile(out _);
        }
    }

    /// <summary>
    /// One grey, the whole frame. Flat on purpose: neither resampling nor sharpening changes a
    /// picture with no gradient in it, so the grey that goes in is the grey that comes out and two
    /// different greys are two visibly different captures.
    /// </summary>
    private sealed class FlatFrames(int width, int height) : IVideoFrameSource
    {
        public event EventHandler<VideoFrameEventArgs>? FrameRendered;

        public void Publish(byte level)
        {
            var stride = width * 4;
            var pixels = new byte[stride * height];
            for (var at = 0; at < pixels.Length; at += 4)
            {
                pixels[at] = level;
                pixels[at + 1] = level;
                pixels[at + 2] = level;
                pixels[at + 3] = 255;
            }

            FrameRendered?.Invoke(this, new VideoFrameEventArgs(pixels, width, height, stride));
        }
    }

    /// <summary>
    /// Black on the left, white on the right. A hard edge is the one thing the shader and the link
    /// below it draw differently, so it is what the comparison above needs.
    /// </summary>
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
