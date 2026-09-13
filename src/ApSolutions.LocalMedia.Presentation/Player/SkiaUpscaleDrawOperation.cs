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
    /// <para>
    /// <b>A sharpened cubic, and the coefficients are measured rather than named.</b> This read
    /// <c>SKFilterMode.Linear</c> until 2026-09-13, on the argument that the shader's own arithmetic
    /// narrows the edge and two sharpenings stacked overshoot into an outline. The argument was
    /// sound and the conclusion was wrong: measured against a synthesised truth in
    /// <c>VideoUpscaleFidelityTests</c>, feeding the mask a linear sample lands <b>30,4 %</b> closer
    /// to the truth than the composition, and feeding it this cubic lands <b>35,7 %</b> closer — with
    /// the sharpening cut from 0.6 to 0.45, and a <i>smaller</i> overshoot than before, 16 levels
    /// against 17.
    /// </para>
    /// <para>
    /// <b>Why the coefficients and not the named preset.</b> The family is Mitchell-Netravali's, where
    /// <c>B = 0, C = 0.5</c> is Catmull-Rom and a larger <c>C</c> deepens the negative lobes that
    /// steepen an edge; the constructor takes the two coefficients, so the whole family is reachable
    /// and not just the two fields — measured by reflection, because a preset-only API would have made
    /// this paragraph fiction.
    /// </para>
    /// <para>
    /// <b>The lobes ring, and the shader's bound cannot remove it</b>, because the ring is created by
    /// the resample and is therefore already inside the neighbourhood the bound clamps to. Measured
    /// across a grey edge — flat levels 64 and 192 — the step beside the edge scales straight with
    /// <c>C</c>: 9 levels at 0.5, 13 at 0.7, <b>16 here</b>; and so does the fidelity it buys, 28,1 % /
    /// 33,2 % / 35,7 %. The ceiling in <c>SkiaUpscaleDrawOperationTests</c> is what holds it.
    /// </para>
    /// <para>
    /// <b>This was 0.7 for half an hour, on a diagnosis that turned out to be wrong, and the story is
    /// worth more than the number.</b> The owner reported «alrededor de las letras se ven cuadraditos o
    /// de distintos tonos», this ring was the obvious suspect, and the coefficient came down twice
    /// chasing it — with a gate loosened on the way. Then he ran the control nobody here had thought
    /// to run: <b>his gamma was at 1.5, and at 1.0 the squares stop</b>. The artefact was banding from
    /// an 8-bit tone curve (`ENG-021`), not this. So the coefficient is back at the value that measures
    /// best, and the lesson is that <b>a report of something seen is a symptom and not a diagnosis</b>:
    /// the suspect gets switched off before anything is tuned.
    /// </para>
    /// <para>
    /// <b>Two other designs were measured while chasing that ghost, and both are worse — which is the
    /// useful part of the detour.</b> Nothing that cannot leave the local range gets past about 13 %
    /// closer to the truth: linear sampling with any strength reaches 13,7 %, and a monotone remap of
    /// the local range — steepening where the centre sits between its neighbours, which cannot overshoot
    /// by construction — reaches 13,4 % with a step of exactly <b>0</b>. Binding the sharpening to a
    /// second linear copy of the frame with a margin buys 25,7 % at a step of 10, against this 35,7 %.
    /// <b>All the sharpness above 13 % comes from the negative lobes, and so does the step.</b> Getting
    /// both needs a different algorithm — interpolating along an edge rather than across it — and that
    /// is `ENG-022`, not a coefficient.
    /// </para>
    /// </remarks>
    public static SKSamplingOptions SharpeningSource => new(new SKCubicResampler(0f, 0.85f));

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
