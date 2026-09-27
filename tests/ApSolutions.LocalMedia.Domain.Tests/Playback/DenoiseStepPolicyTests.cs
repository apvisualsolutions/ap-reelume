// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// How coarse the noise reducer runs, decided from what it costs on the machine it is running on.
/// The numbers in these cases are the ones measured on 2026-09-27: a 1080p frame at a step of one
/// took 14 ms on 28 threads and about three times less at each coarser step.
/// </summary>
public sealed class DenoiseStepPolicyTests
{
    private const double SixtyFrames = 1000d / 60d;
    private const double TwentyFourFrames = 1000d / 24d;

    [Fact]
    public void A_reduction_that_fits_keeps_its_step()
    {
        // Standard definition at 24 frames a second: 2 ms of 41.
        Assert.Equal(1, DenoiseStepPolicy.Next(currentStep: 1, averageCostMilliseconds: 2d, TwentyFourFrames));
    }

    [Fact]
    public void A_reduction_that_takes_more_than_half_the_frame_goes_coarser()
    {
        // 1080p at 60 frames a second on this machine: 14 ms of 16.7 leaves nothing for the rest.
        Assert.Equal(2, DenoiseStepPolicy.Next(1, 14d, SixtyFrames));
        Assert.Equal(4, DenoiseStepPolicy.Next(2, 9d, SixtyFrames));
    }

    [Fact]
    public void The_coarsest_step_is_as_far_as_it_goes()
    {
        // What is left past four is turning the reduction off, and that is the person's decision:
        // a picture a little less clean is still the one they asked for.
        Assert.Equal(DenoiseStepPolicy.CoarsestStep, DenoiseStepPolicy.Next(DenoiseStepPolicy.CoarsestStep, 30d, SixtyFrames));
    }

    [Fact]
    public void It_goes_finer_again_only_when_the_finer_step_would_fit_with_room_to_spare()
    {
        // 2 ms at a step of two is about 6 at one, which fits 41 easily: go back.
        Assert.Equal(1, DenoiseStepPolicy.Next(2, 2d, TwentyFourFrames));

        // 5 ms at a step of two is about 15 at one against a budget of 8.3: going finer would only
        // come straight back, and a step that flips every second is a picture that pulses.
        Assert.Equal(2, DenoiseStepPolicy.Next(2, 5d, SixtyFrames));

        // And the margin itself: 2.5 ms would be about 7.5 at one, which is inside the budget of
        // 8.3 but not inside it with room to spare, so the step holds.
        Assert.Equal(2, DenoiseStepPolicy.Next(2, 2.5d, SixtyFrames));
    }

    [Fact]
    public void The_finest_step_is_one()
    {
        Assert.Equal(1, DenoiseStepPolicy.Next(1, 0.1d, TwentyFourFrames));
    }

    [Fact]
    public void Nothing_measured_yet_changes_nothing()
    {
        // Before two frames have arrived there is no interval, and a guess in either direction is
        // a step chosen by nobody.
        Assert.Equal(2, DenoiseStepPolicy.Next(2, 0d, 0d));
        Assert.Equal(1, DenoiseStepPolicy.Next(1, 20d, 0d));

        // And the other half: frames arriving but no cost measured yet is not «it costs nothing».
        Assert.Equal(2, DenoiseStepPolicy.Next(2, 0d, TwentyFourFrames));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(8)]
    public void A_step_the_reducer_does_not_have_is_refused(int step) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DenoiseStepPolicy.Next(step, 1d, SixtyFrames));
}
