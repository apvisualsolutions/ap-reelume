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
/// The alpha of the centre is returned untouched. A runtime shader returns premultiplied colour, so
/// letting a sharpened channel climb past its own alpha would hand Skia a colour that cannot exist;
/// the clamp is what keeps it valid rather than tidy.
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
    /// <b>Measured rather than chosen by eye.</b> Across a hard edge enlarged four times, 0.3 leaves
    /// the ramp at <b>4</b> pixels — profile <c>18,91,164,237</c>, which is the composition's own
    /// width and therefore no improvement anybody would see — while 0.6 brings it to <b>2</b>, profile
    /// <c>3,86,169,252</c>. So this number is the difference between clearing the gate and not
    /// clearing it, not a taste.
    /// </para>
    /// <para>
    /// <b>What that measurement cannot see is a halo</b>, and it is worth saying so rather than
    /// implying otherwise: the scene is black against white, so an overshoot past either end clamps
    /// invisibly. Whether a stronger value would draw an outline around everything is the owner's
    /// visual judgement on real material, which the acceptance criterion asks for by name.
    /// </para>
    /// </remarks>
    public const float Strength = 0.6f;

    /// <summary>The shader itself.</summary>
    public const string Sksl = """
        uniform shader source;
        uniform float2 sourceStep;
        uniform float strength;

        half4 main(float2 coord) {
            half4 centre = source.eval(coord);
            half4 blur = (source.eval(coord + float2(-sourceStep.x, 0.0))
                        + source.eval(coord + float2( sourceStep.x, 0.0))
                        + source.eval(coord + float2(0.0, -sourceStep.y))
                        + source.eval(coord + float2(0.0,  sourceStep.y))) * 0.25;
            half4 sharpened = centre + (centre - blur) * half(strength);
            return half4(clamp(sharpened.rgb, 0.0, centre.a), centre.a);
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
