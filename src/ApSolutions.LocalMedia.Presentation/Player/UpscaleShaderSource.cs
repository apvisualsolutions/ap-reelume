// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using SkiaSharp;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// The arithmetic PLY-016's portable link runs on the drawing canvas, written as SkSL.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is our own and not somebody else's on purpose.</b> AMD's FSR 1 is the obvious thing to
/// port and its licence permits it, but it also requires attribution in the third-party notices —
/// and <c>ThirdPartyNoticeTests</c> reads the package lock file, so a shader pasted into source
/// would carry an obligation no gate in this tree could see. With the program's own licence three
/// days old and its release already blocked by one copyleft plugin, taking on an invisible licence
/// obligation is the wrong trade. What is here instead is an unsharp mask, which is arithmetic
/// rather than anybody's code.
/// </para>
/// <para>
/// <b>Why an unsharp mask sharpens an enlargement at all</b>: enlarging spreads a hard edge over
/// several destination pixels, and every filter this renderer offers spreads it over the same four
/// — measured in <c>VideoUpscaleQualityTests</c>. Subtracting a blur of the neighbourhood from the
/// centre pushes the light side lighter and the dark side darker, which narrows that spread. The
/// neighbours are stepped by <b>one source pixel</b> and not one destination pixel: at a four-times
/// enlargement a destination-pixel step samples inside the same source pixel four times and
/// subtracts a blur identical to the centre, which is a shader that costs a frame and changes
/// nothing.
/// </para>
/// <para>
/// <b>The result is bounded by its own neighbourhood, and that is the halo made impossible rather
/// than merely small.</b> An unsharp mask on its own can push a pixel past every value around it,
/// which is what an outline drawn around everything is. AMD says the same thing about its own
/// adaptive sharpening in its own words — «areas of the input image that are already sharp are
/// sharpened less … higher overall natural visual sharpness with fewer artifacts» — so the arithmetic
/// here clamps the sharpened pixel between the lightest and darkest of the five it read. Measured: at
/// the shipped settings the edge overshoots <b>16</b> levels with this bound and <b>29</b> without,
/// against a ceiling of 22 that the gate auditor set on 2026-09-13. Deleting the two lines turns that
/// gate red, so this is guarded and not merely intended.
/// </para>
/// <para>
/// The alpha of the centre is returned untouched. A runtime shader returns premultiplied colour, so
/// letting a sharpened channel climb past its own alpha would hand Skia a colour that cannot exist;
/// the second clamp is what keeps it valid rather than tidy.
/// </para>
/// </remarks>
public static class UpscaleShaderSource
{
    /// <summary>The name the shader knows the decoded picture by.</summary>
    public const string SourceChild = "source";

    /// <summary>The name the shader knows one source pixel by, in destination units.</summary>
    public const string StepUniform = "sourceStep";

    /// <summary>The name the shader knows the sharpening strength by.</summary>
    public const string StrengthUniform = "strength";

    /// <summary>
    /// How much of the difference between the centre and its neighbourhood is added back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured rather than chosen by eye, and measured twice.</b> The first reading only had the
    /// ramp width to go on: across a hard edge enlarged four times, 0.3 left the ramp at <b>4</b>
    /// pixels — the composition's own width — while 0.6 brought it to <b>2</b>, profile
    /// <c>3,86,169,252</c>. So 0.6 shipped on 2026-09-13, and the owner said it was still soft.
    /// </para>
    /// <para>
    /// <b>He was right, and the ramp width is why.</b> Its perfect score is zero, which is what
    /// nearest-neighbour gives, so it rewards a hard threshold and cannot say whether the picture
    /// came back <i>faithful</i>. Against a synthesised truth — <c>VideoUpscaleFidelityTests</c> —
    /// this number wants the opposite of what the ramp wanted. Measured in that harness with the
    /// chain as it ships: <b>0.45</b> lands <b>35,7 %</b> closer to the truth than the composition,
    /// 0.6 lands 34,0 %, and 1.0 lands 30,3 %. Over-sharpening does not merely risk a halo; it
    /// measurably moves the picture away from what it should have been.
    /// </para>
    /// <para>
    /// <b>And the ramp is why it is not lower still</b>, which is what keeps this honest rather than
    /// convenient. <b>0.3 scores better</b> — 36,8 % — and it fails: the ramp goes back to the
    /// composition's four and three of the tests next door turn red, measured by putting 0.3 in here
    /// and running them. 0.45 is the highest-scoring value tried that clears <b>every</b> yardstick,
    /// and it leaves the ramp at <b>2</b>. Nothing was loosened to land it.
    /// </para>
    /// </remarks>
    public const float Strength = 0.45f;

    /// <summary>The shader itself.</summary>
    public const string Sksl = """
        uniform shader source;
        uniform float2 sourceStep;
        uniform float strength;

        half4 main(float2 coord) {
            half4 centre = source.eval(coord);
            half4 west  = source.eval(coord + float2(-sourceStep.x, 0.0));
            half4 east  = source.eval(coord + float2( sourceStep.x, 0.0));
            half4 north = source.eval(coord + float2(0.0, -sourceStep.y));
            half4 south = source.eval(coord + float2(0.0,  sourceStep.y));

            half4 blur = (west + east + north + south) * 0.25;
            half4 sharpened = centre + (centre - blur) * half(strength);

            half3 lowest  = min(min(min(west.rgb, east.rgb), min(north.rgb, south.rgb)), centre.rgb);
            half3 highest = max(max(max(west.rgb, east.rgb), max(north.rgb, south.rgb)), centre.rgb);
            half3 bounded = clamp(sharpened.rgb, lowest, highest);

            return half4(clamp(bounded, 0.0, centre.a), centre.a);
        }
        """;

    /// <summary>
    /// Compiles <see cref="Sksl"/>, or <paramref name="sksl"/> where one is given, handing back what
    /// the compiler said if it would not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The error is <b>returned rather than swallowed</b>, and that is the point of the method: a
    /// shader that fails to compile paints nothing, and painting nothing is also what a renderer
    /// with no shader support looks like. Without the text, the two are one silence and the fall to
    /// the next link would be indistinguishable from the feature never having worked.
    /// </para>
    /// <para>
    /// The source is a parameter for one reason: the failing arm cannot be reached with a shader
    /// that compiles, and a branch nothing can take is a branch nobody has seen work. Production
    /// passes nothing and gets <see cref="Sksl"/>.
    /// </para>
    /// </remarks>
    public static SKRuntimeEffect? TryCompile(out string? errorText, string? sksl = null)
    {
        var effect = SKRuntimeEffect.CreateShader(sksl ?? Sksl, out var errors);
        errorText = string.IsNullOrEmpty(errors) ? null : errors;

        return effect;
    }
}
