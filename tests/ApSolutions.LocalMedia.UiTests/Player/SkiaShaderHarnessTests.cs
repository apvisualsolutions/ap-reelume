// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// Whether this repository's pixel harness can see a Skia runtime shader, measured before `PLY-016`
/// builds its portable upscaler on one.
/// </summary>
/// <remarks>
/// <para>
/// The portable link of `PLY-016` has to sharpen an enlarged picture on <b>every</b> card, so it
/// cannot go through the D3D11 video processor — the CI runner carries only the Microsoft Basic
/// Render Driver, which exposes no <c>ID3D11VideoDevice</c> at all. The route that does run
/// everywhere is Avalonia's own Skia canvas, reached with <c>ICustomDrawOperation</c> and
/// <c>ISkiaSharpApiLeaseFeature</c>, with the sharpening written as SkSL.
/// </para>
/// <para>
/// <b>But a gate is only worth what its instrument can see.</b> SkiaSharp's documentation shows
/// <c>SKRuntimeEffect</c> in five samples and every one of them is a GPU surface — OpenGL, Metal,
/// WebGL, a swap chain — and none of them says whether an SkSL shader runs on a software canvas.
/// Inconclusive is not «no», so this file asks the question with controls instead of assuming
/// either answer. If a shader cannot be seen here, the upscaler belongs in the engine's CPU buffer
/// and the gate belongs there with it.
/// </para>
/// <para>
/// The mark is pure green on purpose, like <see cref="GpuCompositionHarnessTests"/>: green sits at
/// byte 1 in both <c>Rgba8888</c> and <c>Bgra8888</c>, so the scan cannot be fooled by the channel
/// order that already cost this tree one wrong reading. And the paper check reads the
/// <b>bottom-right</b> corner rather than the top-left, because a custom operation whose canvas
/// arrives without the control's transform would paint the top-left corner itself and turn an
/// honest reading into a thrown exception.
/// </para>
/// </remarks>
public sealed class SkiaShaderHarnessTests
{
    /// <summary>The white the scene is painted on.</summary>
    private const byte Paper = 250;

    /// <summary>A green mark reads above this on its own channel and below it on the other two.</summary>
    private const byte Mark = 200;

    private const byte Dark = 100;

    /// <summary>
    /// An SkSL runtime shader that paints one flat colour, which is the least a shader can do.
    /// </summary>
    /// <remarks>
    /// Deliberately free of uniforms, samplers and arithmetic: if this one does not arrive, nothing
    /// more elaborate will either, and the reason will be the route rather than the shader.
    /// </remarks>
    private const string FlatGreenShader =
        "half4 main(float2 coord) { return half4(0.0, 1.0, 0.0, 1.0); }";

    /// <summary>
    /// The control that must sound: an ordinary Avalonia visual leaves ink the scan can count.
    /// </summary>
    /// <remarks>
    /// Without it every silence below would be indistinguishable from a harness that draws nothing,
    /// which is exactly how a blind gate passes.
    /// </remarks>
    [AvaloniaFact]
    public void An_ordinary_visual_leaves_ink_the_scan_can_count()
    {
        using var scene = new Scene();
        scene.AddOrdinaryMark();
        var found = scene.CountGreen();

        Assert.True(
            found == Scene.MarkArea,
            $"The scan found {found} green pixels where an ordinary Border of exactly "
                + $"{Scene.MarkArea} was drawn, so the harness itself is not rendering what it is "
                + "told to and nothing measured with it means anything.");
    }

    /// <summary>The control that must stay silent: an empty scene has no green in it.</summary>
    [AvaloniaFact]
    public void An_empty_scene_leaves_no_ink_at_all()
    {
        using var scene = new Scene();

        Assert.Equal(0, scene.CountGreen());
    }

    /// <summary>
    /// The second silence, and it is a different one: an operation that <b>runs</b> and paints
    /// nothing.
    /// </summary>
    /// <remarks>
    /// Without this, «zero green» from the two questions below would have two causes that read
    /// identically — the operation never reached the renderer, or it reached it and drew nothing.
    /// This one proves the scan cannot invent ink for an operation that did run, so a zero there
    /// means the route and not the paint.
    /// </remarks>
    [AvaloniaFact]
    public void An_operation_that_runs_and_paints_nothing_leaves_the_paper_white()
    {
        using var scene = new Scene();
        scene.AddLeasedMark(LeasedMark.Paint.Nothing);

        Assert.Equal(0, scene.CountGreen());
        Assert.True(
            scene.LastMarkLeasedACanvas,
            "The operation never leased a canvas, so this silence is the route failing rather than "
                + "the control it is meant to be.");
    }

