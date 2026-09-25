// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// The tone curve moved behind the enlargement and dithered there, which `ENG-023` asked for,
/// measured against what ships and left unbuilt because of what the measurement said.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was asked.</b> Dither placed before the enlargement came out as 32×32 blocks on screen and
/// was withdrawn on 2026-09-13. The record concluded that dither goes after the scaling, which needs
/// the curve in the shader — and the shader only runs while the enlargement is on, so something had
/// to be decided for when it is off.
/// </para>
/// <para>
/// <b>What the measurement answered, on 2026-09-25, in two halves.</b> The band a raised gamma shows
/// in the shadows is <b>not</b> something dither removes: it is the file's own eight-bit step,
/// stretched by the curve to two or three levels, and a dither spreads a fraction below one level.
/// Measured as the step between neighbouring 8-column averages under the harshest enlargement,
/// wherever the gamma lifts the shadows the shipping chain shows a band of two to four levels and
/// the candidate never takes it below two — it trims up to 0,6 of a level in one scene and widens it
/// in others — while the same ramp before it was quantised reads one. What the candidate does
/// buy is accuracy of the averaged tone: up to two thirds of a level closer to the full-precision
/// curve in some scenes, worse in others, and never enough to move the worst scene.
/// </para>
/// <para>
/// <b>So it is not built</b>, and the question of what happens with the enlargement off goes away
/// with it: a shader on every frame and a second place where the curve can live, for less than one
/// level of averaged tone that a screen cannot show. What removes a band stretched out of the source
/// is a debanding filter, not a dither; and what the owner actually saw around lettering was
/// compression noise lifted by the gamma (`ENG-021`), which is `ENG-024`'s denoiser.
/// </para>
/// <para>
/// <b>The model is grey and one-dimensional on purpose</b>: a luma curve on a grey pixel is the whole
/// of the arithmetic — <c>PackedYuvConverter</c> adds the same luma to all three channels — and a
/// ramp is where banding lives. Two enlargements are used and each for what it can show: linear for
/// the averaged tone, and repetition for the band, because a linear ×4 splits any step of up to four
/// levels into steps of one and would hide the band it is asked about — the gate auditor measured
/// exactly that blindness in the first draft of this file. The player's sharpened cubic lies between
/// the two. Figures are in <c>docs/evidence/stable/ENG023-tone-curve-ordering.md</c>.
/// </para>
/// </remarks>
public sealed class ToneCurveOrderingCandidateTests
{
    private const int Scale = 4;
    private const int Rows = 8;
    private const int Window = 8;

    /// <summary>The classic 8×8 ordered-dither matrix, 0 to 63.</summary>
    private static readonly int[] Bayer =
    [
        0, 32, 8, 40, 2, 34, 10, 42, 48, 16, 56, 24, 50, 18, 58, 26,
        12, 44, 4, 36, 14, 46, 6, 38, 60, 28, 52, 20, 62, 30, 54, 22,
        3, 35, 11, 43, 1, 33, 9, 41, 51, 19, 59, 27, 49, 17, 57, 25,
        15, 47, 7, 39, 13, 45, 5, 37, 63, 31, 55, 23, 61, 29, 53, 21,
    ];

    /// <summary>The gammas that lift the shadows, which is where the band this task was about lives.</summary>
    public static TheoryData<double, double, double, int> LiftingScenes()
    {
        var data = new TheoryData<double, double, double, int>();
        foreach (var (gamma, low, high, width) in AllScenes().Where(scene => scene.Gamma > 1d))
        {
            data.Add(gamma, low, high, width);
        }

        return data;
    }

    /// <summary>
    /// Every scene: slow shadow ramp, the whole range, an almost flat patch, the deepest blacks and a
    /// mid shadow, under gammas either side of one — 1.5 is what the owner had on.
    /// </summary>
    /// <remarks>
    /// The flat patch and the deepest blacks are left out at gamma 0.7, and that is measured rather
    /// than chosen: that curve takes both below limited-range black, so every chain paints 0 and the
    /// rows would pass without measuring anything. <see cref="Measure"/> refuses such a scene.
    /// </remarks>
    private static IEnumerable<(double Gamma, double Low, double High, int Width)> AllScenes()
    {
        foreach (var gamma in new[] { 0.7d, 1.5d, 2d, 3d })
        {
            yield return (gamma, 20d, 60d, 400);
            yield return (gamma, 16d, 235d, 400);
            yield return (gamma, 40d, 48d, 400);
            if (gamma > 1d)
            {
                yield return (gamma, 30d, 34d, 200);
                yield return (gamma, 16d, 24d, 400);
            }
        }
    }

