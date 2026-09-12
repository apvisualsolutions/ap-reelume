// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// What this repository's pixel harness can and cannot see of a GPU route, measured before
/// `PLY-016` builds one on top of it.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia's own documentation warns that <c>RenderTargetBitmap</c> renders in software and that
/// «controls that rely on GPU-specific rendering paths … may not render correctly when captured
/// this way» — and <c>CaptureRenderedFrame</c> is that capture. `PLY-016` ends in a shared D3D11
/// texture imported into the composition, so a gate written on the harness without measuring it
/// first would read a silence as «nothing changed», which is the worst failure this repository
/// knows: a green that means nobody looked.
/// </para>
/// <para>
/// So each question here carries a control that must sound. The mark is pure green on purpose:
/// green sits at byte 1 in both <c>Rgba8888</c> and <c>Bgra8888</c>, so the scan cannot be fooled
/// by the channel order that already cost this tree one wrong reading — and the format is asserted
/// anyway, so the dodge is written down instead of assumed.
/// </para>
/// </remarks>
public sealed class GpuCompositionHarnessTests
{
    /// <summary>The white the scene is painted on.</summary>
    private const byte Paper = 250;

    /// <summary>A green mark reads above this on its own channel and below it on the other two.</summary>
    private const byte Mark = 200;

    private const byte Dark = 100;

    /// <summary>
    /// The control that must sound: an ordinary Avalonia visual leaves ink the scan can count.
    /// </summary>
    /// <remarks>
    /// Without this, every silence below would be indistinguishable from a harness that draws
    /// nothing at all — which is exactly how a blind gate passes.
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

    /// <summary>
    /// The control that must stay silent: an empty scene has no green in it.
    /// </summary>
    /// <remarks>
    /// The counterpart to the one above. A scan that answered «green» on white paper would make
    /// every positive reading here a coincidence with the shape of a measurement.
    /// </remarks>
    [AvaloniaFact]
    public void An_empty_scene_leaves_no_ink_at_all()
    {
        using var scene = new Scene();

        Assert.Equal(0, scene.CountGreen());
    }

    /// <summary>
    /// Whether the harness sees the composition layer at all, which is where `PLY-016` ends.
    /// </summary>
    /// <remarks>
    /// A composition child visual is not drawn by <c>Render</c>: it is handed to the compositor and
    /// composed on the render thread, which is the same door the imported GPU texture will come
    /// through. If a solid colour cannot get through that door in this harness, no video will
    /// either, and the answer has to be written down before anybody builds a gate on it.
    /// </remarks>
    [AvaloniaFact]
    public void A_composition_child_visual_reaches_the_captured_frame()
    {
        using var scene = new Scene();
        scene.AddCompositionMark();
        var found = scene.CountGreen();

        Assert.True(
            found == Scene.MarkArea,
            $"The scan found {found} green pixels where a CompositionSolidColorVisual of exactly "
                + $"{Scene.MarkArea} was attached. Zero means the composition layer does not reach "
                + "CaptureRenderedFrame at all, and any other number means it arrives cropped or "
                + "rescaled — either way a `PLY-016` gate written on this harness would be reading "
                + "something other than the picture, and the chain has to be measured where the "
                + "texture is instead.");
    }

    /// <summary>
    /// Whether the GPU import route exists in this harness at all.
    /// </summary>
    /// <remarks>
    /// Measured on the assembly first, because the documentation is wrong about it: there is no
    /// <c>Compositor.ImportGpuImage</c> anywhere in Avalonia 12.1.1. The route is
    /// <c>TryGetCompositionGpuInterop()</c> → <c>ICompositionGpuInterop.ImportImage</c> →
    /// <c>CompositionDrawingSurface.UpdateWithKeyedMutexAsync</c>, and that last one takes three
    /// arguments and not one.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_headless_harness_reports_whether_gpu_interop_is_reachable()
    {
        using var scene = new Scene();
        var interop = await scene.TryGetGpuInterop();

        Assert.True(
            interop is null,
            "The headless harness now offers GPU interop, which it did not when this was measured. "
                + "That changes where `PLY-016` can be gated: re-measure and rewrite this.");
    }

    private sealed class Scene : IDisposable
    {
        private const int MarkWidth = 100;
        private const int MarkHeight = 40;

        /// <summary>
        /// What a mark of this size has to measure, to the pixel. A threshold of «more than a
        /// thousand» would call a mark arriving at a quarter of its size a success, and «the
        /// composition layer reaches the frame» is exactly the conclusion `PLY-016` inherits.
        /// </summary>
        public const int MarkArea = MarkWidth * MarkHeight;

        private readonly Window _window;
        private readonly Border _host;

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

        public void AddCompositionMark()
        {
            var visual = ElementComposition.GetElementVisual(_host)
                ?? throw new InvalidOperationException("the host has no composition visual.");
            var mark = visual.Compositor.CreateSolidColorVisual();
            mark.Color = Colors.Lime;
            mark.Size = new Vector(MarkWidth, MarkHeight);

            // Sat where the ordinary mark sits, so the two are the same measurement and the paper
            // check below still has a corner of paper to read.
            mark.Offset = new Vector3D(50, 40, 0);
            ElementComposition.SetElementChildVisual(_host, mark);
            Dispatcher.UIThread.RunJobs();
        }

        public async Task<ICompositionGpuInterop?> TryGetGpuInterop()
        {
            var visual = ElementComposition.GetElementVisual(_host)
                ?? throw new InvalidOperationException("the host has no composition visual.");
            var pending = visual.Compositor.TryGetCompositionGpuInterop().AsTask();
            var settled = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.True(
                ReferenceEquals(settled, pending),
                "The compositor never answered whether it has GPU interop, which is neither yes nor "
                    + "no: the render thread is not being pumped and this measurement is void.");

            return await pending;
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

            Assert.True(
                bytes[1] > Paper,
                "The top-left pixel is not the white paper this scene paints, so the capture is not "
                    + "the scene and the count below belongs to something else.");

            return found;
        }

        public void Dispose() => _window.Close();
    }
}
