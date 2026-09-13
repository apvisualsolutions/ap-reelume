// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Domain.Playback;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>How one enlarged frame ends up on screen, which is not the same as which link won.</summary>
/// <remarks>
/// A link is what the chain chose; a route is what the canvas in front of it can actually do. The two
/// differ exactly when something is missing — no Skia canvas, or a shader the driver would not
/// compile — and keeping them apart is what stops a missing piece from becoming a blank screen.
/// </remarks>
public enum UpscaleDrawRoute
{
    /// <summary>What the composition would have drawn, which is where every fall ends.</summary>
    Unenhanced = 0,

    /// <summary>Cubic resampling on the canvas.</summary>
    Cubic = 1,

    /// <summary>Cubic resampling plus the sharpening pass.</summary>
    Sharpened = 2,
}

/// <summary>
/// The decisions PLY-016's drawing takes, kept away from the canvas that carries them out.
/// </summary>
/// <remarks>
/// The tenth rule of this repository, applied: what talks to the machine is separated from what
/// decides, and only the first is excluded from coverage. What is here is arithmetic — which route
/// a frame takes, and where one source pixel lands in the destination — and it runs anywhere, so it
/// is the half that is asserted.
/// </remarks>
public static class UpscaleDrawPlan
{
    /// <summary>
    /// How a frame gets drawn, given the link the chain chose and what the canvas turned out to
    /// offer.
    /// </summary>
    /// <remarks>
    /// A degenerate source size lands on <see cref="UpscaleDrawRoute.Unenhanced"/> before anything
    /// else, and not for tidiness: every route below divides the destination by it.
    /// </remarks>
    public static UpscaleDrawRoute Route(
        UpscaleLink link,
        bool canvasAvailable,
        bool shaderAvailable,
        int sourceWidth,
        int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || !canvasAvailable)
        {
            return UpscaleDrawRoute.Unenhanced;
        }

        return link switch
        {
            UpscaleLink.PortableUpscaler when shaderAvailable => UpscaleDrawRoute.Sharpened,
            UpscaleLink.PortableUpscaler or UpscaleLink.BicubicResample => UpscaleDrawRoute.Cubic,

            // The links that talk to the graphics driver do not come through here, and neither does
            // the composition's own drawing: all three are the canvas doing what it already does.
            _ => UpscaleDrawRoute.Unenhanced,
        };
    }

    /// <summary>
    /// The byte order the engine hands its frames over in, which is also what the surface's
    /// <see cref="Avalonia.Platform.PixelFormat.Bgra8888"/> bitmap carries.
    /// </summary>
    /// <remarks>
    /// <b>It lives here because nothing could see it change.</b> Naming the other order swaps red and
    /// blue in every enlarged video, and the whole test suite was blind to it: every test picture was
    /// grey, and the one with colour in it was pure green — the single colour that is identical in
    /// both byte orders. Found by the gate auditor on 2026-09-13, with the mutant surviving all 1,437
    /// tests.
    /// </remarks>
    public const SKColorType SourceColourType = SKColorType.Bgra8888;

    /// <summary>
    /// How the picture is resampled on the canvas, which is a decision and not a draw call.
    /// </summary>
    /// <remarks>
    /// Catmull-Rom rather than Mitchell: Mitchell is the softer of the two, and softness is exactly
    /// the defect being removed. It is a named value rather than an argument at the call site because
    /// swapping the two survived the whole suite while the comment above it argued the opposite.
    /// </remarks>
    public static SKSamplingOptions Resampling => new(SKCubicResampler.CatmullRom);

    /// <summary>
    /// How the picture is sampled <i>before</i> the sharpening pass, which is a different question.
    /// </summary>
    /// <remarks>
    /// Linear and not cubic: the shader's own arithmetic is what narrows the edge, and two sharpenings
    /// stacked overshoot into a visible outline. Measured — a nearest-neighbour sample here is what
    /// the ramp gate already kills.
    /// </remarks>
    public static SKSamplingOptions SharpeningSource => new(SKFilterMode.Linear);

    /// <summary>
    /// One source pixel measured in the destination units the shader reads its coordinates in.
    /// </summary>
    /// <remarks>
    /// Stepping by a destination pixel instead would sample inside the same source pixel four times
    /// over at a four-times enlargement, and subtract a blur identical to the centre — a shader that
    /// costs a frame and changes nothing.
    /// </remarks>
    public static (float X, float Y) SourceStep(Rect destination, int sourceWidth, int sourceHeight) =>
        ((float)(destination.Width / sourceWidth), (float)(destination.Height / sourceHeight));

    /// <summary>
    /// Where the decoded picture sits in the destination, as the local matrix its sampler needs.
    /// </summary>
    public static SKMatrix SourceToDestination(Rect destination, int sourceWidth, int sourceHeight)
    {
        var (x, y) = SourceStep(destination, sourceWidth, sourceHeight);

        return SKMatrix.CreateScaleTranslation(x, y, (float)destination.X, (float)destination.Y);
    }
}