    /// <summary>
    /// Where the gamma lifts the shadows, the candidate still leaves a band of two levels or more.
    /// </summary>
    /// <remarks>
    /// The band is read as the largest step between two neighbouring 8×8 averages, which is what an
    /// eye sees of a dither; comparing single pixels would count the dither as a band of its own. The
    /// claim is not «the same band»: at gamma 2 over the mid shadow the candidate trims 3,0 to 2,4,
    /// which a first draft of this remark said could not happen. It is that the band stays at two
    /// levels or more wherever the shipping chain has one that wide — a stretched step of the file,
    /// which a dither can soften and cannot remove.
    /// </remarks>
    [Theory]
    [MemberData(nameof(LiftingScenes))]
    public void Dithering_behind_the_enlargement_leaves_the_band_the_gamma_stretched_out_of_the_file(
        double gamma,
        double low,
        double high,
        int width)
    {
        var scene = Measure(gamma, low, high, width);

        var shipping = BandStep(scene.RepeatedShipping);
        var candidate = BandStep(scene.RepeatedCandidate);

        Assert.True(
            candidate >= Math.Min(shipping, 2d),
            $"At gamma {gamma} over {low}–{high}, the dithered candidate reads a band of {candidate:F2} "
                + $"levels against the shipping chain's {shipping:F2}: it now removes the band, so "
                + "ENG-023 is worth reopening.");
    }

    /// <summary>
    /// The band control: the same ramp before the file quantised it reads a far smaller band.
    /// </summary>
    /// <remarks>
    /// This is what makes the test above a measurement. Without it, «the candidate leaves the same
    /// band» could mean that the metric sees no band at all; here it sees one, and names where it
    /// comes from — the eight-bit step of the source, which no stage after the decoder can put back.
    /// </remarks>
    [Fact]
    public void Before_the_file_was_quantised_the_same_ramp_reads_a_far_smaller_band()
    {
        var scene = Measure(gamma: 1.5d, low: 20d, high: 60d, width: 400);

        var shipping = BandStep(scene.RepeatedShipping);
        var unquantised = BandStep(scene.RepeatedUnquantised);

        Assert.True(
            unquantised < shipping - 1d,
            $"The unquantised ramp reads a band of {unquantised:F2} against the shipping chain's "
                + $"{shipping:F2}, so the metric cannot tell a stretched eight-bit step from a smooth ramp.");
    }

    /// <summary>
    /// The candidate does not beat the shipping chain on the worst averaged tone, and this fails the
    /// day it does.
    /// </summary>
    /// <remarks>
    /// The candidate wins most scenes, by up to two thirds of a level, and loses others. The worst
    /// scene is what is compared because it is what a person could meet: the shipping chain's is about
    /// one level — two roundings, one of the curve and one of the conversion — and the candidate's is
    /// a twentieth of a level better. A quarter of a level is the margin.
    /// </remarks>
    [Fact]
    public void The_curve_behind_the_enlargement_with_dither_does_not_beat_the_worst_averaged_tone()
    {
        var shippingWorst = 0d;
        var candidateWorst = 0d;
        foreach (var (gamma, low, high, width) in AllScenes())
        {
            var scene = Measure(gamma, low, high, width);
            shippingWorst = Math.Max(shippingWorst, AveragedError(scene.Shipping, scene.Ideal));
            candidateWorst = Math.Max(candidateWorst, AveragedError(scene.Candidate, scene.Ideal));
        }

        Assert.True(
            candidateWorst > shippingWorst - 0.25,
            $"The candidate's worst averaged error is {candidateWorst:F2} levels against the shipping "
                + $"chain's {shippingWorst:F2}: it now wins by a quarter of a level or more, so ENG-023 "
                + "is worth reopening.");
    }

    /// <summary>
    /// The candidate's control: where it is known to win, it wins clearly.
    /// </summary>
    /// <remarks>
    /// Every mistake that weakens the candidate strengthens the decision above, so without this the
    /// comparison only looks one way — the gate auditor showed that dropping the dither, or biasing
    /// the candidate by 0,4 of a level, left every other test green. Measured: 0,33 against 0,96.
    /// </remarks>
    [Fact]
    public void Where_the_candidate_is_known_to_win_it_wins_clearly()
    {
        var scene = Measure(gamma: 0.7d, low: 20d, high: 60d, width: 400);

        var shipping = AveragedError(scene.Shipping, scene.Ideal);
        var candidate = AveragedError(scene.Candidate, scene.Ideal);

        Assert.True(
            candidate < shipping - 0.4,
            $"At gamma 0.7 over the slow shadow ramp the candidate reads {candidate:F2} against "
                + $"{shipping:F2}, so the model of the candidate has lost what it is known to do.");
    }