    /// <summary>
    /// The shader this probe asks for compiles, said out loud before anything is measured with it.
    /// </summary>
    /// <remarks>
    /// A shader that fails to compile paints nothing, and nothing is what «the harness cannot see a
    /// shader» looks like too. Without this test the two would be the same reading, and `PLY-016`
    /// would be sent down the CPU route by a typo. The API is
    /// <c>SKRuntimeEffect.CreateShader(sksl, out errors)</c> — measured on SkiaSharp 3.119.4, where
    /// there is no <c>SKRuntimeEffect.Create</c> at all.
    /// </remarks>
    [AvaloniaFact]
    public void The_runtime_shader_this_probe_asks_for_actually_compiles()
    {
        using var effect = SKRuntimeEffect.CreateShader(FlatGreenShader, out var errors);

        Assert.True(
            effect is not null,
            $"SkSL did not compile, so every silence below means a broken shader and not a blind "
                + $"harness. The compiler said: {errors}");
        Assert.True(string.IsNullOrEmpty(errors), $"SkSL compiled with complaints: {errors}");
    }

    /// <summary>
    /// Whether a custom draw operation reaches the captured frame at all, which is the door the
    /// shader has to come through.
    /// </summary>
    /// <remarks>
    /// Asked separately from the shader on purpose: if this one is silent the route is wrong, and if
    /// this one sounds while the shader does not, the route is right and SkSL is what the software
    /// canvas will not run. One test cannot tell those apart.
    /// </remarks>
    [AvaloniaFact]
    public void A_custom_draw_operation_reaches_the_captured_frame()
    {
        using var scene = new Scene();
        scene.AddLeasedMark(LeasedMark.Paint.Plain);
        var found = scene.CountGreen();

        Assert.True(
            found == Scene.MarkArea,
            $"The scan found {found} green pixels where a leased SKCanvas painted exactly "
                + $"{Scene.MarkArea}. Zero means ICustomDrawOperation never reaches "
                + "CaptureRenderedFrame in this harness, so `PLY-016`'s portable link cannot be "
                + "gated here and belongs in the engine's CPU buffer instead.");
    }

    /// <summary>
    /// The question this file exists for: whether an SkSL runtime shader reaches the captured frame.
    /// </summary>
    [AvaloniaFact]
    public void A_runtime_shader_reaches_the_captured_frame()
    {
        using var scene = new Scene();
        scene.AddLeasedMark(LeasedMark.Paint.Shader);
        var found = scene.CountGreen();

        Assert.True(
            found == Scene.MarkArea,
            $"The scan found {found} green pixels where an SkSL shader painted exactly "
                + $"{Scene.MarkArea}. Zero, with the two tests above green, means this harness runs "
                + "no runtime shader and `PLY-016`'s portable link has to be arithmetic on the "
                + "frame buffer rather than SkSL on the canvas.");
    }

    /// <summary>
    /// Whether Skia has a GPU context here, which is what makes the readings above interpretable.
    /// </summary>
    /// <remarks>
    /// Not a requirement and not a failure either way: it is the fact that says whether a green
    /// above proves SkSL runs on software or only that it runs on this machine's GPU. CI answers
    /// this differently from a desktop with a real adapter, and the evidence has to name which one
    /// it measured.
    /// </remarks>
    [AvaloniaFact]
    public void The_harness_says_whether_skia_is_drawing_on_the_gpu_or_in_software()
    {
        using var scene = new Scene();
        var onGpu = scene.LeaseHasGpuContext();

        Assert.True(
            onGpu is not null,
            "No custom draw operation was rendered, so nothing leased a canvas and the question was "
                + "never asked.");
        Assert.False(
            onGpu,
            "Skia now leases a GPU context in this harness, which it did not when this was measured. "
                + "That matters: the shader above was proven to arrive on a SOFTWARE canvas, which "
                + "is what makes the reading hold for a CI runner with no adapter. If Skia is now "
                + "on the GPU here, re-measure both and say which one CI answered.");
    }

    private sealed class Scene : IDisposable
    {
        private const int MarkWidth = 100;
        private const int MarkHeight = 40;

        /// <summary>
        /// What a mark of this size has to measure, to the pixel. «More than a thousand» would call
        /// a mark arriving at a quarter of its size a success.
        /// </summary>
        public const int MarkArea = MarkWidth * MarkHeight;

        private readonly Window _window;
        private readonly Border _host;
        private LeasedMark? _mark;