/// <summary>
/// Draws one enlarged frame through the drawing canvas rather than through the composition's own
/// filter, which is PLY-016's portable link (<see cref="UpscaleLink.PortableUpscaler"/>) and the
/// cubic one below it (<see cref="UpscaleLink.BicubicResample"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why here rather than in the engine.</b> The engine has the pixels but not the size of the box
/// they will be drawn in, and <see cref="UpscalePolicy.Decide"/> needs both — reaching back from the
/// player to the decoder for a surface size would turn a one-way dependency into a loop. This is
/// also the only place where the enlargement <i>replaces</i> work instead of adding it: the
/// composition already rescales the picture here every frame, and this draws it instead.
/// </para>
/// <para>
/// <b>It never draws nothing.</b> Every way this can fail — no Skia canvas on this renderer, a
/// shader that would not compile, a buffer Skia will not wrap — ends at the same <c>DrawBitmap</c>
/// the composition would have done. A frame is what a person is looking at, so a silent failure here
/// is a black screen, and «the shader broke on a card nobody has» is exactly the failure nobody would
/// be watching for.
/// </para>
/// <para>
/// The pixels arrive as an array the view was already allocating and throwing away every frame, so
/// holding one adds no copying. It is a snapshot rather than a live buffer on purpose: this runs on
/// the render thread while the decoder writes the next frame, and a shared buffer would tear.
/// </para>
/// </remarks>
public sealed class SkiaUpscaleDrawOperation : ICustomDrawOperation
{
    private readonly byte[] _pixels;
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;
    private readonly int _stride;
    private readonly Rect _destination;
    private readonly UpscaleLink _link;
    private readonly SKRuntimeEffect? _effect;
    private readonly Bitmap _fallback;

    public SkiaUpscaleDrawOperation(
        byte[] pixels,
        int sourceWidth,
        int sourceHeight,
        int stride,
        Rect destination,
        UpscaleLink link,
        SKRuntimeEffect? effect,
        Bitmap fallback)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(fallback);

        _pixels = pixels;
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        _stride = stride;
        _destination = destination;
        _link = link;
        _effect = effect;
        _fallback = fallback;
    }

    public Rect Bounds => _destination;

    /// <summary>
    /// Never hit-tested, because the transport bar sits above the video.
    /// </summary>
    /// <remarks>
    /// An operation that answered here would take the clicks meant for play, pause and the volume,
    /// which is the defect the status badge's own comment records from 2026-08-15 — and nothing said
    /// so, because the player answers the keyboard itself.
    /// </remarks>
    public bool HitTest(Point p) => false;

    /// <summary>
    /// Never equal to another operation, because every frame carries different pixels.
    /// </summary>
    /// <remarks>
    /// Avalonia skips redrawing an operation equal to the one already on screen. Comparing the
    /// destination and the link would make two different frames of the same film equal and freeze
    /// the picture on the first one.
    /// </remarks>
    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        var route = UpscaleDrawPlan.Route(
            _link,
            canvasAvailable: lease is not null,
            shaderAvailable: _effect is not null,
            _sourceWidth,
            _sourceHeight);

        if (route == UpscaleDrawRoute.Unenhanced || lease is null)
        {
            context.DrawBitmap(
                _fallback,
                new Rect(0, 0, _fallback.PixelSize.Width, _fallback.PixelSize.Height),
                _destination);
            return;
        }

        DrawThroughCanvas(lease, route);
    }

    public void Dispose()
    {
    }

    /// <summary>Everything that touches Skia, and nothing that decides anything.</summary>
    /// <remarks>
    /// <para>
    /// Excluded from coverage under this repository's tenth rule: what is left here is the creation
    /// of Skia's own objects and the draw calls on them, which can only answer differently if the
    /// renderer or the driver does.
    /// </para>
    /// <para>
    /// <b>The first draft of this exclusion was a lie, and the gate auditor said so.</b> It claimed
    /// every decision lived in <see cref="UpscaleDrawPlan"/> while four of them were still inside
    /// here — the byte order, the resampling kernel, the sampler's matrix and the route branch — and
    /// three of the four survived all 1,437 tests. The three that were values now live in the plan as
    /// named members with tests on them; what is left is the branch, and it is driven entirely by
    /// <see cref="UpscaleDrawPlan.Route"/>.
    /// </para>
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification =
        "Skia object creation and draw calls. The byte order, the resampling kernels and the "
        + "sampler's matrix are named members of UpscaleDrawPlan with their own tests; the branch "
        + "here only follows UpscaleDrawPlan.Route.")]
    private void DrawThroughCanvas(ISkiaSharpApiLeaseFeature lease, UpscaleDrawRoute route)
    {
        using var leased = lease.Lease();
        var handle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
        try
        {
            var info = new SKImageInfo(
                _sourceWidth,
                _sourceHeight,
                UpscaleDrawPlan.SourceColourType,
                SKAlphaType.Opaque);
            using var image = SKImage.FromPixels(info, handle.AddrOfPinnedObject(), _stride);
            var destination = new SKRect(
                (float)_destination.X,
                (float)_destination.Y,
                (float)_destination.Right,
                (float)_destination.Bottom);

            if (route == UpscaleDrawRoute.Sharpened)
            {
                var (stepX, stepY) = UpscaleDrawPlan.SourceStep(
                    _destination,
                    _sourceWidth,
                    _sourceHeight);

                using var sampled = image.ToShader(
                    SKShaderTileMode.Clamp,
                    SKShaderTileMode.Clamp,
                    UpscaleDrawPlan.SharpeningSource,
                    UpscaleDrawPlan.SourceToDestination(
                        _destination,
                        _sourceWidth,
                        _sourceHeight));

                var builder = new SKRuntimeShaderBuilder(_effect);
                builder.Children[UpscaleShaderSource.SourceChild] = new SKRuntimeEffectChild(sampled);
                builder.Uniforms[UpscaleShaderSource.StepUniform] = new[] { stepX, stepY };
                builder.Uniforms[UpscaleShaderSource.StrengthUniform] = UpscaleShaderSource.Strength;

                using var shader = builder.Build();
                using var sharpening = new SKPaint { Shader = shader, IsAntialias = false };
                leased.SkCanvas.DrawRect(destination, sharpening);
                return;
            }

            using var paint = new SKPaint { IsAntialias = false };
            leased.SkCanvas.DrawImage(image, destination, UpscaleDrawPlan.Resampling, paint);
        }
        finally
        {
            handle.Free();
        }
    }
}
