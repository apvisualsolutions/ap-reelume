using ApSolutions.LocalMedia.Domain.Continuity;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Continuity;

/// <summary>
/// The three states and the exact points at which they change. Everything here is arithmetic on a
/// position and a duration, so the ninety-per-cent boundary can be pinned to the hundredth.
/// </summary>
public sealed class WatchStatePolicyTests
{
    private static readonly TimeSpan Episode = TimeSpan.FromMinutes(50);

    [Fact]
    public void The_threshold_default_and_range_are_the_approved_constants()
    {
        Assert.Equal(0.90, WatchStatePolicy.DefaultWatchedThreshold);
        Assert.Equal(0.50, WatchStatePolicy.MinimumWatchedThreshold);
        Assert.Equal(1.00, WatchStatePolicy.MaximumWatchedThreshold);
    }

    [Theory]
    [InlineData(50, 60)]
    [InlineData(60, 60)]
    [InlineData(300, 60)]
    [InlineData(20, 24)]
    [InlineData(10, 12)]
    public void Significant_progress_is_the_lesser_of_one_minute_and_two_per_cent(
        double durationMinutes,
        double expectedSeconds)
    {
        var significant = WatchStatePolicy.SignificantProgress(TimeSpan.FromMinutes(durationMinutes));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), significant);
    }

    [Fact]
    public void An_unknown_duration_falls_back_to_the_one_minute_rule()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), WatchStatePolicy.SignificantProgress(duration: null));
    }

    [Theory]
    [InlineData(0, WatchStatus.NotStarted)]
    [InlineData(30, WatchStatus.NotStarted)]
    [InlineData(59.9, WatchStatus.NotStarted)]
    [InlineData(60, WatchStatus.InProgress)]
    [InlineData(1_500, WatchStatus.InProgress)]
    public void Trivial_progress_stays_not_started_and_significant_progress_moves_on(
        double seconds,
        WatchStatus expected)
    {
        var status = WatchStatePolicy.Evaluate(
            TimeSpan.FromSeconds(seconds),
            Episode,
            WatchStatePolicy.DefaultWatchedThreshold);

        Assert.Equal(expected, status);
    }

    [Fact]
    public void The_watched_boundary_sits_exactly_on_the_threshold()
    {
        var justUnder = TimeSpan.FromSeconds(Episode.TotalSeconds * 0.8999);
        var exactly = TimeSpan.FromSeconds(Episode.TotalSeconds * 0.90);

        Assert.Equal(
            WatchStatus.InProgress,
            WatchStatePolicy.Evaluate(justUnder, Episode, WatchStatePolicy.DefaultWatchedThreshold));
        Assert.Equal(
            WatchStatus.Watched,
            WatchStatePolicy.Evaluate(exactly, Episode, WatchStatePolicy.DefaultWatchedThreshold));
        Assert.Equal(
            WatchStatus.Watched,
            WatchStatePolicy.Evaluate(Episode, Episode, WatchStatePolicy.DefaultWatchedThreshold));
    }

    [Fact]
    public void Every_whole_percentage_from_zero_to_one_hundred_lands_on_the_expected_state()
    {
        for (var percent = 0; percent <= 100; percent++)
        {
            var position = TimeSpan.FromSeconds(Episode.TotalSeconds * percent / 100.0);
            var expected = position < WatchStatePolicy.SignificantProgress(Episode)
                ? WatchStatus.NotStarted
                : percent >= 90
                    ? WatchStatus.Watched
                    : WatchStatus.InProgress;

            Assert.Equal(
                expected,
                WatchStatePolicy.Evaluate(position, Episode, WatchStatePolicy.DefaultWatchedThreshold));
        }
    }

    [Fact]
    public void A_lower_threshold_marks_content_watched_earlier()
    {
        var halfway = TimeSpan.FromSeconds(Episode.TotalSeconds * 0.55);

        Assert.Equal(WatchStatus.InProgress, WatchStatePolicy.Evaluate(halfway, Episode, 0.90));
        Assert.Equal(WatchStatus.Watched, WatchStatePolicy.Evaluate(halfway, Episode, 0.50));
    }

    [Fact]
    public void An_unknown_duration_never_reaches_watched()
    {
        Assert.Equal(
            WatchStatus.NotStarted,
            WatchStatePolicy.Evaluate(TimeSpan.FromSeconds(10), duration: null, 0.90));
        Assert.Equal(
            WatchStatus.InProgress,
            WatchStatePolicy.Evaluate(TimeSpan.FromHours(4), duration: null, 0.90));
    }

    [Theory]
    [InlineData(0.0, 0.50)]
    [InlineData(0.49, 0.50)]
    [InlineData(0.50, 0.50)]
    [InlineData(0.75, 0.75)]
    [InlineData(1.00, 1.00)]
    [InlineData(1.50, 1.00)]
    [InlineData(double.NaN, 0.90)]
    public void A_threshold_outside_the_range_is_clamped_instead_of_refused(double requested, double expected)
    {
        Assert.Equal(expected, WatchStatePolicy.ClampThreshold(requested));
    }

    [Fact]
    public void A_zero_or_negative_duration_is_treated_as_unknown()
    {
        Assert.Equal(
            WatchStatus.NotStarted,
            WatchStatePolicy.Evaluate(TimeSpan.FromSeconds(5), TimeSpan.Zero, 0.90));
        Assert.Equal(
            WatchStatus.InProgress,
            WatchStatePolicy.Evaluate(TimeSpan.FromMinutes(5), TimeSpan.Zero, 0.90));
    }
}
