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
/// The height decides because nothing available here declares the space. LibVLC 3 publishes a video
/// track's geometry, aspect, cadence and orientation and no colour information at all, and reading
/// it out of the container would mean shipping a second decoder for the job — <c>ffprobe</c>, which
/// the HDR suite uses, is a test tool and is not on the machine of whoever runs the application. So
/// the rule is the industry's own: 720 lines and above is BT.709, below it is BT.601. A file that
/// disagrees with its own height is rare and its error is smaller than the one this replaces.
/// </para>
/// <para>
/// There is deliberately no way to pass a space in. Preferring what the source declares is the right
/// answer the day anything in the tree can read it — PLY-016's graphics path will have to tell the
/// video processor what it is handed — and this is the one place that will change. Until something
/// supplies that value, a parameter for it would be a door nobody walks through, which is this
/// repository's characteristic defect rather than a head start.
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
        _ => throw new ArgumentOutOfRangeException(nameof(space), space, "There is no such matrix."),
    };

    /// <summary>
    /// The coefficients for a picture <paramref name="frameHeight"/> lines tall, which is the call
    /// every decoder wants.
    /// </summary>
    public static YuvColourMatrix For(int frameHeight) => MatrixFor(Choose(frameHeight));
}
