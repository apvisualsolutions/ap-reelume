// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Continuity;

/// <summary>
/// The rules for intro, recap, and credits ranges: a valid range, no overlap with another of the same
/// kind, and a skip button that only exists while the position is inside the range.
/// </summary>
public sealed class MarkerPolicyTests
{
    private static readonly SeriesId Series = new(Guid.Parse("a7c40001-0000-4000-8000-000000000001"));

    private static readonly TimeSpan Episode = TimeSpan.FromMinutes(50);

    [Theory]
    [InlineData(0, 90, true)]
    [InlineData(30, 90, true)]
    [InlineData(0, 3_000, true)]
    [InlineData(-1, 90, false)]
    [InlineData(90, 90, false)]
    [InlineData(120, 90, false)]
    [InlineData(0, 3_001, false)]
    public void A_range_must_start_at_or_after_zero_and_end_inside_the_episode(
        double startSeconds,
        double endSeconds,
        bool expected)
    {
        var valid = MarkerPolicy.IsValidRange(
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(endSeconds),
            Episode);

        Assert.Equal(expected, valid);
    }

    [Fact]
    public void Without_a_known_duration_only_the_order_of_the_two_points_is_checked()
    {
        Assert.True(MarkerPolicy.IsValidRange(TimeSpan.FromSeconds(10), TimeSpan.FromHours(9), duration: null));
        Assert.False(MarkerPolicy.IsValidRange(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), duration: null));
    }

    [Fact]
    public void Two_ranges_of_the_same_kind_may_not_overlap()
    {
        var existing = Marker(MarkerKind.Intro, 30, 120);

        Assert.NotNull(Conflict(existing, MarkerKind.Intro, 100, 180));
        Assert.NotNull(Conflict(existing, MarkerKind.Intro, 0, 60));
        Assert.NotNull(Conflict(existing, MarkerKind.Intro, 60, 90));
        Assert.NotNull(Conflict(existing, MarkerKind.Intro, 0, 200));
    }

    [Fact]
    public void Ranges_that_only_touch_at_the_edges_do_not_overlap()
    {
        var existing = Marker(MarkerKind.Intro, 30, 120);

        Assert.Null(Conflict(existing, MarkerKind.Intro, 120, 180));
        Assert.Null(Conflict(existing, MarkerKind.Intro, 0, 30));
    }

    [Fact]
    public void A_range_of_a_different_kind_may_share_the_same_seconds()
    {
        var existing = Marker(MarkerKind.Intro, 30, 120);

        Assert.Null(Conflict(existing, MarkerKind.Recap, 30, 120));
        Assert.Null(Conflict(existing, MarkerKind.Credits, 60, 90));
    }

    [Fact]
    public void A_marker_being_edited_does_not_conflict_with_itself()
    {
        var existing = Marker(MarkerKind.Intro, 30, 120);

        Assert.Null(MarkerPolicy.FindConflict(
            [existing],
            existing.Id,
            MarkerKind.Intro,
            TimeSpan.FromSeconds(40),
            TimeSpan.FromSeconds(130)));
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(90, true)]
    [InlineData(119, true)]
    [InlineData(120, false)]
    [InlineData(200, false)]
    public void The_button_exists_only_while_the_position_is_inside_the_range(
        double positionSeconds,
        bool expected)
    {
        var marker = Marker(MarkerKind.Intro, 30, 120);

        Assert.Equal(expected, MarkerPolicy.IsButtonVisible(marker, TimeSpan.FromSeconds(positionSeconds)));
    }

    [Fact]
    public void Skipping_lands_exactly_on_the_end_of_the_range()
    {
        var marker = Marker(MarkerKind.Credits, 2_800, 3_000);

        Assert.Equal(TimeSpan.FromSeconds(3_000), MarkerPolicy.SkipTarget(marker));
    }

    [Fact]
    public void The_active_marker_is_the_one_the_position_is_inside_of()
    {
        var markers = new[]
        {
            Marker(MarkerKind.Intro, 30, 120),
            Marker(MarkerKind.Credits, 2_800, 3_000),
        };

        Assert.Equal(markers[0].Id, MarkerPolicy.FindActive(markers, TimeSpan.FromSeconds(60))!.Id);
        Assert.Equal(markers[1].Id, MarkerPolicy.FindActive(markers, TimeSpan.FromSeconds(2_900))!.Id);
        Assert.Null(MarkerPolicy.FindActive(markers, TimeSpan.FromSeconds(1_000)));
    }

    [Fact]
    public void The_model_keeps_the_fields_a_future_detector_will_need_without_using_them()
    {
        var manual = Marker(MarkerKind.Intro, 30, 120);

        Assert.Equal(MarkerOrigin.Manual, manual.Origin);
        Assert.Null(manual.Confidence);
        Assert.False(manual.UserCorrected);
        Assert.Equal(Series, manual.SeriesId);
    }

    private static IntroMarker? Conflict(
        IntroMarker existing,
        MarkerKind kind,
        double startSeconds,
        double endSeconds) =>
        MarkerPolicy.FindConflict(
            [existing],
            null,
            kind,
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(endSeconds));

    private static IntroMarker Marker(MarkerKind kind, double startSeconds, double endSeconds) =>
        new(
            Guid.Parse("a7c40001-0000-4000-8000-0000000000f1"),
            Series,
            kind,
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(endSeconds),
            MarkerOrigin.Manual,
            Confidence: null,
            UserCorrected: false);
}
