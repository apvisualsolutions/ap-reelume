// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// What one enlarged frame costs, and whether that cost leaves room for everything else in it.
/// </summary>
/// <param name="PerFrame">The time one frame's enlargement took.</param>
/// <param name="FrameBudgetShare">That time as a share of the frame this video carries, 0 to 1.</param>
/// <param name="FitsBudget">Whether the share is inside <see cref="UpscaleCostPolicy.MaximumFrameShare"/>.</param>
public readonly record struct UpscaleCost(TimeSpan PerFrame, double FrameBudgetShare, bool FitsBudget);

/// <summary>
/// Whether an enlargement a card can do is one it can afford, kept away from the card that carries
/// it out (PLY-016).
/// </summary>
/// <remarks>
/// <para>
/// It is separate from the probe for the reason <see cref="UpscalePolicy"/> already carries, and for
/// a second one this repository has paid for: a figure that only exists inside code excluded from
/// coverage is a figure no test can check. Measuring needs a graphics card; dividing does not.
/// </para>
/// <para>
/// <b>The probe repeats, and this is why</b>: one pass measures the pass plus the setup, the flush
/// and the read-back around it. Sharing many passes out between them leaves the pass.
/// </para>
/// </remarks>
public static class UpscaleCostPolicy
{
    /// <summary>
    /// The most of a frame an enlargement may spend: one third.
    /// </summary>
    /// <remarks>
    /// The number is not taste, it is what else has to happen inside the same frame — the picture is
    /// decoded, then enlarged, then composed with the bar, the subtitles and anything drawn over it.
    /// An enlargement taking half the frame leaves the other half for both of the others, and the
    /// machines that gain most from being enlarged are exactly the ones with least to spare. A third
    /// keeps the decision on the safe side of that, and a card that cannot meet it is told so rather
    /// than allowed to drop frames.
    /// </remarks>
    public const double MaximumFrameShare = 1d / 3d;

    /// <summary>
    /// The cost of one frame from a measurement of <paramref name="passes"/> of them at
    /// <paramref name="framesPerSecond"/>.
    /// </summary>
    /// <remarks>
    /// A measurement that could not have happened — no passes, no cadence, or a clock that ran
    /// backwards — returns no cost and fits nothing. That is the distinction the whole guard is
    /// for: zero passes divided out reads as «it was free», which would light the indicator over an
    /// enlargement nobody timed. Costing genuinely nothing is a different answer and is reported as
    /// such, because it is the shape of a card that accepts the request and does not act on it;
    /// whether anything happened is settled by the pixels beside this, never by the clock.
    /// </remarks>
    public static UpscaleCost Measure(TimeSpan total, int passes, double framesPerSecond)
    {
        if (passes <= 0 || framesPerSecond <= 0 || total < TimeSpan.Zero || double.IsNaN(framesPerSecond))
        {
            return default;
        }

        return From(total / passes, framesPerSecond);
    }

    /// <summary>
    /// The cost of one frame from two timings that differ by <paramref name="extraPasses"/> passes,
    /// which is how the work is separated from the fixed cost around it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reading a 4K surface back into memory is 33 MB of copying, it happens once per timing whatever
    /// the pass count, and it is no part of playing a film. Dividing a single timing charges it to
    /// the enlargement; subtracting one timing from another leaves only what the extra passes did.
    /// </para>
    /// <para>
    /// A second timing that came out faster than the first is noise rather than a negative cost, and
    /// it is refused: a negative number fits every budget there is, so it would report an enlargement
    /// as free exactly when the measurement was too unstable to say anything.
    /// </para>
    /// </remarks>
    public static UpscaleCost MeasureDifference(
        TimeSpan many,
        TimeSpan one,
        int extraPasses,
        double framesPerSecond)
    {
        if (extraPasses <= 0 || many < one)
        {
            return default;
        }

        return Measure(many - one, extraPasses, framesPerSecond);
    }

    private static UpscaleCost From(TimeSpan perFrame, double framesPerSecond)
    {
        var frame = TimeSpan.FromSeconds(1) / framesPerSecond;
        var share = perFrame / frame;

        return new UpscaleCost(perFrame, share, share <= MaximumFrameShare);
    }
}
