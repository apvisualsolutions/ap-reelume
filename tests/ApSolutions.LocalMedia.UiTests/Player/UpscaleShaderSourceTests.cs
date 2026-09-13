// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// That PLY-016's sharpening shader compiles, and that a shader which does not says why.
/// </summary>
/// <remarks>
/// The second half is what the first one is worth anything without: a shader that fails to compile
/// draws nothing, and drawing nothing is also what a renderer with no shader support looks like. If
/// the compiler's complaint were swallowed, the fall to the cubic link would be indistinguishable
/// from the feature never having worked on that machine.
/// </remarks>
public sealed class UpscaleShaderSourceTests
{
    /// <summary>The shader this feature ships compiles on this renderer.</summary>
    [AvaloniaFact]
    public void The_sharpening_shader_compiles_and_reports_nothing_to_complain_about()
    {
        using var effect = UpscaleShaderSource.TryCompile(out var errorText);

        Assert.Null(errorText);
        Assert.NotNull(effect);
    }

    /// <summary>A shader that will not compile hands back the compiler's own words.</summary>
    /// <remarks>
    /// Asserted on the text and not just on the null, because the text is the whole reason the method
    /// has an out parameter. Somebody reading a log has to be able to tell «the driver rejected this»
    /// from «this machine has no shaders».
    /// </remarks>
    [AvaloniaFact]
    public void A_shader_that_will_not_compile_hands_back_what_the_compiler_said()
    {
        using var effect = UpscaleShaderSource.TryCompile(
            out var errorText,
            "half4 main(float2 coord) { return esto_no_es_sksl(0.0); }");

        Assert.Null(effect);
        Assert.False(
            string.IsNullOrWhiteSpace(errorText),
            "The shader was refused without a word about why, so a log would say only that nothing "
                + "was drawn.");
    }

    /// <summary>
    /// The shader names the three things the drawing operation feeds it, spelled the same way.
    /// </summary>
    /// <remarks>
    /// A uniform set under a name the shader does not declare is not an error anybody sees: Skia
    /// ignores it and the pass runs with a zero step, which draws the enlargement unchanged. So the
    /// names are asserted against the source text rather than trusted to two files agreeing.
    /// </remarks>
    [AvaloniaFact]
    public void The_shader_declares_the_three_names_the_operation_feeds_it()
    {
        Assert.Contains($"uniform shader {UpscaleShaderSource.SourceChild};", UpscaleShaderSource.Sksl, StringComparison.Ordinal);
        Assert.Contains($"uniform float2 {UpscaleShaderSource.StepUniform};", UpscaleShaderSource.Sksl, StringComparison.Ordinal);
        Assert.Contains($"uniform float {UpscaleShaderSource.StrengthUniform};", UpscaleShaderSource.Sksl, StringComparison.Ordinal);
    }

    /// <summary>
    /// The strength is inside the range where it is a sharpening and not an outline.
    /// </summary>
    /// <remarks>
    /// Zero draws the enlargement unchanged, which would leave the ramp gate as the only thing
    /// noticing; past one the overshoot reads as a drawn edge around everything. The ramp gate is what
    /// proves the number works — this is what stops it being edited to a value that happens to keep
    /// that gate green while looking wrong.
    /// </remarks>
    [AvaloniaFact]
    public void The_sharpening_strength_is_a_sharpening_and_not_an_outline()
    {
        Assert.InRange(UpscaleShaderSource.Strength, 0.1f, 1f);
    }
}
