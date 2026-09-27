// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// What the player keeps between frames to choose the noise reducer's step: it averages what it
/// measures and decides every few frames, so one slow frame does not change the picture.
/// </summary>
public sealed class DenoiseCadenceTests
{
    private const double SixtyFrames = 1000d / 60d;

    [Fact]
    public void It_starts_at_the_finest_step()
    {
        Assert.Equal(1, new DenoiseCadence().Step);
    }

    [Fact]
    public void A_frame_that_keeps_costing_too_much_moves_it_to_a_coarser_step()
    {
        var cadence = new DenoiseCadence();

        Feed(cadence, DenoiseCadence.DecisionFrames, costMilliseconds: 14d, SixtyFrames);

        Assert.Equal(2, cadence.Step);
    }

    [Fact]
    public void One_slow_frame_among_quick_ones_does_not()
    {
        var cadence = new DenoiseCadence();

        cadence.Record(40d, SixtyFrames);
        Feed(cadence, DenoiseCadence.DecisionFrames - 1, costMilliseconds: 2d, SixtyFrames);

        Assert.Equal(1, cadence.Step);
    }

    [Fact]
    public void Nothing_is_decided_before_enough_frames_have_been_measured()
    {
        var cadence = new DenoiseCadence();

        Feed(cadence, DenoiseCadence.DecisionFrames - 1, costMilliseconds: 14d, SixtyFrames);

        Assert.Equal(1, cadence.Step);
    }

    [Fact]
    public void After_a_change_it_measures_the_new_step_before_deciding_again()
    {
        // The costs measured at the old step say nothing about the new one: carried over, a step of
        // two would inherit the 14 ms of a step of one and jump straight on to four.
        var cadence = new DenoiseCadence();
        Feed(cadence, DenoiseCadence.DecisionFrames, costMilliseconds: 14d, SixtyFrames);

        Feed(cadence, DenoiseCadence.DecisionFrames, costMilliseconds: 5d, SixtyFrames);

        Assert.Equal(2, cadence.Step);
    }

    [Fact]
    public void A_pause_does_not_read_as_a_slow_film()
    {
        // No frame arrives while the film is paused, so the first one after it comes seconds after
        // the last. Taken at its word that interval makes any cost look small and walks the step
        // back to one on a machine that cannot hold it.
        var cadence = new DenoiseCadence();
        Feed(cadence, DenoiseCadence.DecisionFrames, costMilliseconds: 14d, SixtyFrames);

        cadence.Record(5d, intervalMilliseconds: 30_000d);
        Feed(cadence, DenoiseCadence.DecisionFrames - 1, costMilliseconds: 5d, SixtyFrames);

        Assert.Equal(2, cadence.Step);
    }

    [Fact]
    public void Resetting_goes_back_to_the_finest_step_for_the_next_film()
    {
        var cadence = new DenoiseCadence();
        Feed(cadence, DenoiseCadence.DecisionFrames, costMilliseconds: 14d, SixtyFrames);

        cadence.Reset();

        Assert.Equal(1, cadence.Step);
        Feed(cadence, DenoiseCadence.DecisionFrames - 1, costMilliseconds: 14d, SixtyFrames);
        Assert.Equal(1, cadence.Step);
    }

    private static void Feed(DenoiseCadence cadence, int frames, double costMilliseconds, double intervalMilliseconds)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            cadence.Record(costMilliseconds, intervalMilliseconds);
        }
    }
}
