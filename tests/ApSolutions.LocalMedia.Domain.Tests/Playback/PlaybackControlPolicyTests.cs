using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// Speed, seek, and skip ranges are approved constants. Every value the person can reach is clamped
/// here so no adapter has to guess what a legal request looks like.
/// </summary>
public sealed class PlaybackControlPolicyTests
{
    [Theory]
    [InlineData(0.25, 0.25)]
    [InlineData(1.0, 1.0)]
    [InlineData(4.0, 4.0)]
    [InlineData(0.0, 0.25)]
    [InlineData(-3.5, 0.25)]
    [InlineData(9.75, 4.0)]
    public void Speed_stays_between_a_quarter_and_four_times(double requested, double expected) =>
        Assert.Equal(expected, PlaybackControlPolicy.ClampSpeed(requested));

    [Fact]
    public void The_approved_speed_steps_are_offered_in_ascending_order()
    {
        Assert.Equal(
            [0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0, 3.0, 4.0],
            PlaybackControlPolicy.SpeedSteps);
        Assert.All(PlaybackControlPolicy.SpeedSteps, step => Assert.Equal(step, PlaybackControlPolicy.ClampSpeed(step)));
    }

    [Fact]
    public void A_position_never_leaves_the_observed_duration()
    {
        var duration = TimeSpan.FromMinutes(10);

        Assert.Equal(TimeSpan.Zero, PlaybackControlPolicy.ClampPosition(TimeSpan.FromSeconds(-30), duration));
        Assert.Equal(duration, PlaybackControlPolicy.ClampPosition(TimeSpan.FromMinutes(12), duration));
        Assert.Equal(
            TimeSpan.FromMinutes(4),
            PlaybackControlPolicy.ClampPosition(TimeSpan.FromMinutes(4), duration));
        Assert.Equal(
            TimeSpan.FromMinutes(12),
            PlaybackControlPolicy.ClampPosition(TimeSpan.FromMinutes(12), duration: null));
    }

    [Fact]
    public void Skipping_near_either_boundary_lands_exactly_on_it()
    {
        var duration = TimeSpan.FromMinutes(10);

        Assert.Equal(
            TimeSpan.Zero,
            PlaybackControlPolicy.Skip(TimeSpan.FromSeconds(3), -PlaybackControlPolicy.DefaultBackwardSkip, duration));
        Assert.Equal(
            duration,
            PlaybackControlPolicy.Skip(
                duration - TimeSpan.FromSeconds(2),
                PlaybackControlPolicy.DefaultForwardSkip,
                duration));
        Assert.Equal(
            TimeSpan.FromMinutes(5) + PlaybackControlPolicy.DefaultForwardSkip,
            PlaybackControlPolicy.Skip(TimeSpan.FromMinutes(5), PlaybackControlPolicy.DefaultForwardSkip, duration));
    }

    [Fact]
    public void The_initial_skip_intervals_are_ten_and_thirty_seconds_and_stay_positive()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), PlaybackControlPolicy.DefaultBackwardSkip);
        Assert.Equal(TimeSpan.FromSeconds(30), PlaybackControlPolicy.DefaultForwardSkip);

        Assert.Equal(
            PlaybackControlPolicy.MinimumSkipInterval,
            PlaybackControlPolicy.ClampSkipInterval(TimeSpan.Zero));
        Assert.Equal(
            PlaybackControlPolicy.MinimumSkipInterval,
            PlaybackControlPolicy.ClampSkipInterval(TimeSpan.FromSeconds(-45)));
        Assert.Equal(
            PlaybackControlPolicy.MaximumSkipInterval,
            PlaybackControlPolicy.ClampSkipInterval(TimeSpan.FromHours(2)));
        Assert.Equal(
            TimeSpan.FromSeconds(15),
            PlaybackControlPolicy.ClampSkipInterval(TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public void One_hundred_rapid_changes_end_on_a_deterministic_position_and_speed()
    {
        var duration = TimeSpan.FromMinutes(10);
        var position = TimeSpan.FromMinutes(5);
        var speed = 1.0;

        for (var iteration = 0; iteration < 100; iteration++)
        {
            var forward = iteration % 2 == 0;
            position = PlaybackControlPolicy.Skip(
                position,
                forward ? PlaybackControlPolicy.DefaultForwardSkip : -PlaybackControlPolicy.DefaultBackwardSkip,
                duration);
            speed = PlaybackControlPolicy.ClampSpeed(forward ? speed * 2 : speed / 2);
        }

        // The loop ends on a backward skip, so the deterministic landing point is one backward step
        // below the end rather than the end itself.
        Assert.Equal(duration - PlaybackControlPolicy.DefaultBackwardSkip, position);
        Assert.Equal(1.0, speed);
        Assert.Equal(position, PlaybackControlPolicy.ClampPosition(position, duration));
    }
}
