// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Continuity;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Continuity;

/// <summary>
/// Moving progress from one version of the same content to another. The decision is arithmetic on two
/// durations plus one structural fact, so every boundary can be pinned exactly.
/// </summary>
public sealed class ProgressTransferPolicyTests
{
    private static readonly TimeSpan Feature = TimeSpan.FromMinutes(100);

    private static readonly TimeSpan Halfway = TimeSpan.FromMinutes(50);

    [Fact]
    public void The_tolerance_is_the_greater_of_five_seconds_and_one_per_cent()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), ProgressTransferPolicy.ExactTolerance(Feature));
        Assert.Equal(TimeSpan.FromSeconds(5), ProgressTransferPolicy.ExactTolerance(TimeSpan.FromMinutes(2)));
        Assert.Equal(TimeSpan.FromSeconds(5), ProgressTransferPolicy.ExactTolerance(duration: null));
        Assert.Equal(0.10, ProgressTransferPolicy.ProportionalLimit);
    }

    [Fact]
    public void Identical_durations_keep_the_very_same_second()
    {
        var decision = ProgressTransferPolicy.Decide(Halfway, Feature, Feature, structureIsCompatible: true);

        Assert.Equal(ProgressTransferKind.Exact, decision.Kind);
        Assert.Equal(Halfway, decision.Position);
        Assert.Equal(ProgressTransferReason.None, decision.Reason);
    }

    [Fact]
    public void A_difference_inside_the_tolerance_still_keeps_the_second()
    {
        var target = Feature + TimeSpan.FromSeconds(59);

        var decision = ProgressTransferPolicy.Decide(Halfway, Feature, target, structureIsCompatible: true);

        Assert.Equal(ProgressTransferKind.Exact, decision.Kind);
        Assert.Equal(Halfway, decision.Position);
    }

    [Fact]
    public void A_short_pair_uses_the_five_second_floor_rather_than_one_per_cent()
    {
        var source = TimeSpan.FromMinutes(2);
        var inside = source + TimeSpan.FromSeconds(4);
        var outside = source + TimeSpan.FromSeconds(9);

        Assert.Equal(
            ProgressTransferKind.Exact,
            ProgressTransferPolicy.Decide(TimeSpan.FromSeconds(60), source, inside, true).Kind);
        Assert.Equal(
            ProgressTransferKind.Proportional,
            ProgressTransferPolicy.Decide(TimeSpan.FromSeconds(60), source, outside, true).Kind);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public void A_difference_up_to_ten_per_cent_moves_the_second_in_proportion(double percent)
    {
        var target = TimeSpan.FromTicks((long)(Feature.Ticks * (1 + (percent / 100.0))));

        var decision = ProgressTransferPolicy.Decide(Halfway, Feature, target, structureIsCompatible: true);

        Assert.Equal(ProgressTransferKind.Proportional, decision.Kind);
        Assert.Equal(
            Halfway.TotalSeconds * target.TotalSeconds / Feature.TotalSeconds,
            decision.Position.TotalSeconds,
            precision: 3);
    }

    [Fact]
    public void A_difference_beyond_ten_per_cent_asks_before_moving_anything()
    {
        var target = TimeSpan.FromMinutes(130);

        var decision = ProgressTransferPolicy.Decide(Halfway, Feature, target, structureIsCompatible: true);

        Assert.Equal(ProgressTransferKind.Confirm, decision.Kind);
        Assert.Equal(ProgressTransferReason.LargeDifference, decision.Reason);
        Assert.Equal(TimeSpan.FromMinutes(65), decision.Position);
    }

    [Fact]
    public void An_incompatible_structure_asks_even_when_the_durations_are_close()
    {
        var target = Feature + TimeSpan.FromMinutes(5);

        var decision = ProgressTransferPolicy.Decide(Halfway, Feature, target, structureIsCompatible: false);

        Assert.Equal(ProgressTransferKind.Confirm, decision.Kind);
        Assert.Equal(ProgressTransferReason.IncompatibleStructure, decision.Reason);
    }

    [Fact]
    public void An_identical_pair_with_an_incompatible_structure_still_keeps_the_second()
    {
        var decision = ProgressTransferPolicy.Decide(Halfway, Feature, Feature, structureIsCompatible: false);

        Assert.Equal(ProgressTransferKind.Exact, decision.Kind);
    }

    [Fact]
    public void An_unknown_duration_on_either_side_asks_instead_of_guessing()
    {
        var unknownSource = ProgressTransferPolicy.Decide(Halfway, null, Feature, structureIsCompatible: true);
        var unknownTarget = ProgressTransferPolicy.Decide(Halfway, Feature, null, structureIsCompatible: true);

        Assert.Equal(ProgressTransferKind.Confirm, unknownSource.Kind);
        Assert.Equal(ProgressTransferReason.UnknownDuration, unknownSource.Reason);
        Assert.Equal(Halfway, unknownSource.Position);
        Assert.Equal(ProgressTransferKind.Confirm, unknownTarget.Kind);
        Assert.Equal(ProgressTransferReason.UnknownDuration, unknownTarget.Reason);
    }

    [Fact]
    public void Progress_too_small_to_resume_simply_restarts()
    {
        var decision = ProgressTransferPolicy.Decide(
            TimeSpan.FromSeconds(12),
            Feature,
            Feature,
            structureIsCompatible: true);

        Assert.Equal(ProgressTransferKind.Restart, decision.Kind);
        Assert.Equal(TimeSpan.Zero, decision.Position);
    }

    [Fact]
    public void A_transferred_second_never_lands_past_the_end_of_the_new_version()
    {
        var nearTheEnd = TimeSpan.FromMinutes(99);
        var shorter = TimeSpan.FromMinutes(91);

        var decision = ProgressTransferPolicy.Decide(nearTheEnd, Feature, shorter, structureIsCompatible: true);

        Assert.Equal(ProgressTransferKind.Proportional, decision.Kind);
        Assert.True(decision.Position <= shorter);
    }

    [Fact]
    public void A_zero_length_source_is_treated_as_unknown_rather_than_dividing_by_zero()
    {
        var decision = ProgressTransferPolicy.Decide(Halfway, TimeSpan.Zero, Feature, structureIsCompatible: true);

        Assert.Equal(ProgressTransferKind.Confirm, decision.Kind);
        Assert.Equal(ProgressTransferReason.UnknownDuration, decision.Reason);
    }
}