        public Scene()
        {
            _host = new Border { Background = Brushes.White };
            _window = new Window { Width = 200, Height = 120, Content = _host };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public void AddOrdinaryMark()
        {
            _host.Child = new Border
            {
                Background = Brushes.Lime,
                Width = MarkWidth,
                Height = MarkHeight,
            };
            Dispatcher.UIThread.RunJobs();
        }

        public void AddLeasedMark(LeasedMark.Paint paint)
        {
            _mark = new LeasedMark(MarkWidth, MarkHeight, paint);
            _host.Child = _mark;
            Dispatcher.UIThread.RunJobs();

            // The capture is what forces the render pass, so the lease has not happened yet.
            _window.CaptureRenderedFrame()?.Dispose();
        }

        /// <summary>Whether the operation that last ran got a Skia canvas at all.</summary>
        public bool LastMarkLeasedACanvas => _mark?.LeasedACanvas ?? false;

        /// <summary>
        /// Whether the canvas Skia leased is GPU-backed, or null if nothing leased one.
        /// </summary>
        public bool? LeaseHasGpuContext()
        {
            AddLeasedMark(LeasedMark.Paint.Plain);
            return _mark?.SawGpuContext;
        }

        /// <summary>Pixels whose green channel is lit and whose other two are not.</summary>
        public int CountGreen()
        {
            using var frame = _window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("the headless backend returned no frame.");
            using var buffer = frame.Lock();

            Assert.True(
                buffer.Format == PixelFormat.Rgba8888 || buffer.Format == PixelFormat.Bgra8888,
                $"The frame came back as {buffer.Format}, and the scan below only holds for the two "
                    + "formats whose green channel is byte 1.");

            var size = frame.PixelSize;
            var bytes = new byte[buffer.RowBytes * size.Height];
            Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

            var found = 0;
            for (var y = 0; y < size.Height; y++)
            {
                for (var x = 0; x < size.Width; x++)
                {
                    var i = (y * buffer.RowBytes) + (x * 4);
                    if (bytes[i + 1] > Mark && bytes[i] < Dark && bytes[i + 2] < Dark)
                    {
                        found++;
                    }
                }
            }

            // The bottom-right corner, which no placement of a 100x40 mark in a 200x120 window can
            // reach — not even one drawn at the window's origin because the transform was missing.
            var corner = ((size.Height - 1) * buffer.RowBytes) + ((size.Width - 1) * 4);
            Assert.True(
                bytes[corner + 1] > Paper,
                "The bottom-right pixel is not the white paper this scene paints, so the capture is "
                    + "not the scene and the count above belongs to something else.");

            return found;
        }

        public void Dispose()
        {
            _window.Close();
            _mark = null;
        }
    }

    /// <summary>
    /// A control that paints its whole area green through a leased <see cref="SKCanvas"/>, either
    /// with a plain paint or with an SkSL runtime shader.
    /// </summary>
    private sealed class LeasedMark : Control
    {
        private readonly Paint _paint;

        /// <summary>
        /// The size is set here and never inside <c>Render</c>: assigning <c>Width</c> during the
        /// render pass invalidates measure from inside the compositor's own commit, which throws.
        /// Measured — it cost this probe its first three reds.
        /// </summary>
        public LeasedMark(double width, double height, Paint paint)
        {
            Width = width;
            Height = height;
            _paint = paint;
        }

        /// <summary>What the operation puts on the leased canvas.</summary>
        public enum Paint
        {
            /// <summary>Leases the canvas and draws nothing, which is the silent control.</summary>
            Nothing,

            /// <summary>An ordinary solid paint, which asks only whether the route works.</summary>
            Plain,

            /// <summary>An SkSL runtime shader, which is the question this file exists for.</summary>
            Shader,
        }

        /// <summary>Whether the lease that actually happened carried a GPU context.</summary>
        public bool? SawGpuContext { get; private set; }

        /// <summary>Whether the operation got a Skia canvas at all when it ran.</summary>
        public bool LeasedACanvas { get; private set; }

        public override void Render(DrawingContext context) =>
            context.Custom(new Operation(new Rect(0, 0, Width, Height), _paint, this));

        private sealed class Operation(Rect bounds, Paint paint, LeasedMark owner)
            : ICustomDrawOperation
        {
            public Rect Bounds => bounds;

            public bool HitTest(Point p) => false;

            public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

            public void Render(ImmediateDrawingContext context)
            {
                var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (feature is null)
                {
                    // Left as a silence the caller reads as zero, which is the honest answer: this
                    // renderer is not Skia and the portable link cannot live on its canvas.
                    return;
                }

                using var lease = feature.Lease();
                owner.SawGpuContext = lease.GrContext is not null;
                owner.LeasedACanvas = true;

                if (paint == Paint.Nothing)
                {
                    return;
                }

                // The plain paint is RED and the shader returns GREEN, and the scan only counts
                // green. An earlier draft painted both in green, and a mutant proved what that
                // cost: deleting the shader assignment left the test green, because Skia simply
                // used the paint's own colour. The probe could not tell «the shader ran» from «the
                // shader was ignored» — and the whole architecture was chosen on that reading.
                using var brush = new SKPaint
                {
                    Color = paint == Paint.Shader ? SKColors.Red : SKColors.Lime,
                    IsAntialias = false,
                };
                if (paint == Paint.Shader)
                {
                    using var effect = SKRuntimeEffect.CreateShader(FlatGreenShader, out _);
                    if (effect is null)
                    {
                        return;
                    }

                    brush.Shader = effect.ToShader();
                }

                lease.SkCanvas.DrawRect(
                    new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height),
                    brush);
            }

            public void Dispose()
            {
            }
        }
    }
}
