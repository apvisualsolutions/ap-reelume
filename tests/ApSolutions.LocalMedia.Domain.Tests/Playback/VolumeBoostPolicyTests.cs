using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// Volume above one hundred percent is only ever offered together with a limiter and a warning. The
/// policy is what guarantees that pairing; no adapter can raise the gain without it.
/// </summary>
public sealed class VolumeBoostPolicyTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(55, 55, false)]
    [InlineData(100, 100, false)]
    [InlineData(101, 101, true)]
    [InlineData(200, 200, true)]
    public void The_normal_range_ends_at_one_hundred_and_the_boost_range_starts_at_one_hundred_and_one(
        int requested,
        int expected,
        bool boosted)
    {
        var decision = VolumeBoostPolicy.Decide(requested, muted: false);

        Assert.Equal(expected, decision.Percent);
        Assert.Equal(boosted, decision.IsBoosted);
        Assert.Equal(boosted, decision.LimiterEngaged);
        Assert.Equal(boosted, decision.RequiresWarning);
    }

    [Theory]
    [InlineData(-40, 0)]
    [InlineData(201, 200)]
    [InlineData(10_000, 200)]
    public void A_request_outside_the_range_is_clamped_rather_than_refused(int requested, int expected) =>
        Assert.Equal(expected, VolumeBoostPolicy.Decide(requested, muted: false).Percent);

    [Fact]
    public void Muting_silences_the_output_without_forgetting_the_chosen_level()
    {
        var muted = VolumeBoostPolicy.Decide(180, muted: true);
        var restored = VolumeBoostPolicy.Decide(muted.Percent, muted: false);

        Assert.Equal(180, muted.Percent);
        Assert.True(muted.IsMuted);
        Assert.Equal(0, muted.LinearGain);
        Assert.True(muted.LimiterEngaged);
        Assert.Equal(180, restored.Percent);
        Assert.False(restored.IsMuted);
        Assert.Equal(1.8, restored.LinearGain, 6);
    }

    [Fact]
    public void The_linear_gain_follows_the_percentage_exactly()
    {
        Assert.Equal(0.0, VolumeBoostPolicy.Decide(0, muted: false).LinearGain);
        Assert.Equal(1.0, VolumeBoostPolicy.Decide(100, muted: false).LinearGain, 6);
        Assert.Equal(2.0, VolumeBoostPolicy.Decide(200, muted: false).LinearGain, 6);
    }

    [Fact]
    public void The_limiter_threshold_never_exceeds_the_normalised_peak()
    {
        Assert.InRange(VolumeBoostPolicy.LimiterThreshold, 0.5, 1.0);
        Assert.Equal(VolumeBoostPolicy.MaximumBoostPercent, 200);
        Assert.Equal(VolumeBoostPolicy.MaximumNormalPercent, 100);
    }
}
