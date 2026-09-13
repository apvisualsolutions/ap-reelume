// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Globalization;

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// What a person asked the picture to look like: brightness, contrast and gamma, and the 256-entry
/// table that carries all three at once.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists</b>, measured on 2026-09-12 against a real episode out of the owner's own
/// library: the picture was not soft, it was crushed. Five scenes of one file read a mean luma
/// between 28 and 69 out of 235, and in one of them the brightest pixel anywhere was 97. Enlarging
/// that more sharply does nothing for it — half the picture is flat black, and no amount of
/// sharpening invents what was never lit. Raising gamma brings back the curtains, the armour and the
/// texture of the cloth, and it costs a table lookup.
/// </para>
/// <para>
/// <b>The arithmetic is FFmpeg's <c>eq</c> filter</b> (<c>libavfilter/vf_eq.c</c>,
/// <c>LGPL-2.1-or-later</c>), deliberately and not by coincidence: the comparison the owner looked at
/// and approved was produced by that filter, so anything else here would be a different picture from
/// the one that was signed off. Its <c>gamma_weight</c> is fixed at 1, which is its own default and
/// the value that produced those frames; exposing a fourth control that blends the curve back
/// towards the straight line would add a dial nobody asked for.
/// </para>
/// <para>
/// <b>The neutral setting builds the identity exactly</b>, and that is not a happy accident of
/// rounding — it is what lets the default cost nothing and change nothing. A conversion that runs on
/// every pixel of every frame cannot afford «almost».
/// </para>
/// </remarks>
/// <param name="Brightness">
/// Shifts every level the same way, −1 to 1, where 0 leaves the picture alone. The range is
/// FFmpeg's.
/// </param>
/// <param name="Contrast">
/// Pivots levels around the middle, 0.5 to 2, where 1 leaves the picture alone. FFmpeg allows
/// −1000 to 1000; a person cannot use that and a slider that reaches it is a slider that ruins the
/// picture by accident.
/// </param>
/// <param name="Gamma">
/// Bends the curve, 0.5 to 3, where 1 leaves the picture alone. Above 1 lifts the shadows without
/// moving black or white, which is the whole reason this feature exists. FFmpeg allows 0.1 to 10,
/// and both ends of that are unwatchable.
/// </param>
public sealed record PictureAdjustment(double Brightness, double Contrast, double Gamma)
{
    /// <summary>The smallest and largest each control accepts.</summary>
    public const double MinimumBrightness = -1d;

    /// <inheritdoc cref="MinimumBrightness"/>
    public const double MaximumBrightness = 1d;

    /// <inheritdoc cref="MinimumBrightness"/>
    public const double MinimumContrast = 0.5d;

    /// <inheritdoc cref="MinimumBrightness"/>
    public const double MaximumContrast = 2d;

    /// <inheritdoc cref="MinimumBrightness"/>
    public const double MinimumGamma = 0.5d;

    /// <inheritdoc cref="MinimumBrightness"/>
    public const double MaximumGamma = 3d;

    /// <summary>Every control where it leaves the picture exactly as it arrived.</summary>
    public static PictureAdjustment Neutral { get; } = new(0d, 1d, 1d);

    /// <inheritdoc cref="PictureAdjustment(double, double, double)"/>
    public double Brightness { get; } =
        Validated(Brightness, MinimumBrightness, MaximumBrightness, nameof(Brightness));

    /// <inheritdoc cref="PictureAdjustment(double, double, double)"/>
    public double Contrast { get; } =
        Validated(Contrast, MinimumContrast, MaximumContrast, nameof(Contrast));

    /// <inheritdoc cref="PictureAdjustment(double, double, double)"/>
    public double Gamma { get; } = Validated(Gamma, MinimumGamma, MaximumGamma, nameof(Gamma));

    /// <summary>
    /// Whether this asks for nothing at all, which is what lets the conversion skip the table and
    /// the feature cost nothing until somebody turns a dial.
    /// </summary>
    public bool IsNeutral => Brightness == 0d && Contrast == 1d && Gamma == 1d;

    /// <summary>How much of a level the fixed-point curve keeps below the whole number.</summary>
    /// <remarks>
    /// Eight bits, so a level is 256 steps and the largest value is <c>255 × 256</c>. It is a shift and
    /// not a division in the per-pixel path, which is the only reason a fraction is affordable there.
    /// </remarks>
    public const int FractionBits = 8;

    /// <summary>The largest value <see cref="BuildLookup"/> can produce, which is white.</summary>
    public const int FullScale = 255 << FractionBits;

