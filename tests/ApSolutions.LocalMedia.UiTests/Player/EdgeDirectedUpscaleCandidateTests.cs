// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia;
using SkiaSharp;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The edge-directed enlargement `ENG-022` asked for, measured against what ships and left unbuilt
/// because of what the measurement said.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was asked.</b> The shipping chain — a sharpened cubic under a bounded mask — lands
/// 35,7 % closer to the truth than the composition, and pays for it with a step of 16 levels beside
/// every edge. The record said that nothing unable to leave the local range got past 13 %, so
/// sharpness without the step would need interpolating <i>along</i> an edge instead of across it.
/// </para>
/// <para>
/// <b>What the measurement answered, on 2026-09-25, in two halves.</b> The 13 % ceiling was a
/// property of <i>which</i> range the result was bound to, not of bounding: the shipping shader
/// clamps to five samples that were already resampled by the sharpened cubic, so the ring is inside
/// the range before the clamp sees it. Bounding to the four <b>source texels</b> around the point
/// instead removes the step by construction and still reaches 30 % with a plain cubic. The
/// candidate here — a Lanczos-2 kernel steered by the local structure tensor, stretched along the
/// edge — reaches <b>34,9 %</b> with a step of <b>0</b> and the same ramp of 2.
/// </para>
/// <para>
/// <b>And it is not built, because 34,9 is below 35,7</b> and the step it removes is inside the
/// ceiling the owner never objected to. It also costs more: on the software canvas, 5,1 s per 4K
/// frame against the shipping chain's 0,9 s, and the card's figure cannot be read without a window.
/// So the tests below keep the decision honest in both directions: the first proves this harness
/// measures what the player measures, and the last fails the day either side moves enough to make
/// the question worth asking again.
/// </para>
/// <para>
/// <b>Algorithms, not anybody's code</b>: Lanczos windowing and the structure-tensor steering of a
/// kernel are textbook, and FSR's own edge-adaptive pass is not copied, for the attribution reason
/// <see cref="UpscaleShaderSource"/> already records. Everything else measured on the way — cubics
/// bounded to the texels, a steepened cubic, masks against a bilinear copy, Lanczos-3 bounded and
/// unbounded — is in <c>docs/evidence/stable/ENG022-edge-directed-upscale.md</c> with every figure,
/// and so is why DCCI and NEDI were not: both are defined on a two-times lattice, and a player
/// enlarges by whatever the window asks.
/// </para>
/// </remarks>
public sealed class EdgeDirectedUpscaleCandidateTests
{
    /// <summary>The grey edge and the hard edge the step and the ramp are read across.</summary>
    private const int EdgeSourceWidth = 160;
    private const int EdgeSourceHeight = 90;
    private const int EdgeWidth = 640;
    private const int EdgeHeight = 360;