    /// <summary>
    /// The error control: truncating the curve, which was a real defect, reads clearly worse.
    /// </summary>
    /// <remarks>
    /// Without this the comparison above could hold because the averaged error cannot tell two chains
    /// apart. Measured at 1,47 against 0,85; the margin of 0,4 is below that on purpose, and the
    /// gentler mutants the gate auditor tried are caught by the comparison instead.
    /// </remarks>
    [Fact]
    public void Truncating_the_curve_reads_clearly_worse_on_the_same_yardstick()
    {
        var scene = Measure(gamma: 1.5d, low: 20d, high: 60d, width: 400);

        var shipping = AveragedError(scene.Shipping, scene.Ideal);
        var truncated = AveragedError(scene.Truncated, scene.Ideal);

        Assert.True(
            truncated > shipping + 0.4,
            $"Truncated, the averaged error is {truncated:F2} against {shipping:F2} rounded, so the "
                + "yardstick cannot tell a known defect from the fix.");
    }

    /// <summary>
    /// The continuous curve here is the domain's table at every level, for all three controls.
    /// </summary>
    /// <remarks>
    /// The candidate needs the curve between levels, which the domain does not offer, so this file
    /// carries a copy. It is bound to <see cref="PictureAdjustment.BuildLookup"/> — what the player
    /// paints — and not to <see cref="PictureAdjustment.ExactLevel"/>, which is another copy of the
    /// same formula; and it covers brightness and contrast, because every scene above runs at their
    /// neutral values and a drift in either would otherwise go unseen.
    /// </remarks>
    [Theory]
    [InlineData(0d, 1d, 0.7d)]
    [InlineData(0d, 1d, 1.5d)]
    [InlineData(0d, 1d, 3d)]
    [InlineData(0.1d, 1d, 1d)]
    [InlineData(-0.3d, 1d, 1d)]
    [InlineData(0d, 1.5d, 1d)]
    [InlineData(0d, 0.7d, 1d)]
    [InlineData(0.1d, 1.3d, 1.5d)]
    public void The_continuous_curve_is_the_table_the_player_paints(
        double brightness,
        double contrast,
        double gamma)
    {
        var adjustment = new PictureAdjustment(brightness, contrast, gamma);
        var lookup = adjustment.BuildLookup();

        for (var level = 0; level < 256; level++)
        {
            var continuous = Curve(adjustment, level) * (1 << PictureAdjustment.FractionBits);
            Assert.True(
                Math.Abs(continuous - lookup[level]) <= 0.5 + 1e-6,
                $"At brightness {brightness}, contrast {contrast}, gamma {gamma}, level {level}: the "
                    + $"copy here asks for {continuous:F2} and the table holds {lookup[level]}.");
        }
    }

    private sealed record Scene(
        double[] Ideal,
        int[,] Shipping,
        int[,] Truncated,
        int[,] Candidate,
        int[,] RepeatedShipping,
        int[,] RepeatedCandidate,
        int[,] RepeatedUnquantised);

