// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// The colour space a packed picture was encoded in.
/// </summary>
public enum YuvColourSpace
{
    /// <summary>Standard definition, the matrix of Rec. ITU-R BT.601.</summary>
    Bt601 = 0,

    /// <summary>High definition, the matrix of Rec. ITU-R BT.709.</summary>
    Bt709 = 1,

    /// <summary>Ultra high definition and HDR10, the non-constant luminance matrix of Rec. ITU-R BT.2020.</summary>
    Bt2020 = 2,
}

/// <summary>
/// The fixed-point coefficients that turn one limited-range Y'CbCr sample into full-range RGB,
/// all scaled by 256 so the conversion is integer arithmetic and a shift.
/// </summary>
/// <remarks>
/// Luma carries the 255/219 gain of limited range, and every chroma coefficient the 255/224 one, so
/// the offsets the caller subtracts — 16 from luma, 128 from both chroma channels — are the whole of
/// the range handling. Measured against the exact formula over every valid sample of both matrices,
/// no coefficient here is ever more than one level out.
/// </remarks>
public readonly record struct YuvColourMatrix(
    int Luma,
    int RedFromV,
    int GreenFromU,
    int GreenFromV,
    int BlueFromU);

/// <summary>
/// Which matrix decodes a picture. Standard definition and high definition have used different ones
/// since high definition existed, and decoding one with the other is not a subtle shift: measured on
/// 2026-09-12 on the samples the suite generates, pure red came back as 231 instead of 253, and a
/// standard definition picture run through the high definition matrix picks up 25 of green.
/// </summary>
/// <remarks>
/// <para>
/// What the file declares decides when it declares anything, and the height decides when it does
/// not. LibVLC 3 publishes a video track's geometry, aspect, cadence and orientation and no colour
/// information at all, so the declaration is read out of the container itself — Matroska's
/// <c>Colour</c> element or the MP4 <c>colr</c> box — as a <see cref="SourceColour"/>. Until
/// 2026-09-27 nothing read it, and an HDR10 film, which is BT.2020, was decoded with BT.709.
/// </para>
/// <para>
/// Without a declaration the rule is the industry's own: 720 lines and above is BT.709, below it is
/// BT.601. A file that disagrees with its own height and says nothing is rare, and its error is
/// smaller than the one the rule replaced.
/// </para>
/// </remarks>
public static class YuvMatrixPolicy
{
    /// <summary>
    /// The first frame height that is high definition. Below it a picture is standard definition,
    /// whatever it is carried in.
    /// </summary>
    public const int HighDefinitionHeight = 720;

    private static readonly YuvColourMatrix Bt601Matrix = new(298, 409, 100, 208, 516);

    private static readonly YuvColourMatrix Bt709Matrix = new(298, 459, 55, 136, 541);

    private static readonly YuvColourMatrix Bt2020Matrix = new(298, 430, 48, 167, 548);

    /// <summary>
    /// The space a picture <paramref name="frameHeight"/> lines tall was encoded in. A degenerate
    /// height is standard definition rather than a guess, which is also what it was before.
    /// </summary>
    public static YuvColourSpace Choose(int frameHeight) =>
        frameHeight >= HighDefinitionHeight ? YuvColourSpace.Bt709 : YuvColourSpace.Bt601;

    /// <summary>The coefficients of <paramref name="space"/>.</summary>
    public static YuvColourMatrix MatrixFor(YuvColourSpace space) => space switch
    {
        YuvColourSpace.Bt601 => Bt601Matrix,
        YuvColourSpace.Bt709 => Bt709Matrix,
        YuvColourSpace.Bt2020 => Bt2020Matrix,
        _ => throw new ArgumentOutOfRangeException(nameof(space), space, "There is no such matrix."),
    };

    /// <summary>
    /// The coefficients for a picture <paramref name="frameHeight"/> lines tall, which is the call
    /// every decoder wants.
    /// </summary>
    public static YuvColourMatrix For(int frameHeight) => MatrixFor(Choose(frameHeight));

    /// <summary>
    /// The coefficients for a picture that may declare its own matrix: the declaration when it names
    /// one of the three, the height otherwise.
    /// </summary>
    public static YuvColourMatrix For(int frameHeight, SourceColour colour)
    {
        ArgumentNullException.ThrowIfNull(colour);
        return colour.Matrix switch
        {
            SourceColour.Bt2020NonConstantMatrix => Bt2020Matrix,
            1 => Bt709Matrix,
            5 or 6 => Bt601Matrix,
            _ => For(frameHeight),
        };
    }
}
