// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Playback;

using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// What a measured enlargement costs, and whether that cost fits inside one frame (PLY-016).
/// </summary>
/// <remarks>
/// The arithmetic is here and the measuring is not, for the reason <see cref="UpscalePolicy"/>
/// already carries: whether a card CAN enlarge is a question only that card answers, and whether the
/// answer is affordable is arithmetic that runs anywhere. It is separate so it can be tested at all
/// — a figure that only exists inside a probe is a figure nobody can check.
/// </remarks>
public sealed class UpscaleCostPolicyTests
{
    /// <summary>
    /// The cost of one frame is the total divided by how many passes produced it, which is the whole
    /// reason a probe repeats: one pass measures the pass plus everything around it.
    /// </summary>
    [Fact]
    public void The_cost_of_a_frame_is_the_total_shared_between_the_passes()
    {
        var cost = UpscaleCostPolicy.Measure(TimeSpan.FromMilliseconds(600), passes: 60, framesPerSecond: 24);

        Assert.Equal(TimeSpan.FromMilliseconds(10), cost.PerFrame);
    }

    /// <summary>
    /// The share is of the frame the video actually carries, not of a fixed number: the same ten
    /// milliseconds are a quarter of a frame at 24 per second and two thirds of one at 60.
    /// </summary>
    [Theory]
    [InlineData(24, 0.24)]
    [InlineData(30, 0.30)]
    [InlineData(60, 0.60)]
    public void The_share_is_of_the_frame_this_video_carries(double framesPerSecond, double expected)
    {
        var cost = UpscaleCostPolicy.Measure(
            TimeSpan.FromMilliseconds(100),
            passes: 10,
            framesPerSecond: framesPerSecond);

        Assert.Equal(expected, cost.FrameBudgetShare, 3);
    }

    /// <summary>
    /// A third of the frame, and the reason is what else has to happen inside it: the picture has to
    /// be decoded, enlarged and composed with everything drawn over it. An enlargement that takes
    /// half the frame leaves the other half for both of the others, which is how a player starts
    /// dropping frames on the machines that needed the enlargement most.
    /// </summary>
    [Theory]
    [InlineData(10, true)]
    [InlineData(13, true)]
    [InlineData(14, false)]
    [InlineData(41, false)]
    public void It_fits_when_it_takes_no_more_than_a_third_of_the_frame(int milliseconds, bool fits)
    {
        var cost = UpscaleCostPolicy.Measure(
            TimeSpan.FromMilliseconds(milliseconds),
            passes: 1,
            framesPerSecond: 24);

        Assert.Equal(fits, cost.FitsBudget);
    }

    /// <summary>
    /// A measurement of nothing is not a cost of zero, and that distinction is the whole guard: zero
    /// passes reads as «it was free» and would light the indicator over an enlargement nobody timed.
    /// </summary>
    [Theory]
    [InlineData(0, 24)]
    [InlineData(-1, 24)]
    [InlineData(60, 0)]
    [InlineData(60, -24)]
    public void A_measurement_that_could_not_have_happened_costs_nothing_and_fits_nothing(
        int passes,
        double framesPerSecond)
    {
        var cost = UpscaleCostPolicy.Measure(TimeSpan.FromMilliseconds(600), passes, framesPerSecond);

        Assert.Equal(TimeSpan.Zero, cost.PerFrame);
        Assert.Equal(0, cost.FrameBudgetShare);
        Assert.False(cost.FitsBudget);
    }

    /// <summary>
    /// Negative time is a clock that went backwards, which happens, and it is refused rather than
    /// divided: a negative cost fits every budget there is.
    /// </summary>
    [Fact]
    public void Time_that_ran_backwards_is_refused_rather_than_shared_out()
    {
        var cost = UpscaleCostPolicy.Measure(TimeSpan.FromMilliseconds(-5), passes: 10, framesPerSecond: 24);

        Assert.Equal(TimeSpan.Zero, cost.PerFrame);
        Assert.False(cost.FitsBudget);
    }

    /// <summary>
    /// An enlargement that takes no time at all is the shape of a card that accepted the request and
    /// did nothing — the exact answer NVIDIA gave this repository on 2026-09-12 — so it is reported
    /// as free and fitting, and what decides whether it happened is the pixel count beside it.
    /// </summary>
    [Fact]
    public void Costing_nothing_is_reported_rather_than_treated_as_a_failure()
    {
        var cost = UpscaleCostPolicy.Measure(TimeSpan.Zero, passes: 60, framesPerSecond: 24);

        Assert.Equal(TimeSpan.Zero, cost.PerFrame);
        Assert.Equal(0, cost.FrameBudgetShare);
        Assert.True(cost.FitsBudget);
    }

    /// <summary>The share a frame may spend, named so the probe and this policy cannot disagree.</summary>
    [Fact]
    public void The_share_a_frame_may_spend_is_named_rather_than_written_twice()
    {
        Assert.Equal(1d / 3d, UpscaleCostPolicy.MaximumFrameShare, 6);
    }

    /// <summary>
    /// Two timings and a subtraction, which is how the cost of the enlargement is separated from
    /// everything around it. Reading a 4K surface back into memory is 33 MB of copying and it happens
    /// once per timing whatever the pass count, so dividing a single timing measures the read-back as
    /// much as the work — and the read-back is not part of playing a film.
    /// </summary>
    [Fact]
    public void The_fixed_cost_around_the_work_is_subtracted_rather_than_diluted()
    {
        // One pass plus 40 ms of read-back, and sixty-one passes plus the same 40 ms.
        var cost = UpscaleCostPolicy.MeasureDifference(
            many: TimeSpan.FromMilliseconds(640),
            one: TimeSpan.FromMilliseconds(50),
            extraPasses: 60,
            framesPerSecond: 24);

        // 590 ms of extra work over sixty extra passes is 9.83 ms each — and dividing the 640 alone
        // would have said 10.5, which is the read-back being charged to the enlargement.
        Assert.Equal(9.833, cost.PerFrame.TotalMilliseconds, 2);
        Assert.True(cost.FitsBudget);
    }

    /// <summary>
    /// A second timing faster than the first is noise, not a negative cost, and it is refused: a
    /// negative number divided out fits every budget there is and would report free enlargement.
    /// </summary>
    [Theory]
    [InlineData(50, 60, 60)]
    [InlineData(640, 50, 0)]
    [InlineData(640, 50, -1)]
    public void A_difference_that_could_not_have_happened_costs_nothing_and_fits_nothing(
        int many,
        int one,
        int extraPasses)
    {
        var cost = UpscaleCostPolicy.MeasureDifference(
            TimeSpan.FromMilliseconds(many),
            TimeSpan.FromMilliseconds(one),
            extraPasses,
            framesPerSecond: 24);

        Assert.Equal(TimeSpan.Zero, cost.PerFrame);
        Assert.False(cost.FitsBudget);
    }
}