    /// <summary>
    /// The candidate: a Lanczos-2 kernel over the 4×4 source texels, steered by the local structure
    /// tensor, and bounded to the 2×2 texels around the point.
    /// </summary>
    /// <remarks>
    /// The child is sampled <b>nearest, in source pixels</b>, so every tap is a real texel and the
    /// bound is taken over values the decoder produced — which is the whole difference from the
    /// shipping shader. <c>0.75</c> is how far the kernel stretches along a clear edge and <c>3</c>
    /// how steep a gradient has to be to count as one; both are the best of a sweep, 34,5 to 34,9 %
    /// across their neighbours.
    /// </remarks>
    private const string Candidate = """
        uniform shader source;
        uniform float2 invScale;

        float sinc(float x) { if (abs(x) < 1e-4) { return 1.0; } float a = 3.14159265 * x; return sin(a) / a; }
        float lanczos2(float x) { if (abs(x) >= 2.0) { return 0.0; } return sinc(x) * sinc(x * 0.5); }
        float luma(float4 c) { return dot(c.rgb, float3(0.299, 0.587, 0.114)); }

        half4 main(float2 coord) {
            float2 p = coord * invScale - 0.5;
            float2 base = floor(p);
            float2 f = p - base;
            float4 t[16];
            for (int j = 0; j < 4; j++) {
                for (int i = 0; i < 4; i++) {
                    t[j * 4 + i] = source.eval(base + float2(float(i - 1), float(j - 1)) + 0.5);
                }
            }

            float2 g00 = float2(luma(t[6]) - luma(t[4]), luma(t[9]) - luma(t[1])) * 0.5;
            float2 g10 = float2(luma(t[7]) - luma(t[5]), luma(t[10]) - luma(t[2])) * 0.5;
            float2 g01 = float2(luma(t[10]) - luma(t[8]), luma(t[13]) - luma(t[5])) * 0.5;
            float2 g11 = float2(luma(t[11]) - luma(t[9]), luma(t[14]) - luma(t[6])) * 0.5;
            float3 st = float3(g00.x * g00.x, g00.x * g00.y, g00.y * g00.y) * (1.0 - f.x) * (1.0 - f.y)
                + float3(g10.x * g10.x, g10.x * g10.y, g10.y * g10.y) * f.x * (1.0 - f.y)
                + float3(g01.x * g01.x, g01.x * g01.y, g01.y * g01.y) * (1.0 - f.x) * f.y
                + float3(g11.x * g11.x, g11.x * g11.y, g11.y * g11.y) * f.x * f.y;

            float tr = st.x + st.z;
            float disc = sqrt(max(tr * tr * 0.25 - (st.x * st.z - st.y * st.y), 0.0));
            float l1 = tr * 0.5 + disc;
            float l2 = max(tr * 0.5 - disc, 0.0);
            float2 dir = float2(1, 0);
            if (abs(st.y) > 1e-7) { dir = normalize(float2(l1 - st.z, st.y)); }
            else if (st.z > st.x) { dir = float2(0, 1); }
            float coherence = (l1 + l2) > 1e-7 ? (sqrt(l1) - sqrt(l2)) / (sqrt(l1) + sqrt(l2)) : 0.0;
            float along = 1.0 / (1.0 + 0.75 * clamp(sqrt(l1) * 3.0, 0.0, 1.0) * coherence);
            float2 perp = float2(-dir.y, dir.x);

            float4 sum = float4(0);
            float weights = 0.0;
            for (int j = 0; j < 4; j++) {
                for (int i = 0; i < 4; i++) {
                    float2 d = float2(float(i - 1), float(j - 1)) - f;
                    float u = dot(d, dir);
                    float v = dot(d, perp) * along;
                    float w = lanczos2(sqrt(u * u + v * v));
                    sum += t[j * 4 + i] * w;
                    weights += w;
                }
            }

            float4 lowest = min(min(t[5], t[6]), min(t[9], t[10]));
            float4 highest = max(max(t[5], t[6]), max(t[9], t[10]));
            return half4(clamp(sum / weights, lowest, highest).rgb, 1.0);
        }
        """;

    /// <summary>
    /// The control: drawn straight on a canvas, the composition and the shipping chain land where
    /// the player's own harness puts them.
    /// </summary>
    /// <remarks>
    /// Without this, the ranking below compares a candidate drawn here against a chain drawn here,
    /// and a harness that drew both wrong the same way would still rank them. The figures are the
    /// ones <see cref="VideoUpscaleFidelityTests"/> and <c>SkiaUpscaleDrawOperationTests</c> archive
    /// through a real <see cref="VideoFrameView"/>, and this harness reproduced every one of them
    /// exactly: 21,70, 35,7 %, a step of 16 and a ramp of 2.
    /// </remarks>
    [Fact]
    public void The_canvas_harness_reproduces_what_the_player_measures()
    {
        var truth = UpscaleTruth.Draw();
        var composition = Distance(truth, Composition);
        var shipping = Distance(truth, Shipping);

        Assert.InRange(composition, 21.65, 21.75);
        Assert.InRange(Gained(composition, shipping), 35.6, 35.8);
        Assert.Equal(16, Step(Shipping));
        Assert.Equal(2, Ramp(Shipping));
        Assert.Equal(4, Ramp(Composition));
    }