    /// <summary>
    /// The 256 levels this setting turns each incoming level into, in one table so that a per-pixel
    /// loop pays one indexed read instead of three multiplications and a power.
    /// </summary>
    /// <remarks>
    /// <b>In fixed point with <see cref="FractionBits"/> bits of fraction</b>, so that
    /// <see cref="Quantise"/> can round to the nearest level instead of truncating. Truncation is a
    /// defect of its own: a curve asking for 100,9 painted 100, and a control whose shift is not a whole
    /// number of levels — brightness at 0.1 is 25,5 of them — left the entire picture half a level dark.
    /// </remarks>
    public int[] BuildLookup()
    {
        var table = new int[256];
        var exponent = 1d / Gamma;
        for (var level = 0; level < table.Length; level++)
        {
            // FFmpeg's create_lut, verbatim in shape: the linear part first, then the curve, then
            // the two saturating ends.
            //
            // The scale is 255 and not FFmpeg's 256, and the difference is the whole reason neutral
            // still comes out byte for byte. Theirs relies on truncation — 256 × v truncated happens to
            // give back the level — which is a scale of 256/255 papering over a rounding this path now
            // does properly. At 255 the neutral curve lands on exact whole numbers, so rounding cannot
            // move it and PLY-018's promise holds.
            var v = (Contrast * ((level / 255d) - 0.5d)) + 0.5d + Brightness;
            // Below black is pinned at black and never carried on with, and the reason is measured
            // rather than defensive: taking this branch out and asking for brightness −0.5 at
            // contrast 2 puts 255 in level 127 and 1 in level 128 — the whole shadow end of the
            // picture comes out WHITE, because a negative double cast to byte is not 0, it is
            // whatever the wrap leaves. With a gamma other than 1 it is NaN instead, which does cast
            // to 0 and hides the same hole on this runtime only.
            if (v <= 0d)
            {
                table[level] = 0;
                continue;
            }

            v = Math.Pow(v, exponent);
            table[level] = v >= 1d
                ? FullScale
                : (int)Math.Round(FullScale * v, MidpointRounding.AwayFromZero);
        }

        return table;
    }

    /// <summary>
    /// What the curve asks for at <paramref name="level"/>, in levels and with its fraction kept.
    /// </summary>
    /// <remarks>
    /// Exists so a test can compare against the intention rather than against another copy of the
    /// implementation: asserting that the painted level matches <see cref="BuildLookup"/> would be
    /// asserting that a number equals itself.
    /// </remarks>
    public double ExactLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 255);

        var v = (Contrast * ((level / 255d) - 0.5d)) + 0.5d + Brightness;

        return v <= 0d ? 0d : Math.Min(255d, 255d * Math.Pow(v, 1d / Gamma));
    }

    /// <summary>Half a level, in the fixed point of the curve.</summary>
    /// <remarks>
    /// Adding it before dropping the fraction is what turns a truncation into a rounding, which is the
    /// difference between a picture that is systematically half a level dark and one that is not.
    /// </remarks>
    public const int HalfLevel = 1 << (FractionBits - 1);

    /// <summary>
    /// Turns one fixed-point value from <see cref="BuildLookup"/> into the level a pixel is painted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rounded to the nearest level rather than truncated, which fixes a defect of its own.</b> The
    /// old table truncated, so a curve asking for 100,9 painted 100 — and a control whose shift is not
    /// a whole number of levels, such as brightness at 0.1, is 25,5 levels and left the <i>entire</i>
    /// picture half a level dark. Measured over all 256 levels in
    /// <c>PictureAdjustmentRoundingTests</c>.
    /// </para>
    /// <para>
    /// <b>What this deliberately does NOT do is dither, and the reason is measured.</b> Spreading the
    /// fraction across neighbouring pixels is the textbook answer to the banding a tone curve leaves,
    /// and it was built and shipped for twenty minutes on 2026-09-13. The owner saw it immediately:
    /// «ahora aparecen un montón de cuadraditos en toda la imagen». <b>The pattern lives in the
    /// resolution of the decoded frame and the screen is four times that</b>, so an 8×8 dither cell
    /// becomes a 32×32 block of screen pixels — one level of difference spread over an area large
    /// enough to read as banding of its own. Dither has to be the last step before the screen and here
    /// it was the first. `ENG-023` carries the ordering.
    /// </para>
    /// <para>
    /// No clamp is needed and that is arithmetic rather than optimism: the largest sum is
    /// <c>(255 × 256) + 128</c>, and shifting that right by eight gives 255.
    /// </para>
    /// </remarks>
    public static int Quantise(int curveValue) => (curveValue + HalfLevel) >> FractionBits;

    /// <summary>
    /// Refuses a setting no control can produce. Clamping would be the friendlier-looking choice and
    /// the wrong one: a value out of range reaches here from a settings file somebody edited or from
    /// a caller that computed it, and silently watching a different picture than the one asked for
    /// is how a defect survives being looked at.
    /// </summary>
    private static double Validated(double value, double minimum, double maximum, string name)
    {
        // NaN fails every comparison, so it is named rather than left to one: the test is «not
        // inside the range» and never «outside it», or a NaN walks straight through and Math.Pow
        // answers NaN for all 256 entries — a black picture with no error anywhere.
        if (double.IsNaN(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                name,
                value,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} has to be between {1} and {2}.",
                    name,
                    minimum,
                    maximum));
        }

        return value;
    }
}