    private static Scene Measure(double gamma, double low, double high, int width)
    {
        var adjustment = new PictureAdjustment(0d, 1d, gamma);
        var lookup = adjustment.BuildLookup();
        var ramp = new double[width];
        var source = new int[width];
        for (var i = 0; i < width; i++)
        {
            ramp[i] = low + ((high - low) * i / (width - 1));
            source[i] = (int)Math.Round(ramp[i]);
        }

        // What the decoder hands the view: the curve on luma, then grey, in eight bits.
        var shippingGrey = source.Select(y => ToByte(Grey(PictureAdjustment.Quantise(lookup[y])))).ToArray();
        var truncatedGrey = source.Select(y => ToByte(Grey(lookup[y] >> PictureAdjustment.FractionBits))).ToArray();
        var plainGrey = source.Select(y => ToByte(Grey(y))).ToArray();

        var screen = width * Scale;
        var ideal = new double[screen];
        var shipping = new int[Rows, screen];
        var truncated = new int[Rows, screen];
        var candidate = new int[Rows, screen];
        var repeatedShipping = new int[Rows, screen];
        var repeatedCandidate = new int[Rows, screen];
        var repeatedUnquantised = new int[Rows, screen];
        for (var x = 0; x < screen; x++)
        {
            ideal[x] = Math.Clamp(Grey(Curve(adjustment, Interpolate(source, x))), 0d, 255d);

            // The candidate: the frame arrives without the curve, is enlarged in full precision, then
            // the curve is applied to the luma that grey stands for, and the fraction is dithered.
            var curved = CurvedGrey(adjustment, Interpolate(plainGrey, x));
            var repeated = CurvedGrey(adjustment, plainGrey[x / Scale]);
            var unquantised = ToByte(Grey(Curve(adjustment, ramp[x / Scale])));
            for (var row = 0; row < Rows; row++)
            {
                var dither = ((Bayer[(x % 8) + (8 * row)] + 0.5d) / 64d) - 0.5d;
                shipping[row, x] = ToByte(Interpolate(shippingGrey, x));
                truncated[row, x] = ToByte(Interpolate(truncatedGrey, x));
                candidate[row, x] = ToByte(curved + dither);
                repeatedShipping[row, x] = shippingGrey[x / Scale];
                repeatedCandidate[row, x] = ToByte(repeated + dither);
                repeatedUnquantised[row, x] = unquantised;
            }
        }

        // A scene every chain paints flat measures nothing and passes anyway; the gate auditor found
        // two such rows in the first draft of this file.
        Assert.True(
            ideal.Max() - ideal.Min() > 1d,
            $"Gamma {gamma} over {low}–{high} paints a range of {ideal.Max() - ideal.Min():F2} levels, "
                + "which is no scene at all.");

        return new Scene(
            ideal,
            shipping,
            truncated,
            candidate,
            repeatedShipping,
            repeatedCandidate,
            repeatedUnquantised);
    }

    /// <summary>The curve applied, in full precision, to the luma an eight-bit grey stands for.</summary>
    private static double CurvedGrey(PictureAdjustment adjustment, double grey) =>
        Math.Clamp(Grey(Curve(adjustment, (grey * 219d / 255d) + 16d)), 0d, 255d);

    /// <summary>FFmpeg's eq curve at any luma, the same formula as the domain's table.</summary>
    private static double Curve(PictureAdjustment adjustment, double level)
    {
        var v = (adjustment.Contrast * ((level / 255d) - 0.5d)) + 0.5d + adjustment.Brightness;

        return v <= 0d ? 0d : Math.Min(255d, 255d * Math.Pow(v, 1d / adjustment.Gamma));
    }

    /// <summary>Limited-range luma to the grey <c>PackedYuvConverter</c> writes for it.</summary>
    private static double Grey(double luma) => (luma - 16d) * 255d / 219d;

    private static int ToByte(double value) =>
        (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0d, 255d);

    /// <summary>Linear enlargement with pixel centres aligned, clamped at the ends.</summary>
    private static double Interpolate(int[] source, int x)
    {
        var position = ((x + 0.5d) / Scale) - 0.5d;
        var left = (int)Math.Floor(position);
        var t = position - left;
        var a = source[Math.Clamp(left, 0, source.Length - 1)];
        var b = source[Math.Clamp(left + 1, 0, source.Length - 1)];

        return a + ((b - a) * t);
    }

    /// <summary>The largest step between two neighbouring 8×8 averages, in levels.</summary>
    private static double BandStep(int[,] screen)
    {
        var worst = 0d;
        for (var x = Window; x + Window <= screen.GetLength(1); x++)
        {
            var before = 0d;
            var after = 0d;
            for (var dx = 0; dx < Window; dx++)
            {
                for (var row = 0; row < Rows; row++)
                {
                    before += screen[row, x - Window + dx];
                    after += screen[row, x + dx];
                }
            }

            worst = Math.Max(worst, Math.Abs(after - before) / (Window * Rows));
        }

        return worst;
    }

    /// <summary>The largest error of any 8×8 average against the full-precision curve.</summary>
    private static double AveragedError(int[,] screen, double[] ideal)
    {
        var worst = 0d;
        for (var x = 0; x + Window <= ideal.Length; x++)
        {
            var sum = 0d;
            for (var dx = 0; dx < Window; dx++)
            {
                for (var row = 0; row < Rows; row++)
                {
                    sum += screen[row, x + dx] - ideal[x + dx];
                }
            }

            worst = Math.Max(worst, Math.Abs(sum / (Window * Rows)));
        }

        return worst;
    }
}