    /// <summary>
    /// What the candidate is for: the step beside an edge is gone, and the edge is no wider for it.
    /// </summary>
    /// <remarks>
    /// The step is zero by construction — the result cannot leave the range of the four texels it
    /// sits between — and that is exactly why it needs a test: a bound that is taken over the wrong
    /// four values, or dropped, reads <b>29</b> here, measured by deleting it on 2026-09-25 — and the
    /// same mutant lands 42,2 % closer to the truth, which is the trade the bound exists to refuse.
    /// </remarks>
    [Fact]
    public void The_edge_directed_candidate_removes_the_step_without_widening_the_edge()
    {
        Assert.Equal(0, Step(EdgeDirected));
        Assert.Equal(2, Ramp(EdgeDirected));
    }

    /// <summary>
    /// The decision, stated as the gap that holds it: the candidate is close to what ships and not
    /// better than it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It fails in both directions on purpose. If the candidate ever lands closer to the truth than
    /// what ships — because the shipping chain got softer, or the candidate was improved — then it
    /// wins on every yardstick at once and `ENG-022`'s answer changes. If it leaves 34,9 %, it has
    /// stopped being the alternative the evidence describes, and the record would be pointing at a
    /// figure nobody can reproduce. The band is as narrow as the control's on purpose: the gate
    /// auditor found that a floor of 33 let a candidate with its coherence dropped (33,9 %) or one
    /// gradient mis-indexed (34,5 %) pass all three, measured on 2026-09-25.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_edge_directed_candidate_lands_just_short_of_what_ships()
    {
        var truth = UpscaleTruth.Draw();
        var composition = Distance(truth, Composition);
        var shipping = Gained(composition, Distance(truth, Shipping));
        var candidate = Gained(composition, Distance(truth, EdgeDirected));

        Assert.True(
            candidate < shipping,
            $"The edge-directed candidate lands {candidate:F1} % closer to the truth and what ships "
            + $"lands {shipping:F1} %. It removes the step as well, so it now wins on every yardstick "
            + "and ENG-022's decision not to build it has to be taken again.");
        Assert.True(
            candidate is > 34.8 and < 35.0,
            $"The candidate lands {candidate:F2} % closer, against 34,9 % when ENG-022 was decided, "
            + "so it is no longer the alternative the evidence describes.");
    }

    private static double Gained(double composition, double enhanced) =>
        (composition - enhanced) / composition * 100;

    /// <summary>What the composition draws: the bitmap, sampled linearly.</summary>
    private static void Composition(SKImage image, SKCanvas canvas, int width, int height)
    {
        using var paint = new SKPaint();
        canvas.DrawImage(
            image,
            new SKRect(0, 0, width, height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
            paint);
    }

    /// <summary>
    /// What ships, assembled from the production members rather than a copy of them, so a change to
    /// the chain is a change to this control.
    /// </summary>
    private static void Shipping(SKImage image, SKCanvas canvas, int width, int height)
    {
        var destination = new Rect(0, 0, width, height);
        var (stepX, stepY) = UpscaleDrawPlan.SourceStep(destination, image.Width, image.Height);
        using var effect = UpscaleShaderSource.TryCompile(out var errors);
        Assert.True(effect is not null, errors);
        using var sampled = image.ToShader(
            SKShaderTileMode.Clamp,
            SKShaderTileMode.Clamp,
            UpscaleDrawPlan.SharpeningSource,
            UpscaleDrawPlan.SourceToDestination(destination, image.Width, image.Height));

        var builder = new SKRuntimeShaderBuilder(effect);
        builder.Children[UpscaleShaderSource.SourceChild] = new SKRuntimeEffectChild(sampled);
        builder.Uniforms[UpscaleShaderSource.StepUniform] = new[] { stepX, stepY };
        builder.Uniforms[UpscaleShaderSource.StrengthUniform] = UpscaleShaderSource.Strength;
        Fill(canvas, builder, width, height);
    }

    /// <summary>The candidate, reading real texels through a nearest sampler.</summary>
    private static void EdgeDirected(SKImage image, SKCanvas canvas, int width, int height)
    {
        using var effect = UpscaleShaderSource.TryCompile(out var errors, Candidate);
        Assert.True(effect is not null, errors);
        using var texels = image.ToShader(
            SKShaderTileMode.Clamp,
            SKShaderTileMode.Clamp,
            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));

        var builder = new SKRuntimeShaderBuilder(effect);
        builder.Children["source"] = new SKRuntimeEffectChild(texels);
        builder.Uniforms["invScale"] = new[] { (float)image.Width / width, (float)image.Height / height };
        Fill(canvas, builder, width, height);
    }

    private static void Fill(SKCanvas canvas, SKRuntimeShaderBuilder builder, int width, int height)
    {
        using var shader = builder.Build();
        using var paint = new SKPaint { Shader = shader, IsAntialias = false };
        canvas.DrawRect(new SKRect(0, 0, width, height), paint);
    }

    /// <summary>Mean absolute distance per pixel to the truth, 0 to 255.</summary>
    private static double Distance(byte[] truth, Action<SKImage, SKCanvas, int, int> draw)
    {
        var drawn = Render(
            UpscaleTruth.Shrink(truth),
            UpscaleTruth.SourceWidth,
            UpscaleTruth.SourceHeight,
            UpscaleTruth.Width,
            UpscaleTruth.Height,
            draw);

        var total = 0L;
        for (var index = 0; index < drawn.Length; index++)
        {
            total += Math.Abs(drawn[index] - truth[index]);
        }

        return (double)total / drawn.Length;
    }

    /// <summary>
    /// How far past the flat levels a grey edge goes, 64 against 192 — the same edge and the same
    /// reading as the overshoot ceiling in <c>SkiaUpscaleDrawOperationTests</c>.
    /// </summary>
    private static int Step(Action<SKImage, SKCanvas, int, int> draw)
    {
        var row = Row(Edge(64, 192), draw);

        return Math.Max(64 - row.Min(), row.Max() - 192);
    }

    /// <summary>
    /// How many pixels across a hard black-to-white edge come back neither black nor white, over
    /// the same twenty pixels <c>VideoUpscaleQualityTests</c> reads.
    /// </summary>
    private static int Ramp(Action<SKImage, SKCanvas, int, int> draw) =>
        Row(Edge(0, 255), draw)
            .Skip((EdgeWidth / 2) - 10)
            .Take(20)
            .Count(value => value > 8 && value < 247);

    private static byte[] Row(byte[] edge, Action<SKImage, SKCanvas, int, int> draw) =>
        Render(edge, EdgeSourceWidth, EdgeSourceHeight, EdgeWidth, EdgeHeight, draw)
            .Skip(EdgeHeight / 2 * EdgeWidth)
            .Take(EdgeWidth)
            .ToArray();

    private static byte[] Edge(byte dark, byte light)
    {
        var edge = new byte[EdgeSourceWidth * EdgeSourceHeight];
        for (var index = 0; index < edge.Length; index++)
        {
            edge[index] = index % EdgeSourceWidth < EdgeSourceWidth / 2 ? dark : light;
        }

        return edge;
    }

    /// <summary>The green channel of a grey picture enlarged by <paramref name="draw"/>.</summary>
    private static byte[] Render(
        byte[] grey,
        int sourceWidth,
        int sourceHeight,
        int width,
        int height,
        Action<SKImage, SKCanvas, int, int> draw)
    {
        var pixels = new byte[grey.Length * 4];
        for (var index = 0; index < grey.Length; index++)
        {
            pixels[index * 4] = grey[index];
            pixels[(index * 4) + 1] = grey[index];
            pixels[(index * 4) + 2] = grey[index];
            pixels[(index * 4) + 3] = 255;
        }

        using var image = SKImage.FromPixelCopy(
            new SKImageInfo(sourceWidth, sourceHeight, UpscaleDrawPlan.SourceColourType, SKAlphaType.Opaque),
            pixels);
        using var surface = SKSurface.Create(
            new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        draw(image, surface.Canvas, width, height);

        using var snapshot = surface.Snapshot();
        using var drawn = snapshot.PeekPixels();
        var bytes = drawn.GetPixelSpan();
        var green = new byte[width * height];
        for (var index = 0; index < green.Length; index++)
        {
            green[index] = bytes[(index * 4) + 1];
        }

        return green;
    }
}
