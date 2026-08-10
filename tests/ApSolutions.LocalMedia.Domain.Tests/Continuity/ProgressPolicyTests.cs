// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Continuity;

/// <summary>
/// The rules that decide when a position is worth storing and when it is worth offering back. They are
/// pure so the exact boundaries — thirty seconds, five seconds, and the clamp — can be pinned without
/// a database, a clock, or an engine.
/// </summary>
public sealed class ProgressPolicyTests
{
    private static readonly TitleId Movie = new(Guid.Parse("6f1a1f9c-0000-4000-8000-000000000001"));

    [Fact]
    public void The_persistence_interval_and_the_minimum_resume_point_are_the_approved_constants()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), ProgressPolicy.SaveInterval);
        Assert.Equal(TimeSpan.FromSeconds(30), ProgressPolicy.MinimumResumePosition);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(29, false)]
    [InlineData(29.999, false)]
    [InlineData(30, true)]
    [InlineData(31, true)]
    [InlineData(600, true)]
    public void A_position_under_thirty_seconds_never_offers_a_resume(double seconds, bool expected)
    {
        var offered = ProgressPolicy.ShouldOfferResume(
            TimeSpan.FromSeconds(seconds),
            TimeSpan.FromMinutes(60));

        Assert.Equal(expected, offered);
    }

    [Fact]
    public void A_position_at_or_past_the_observed_end_offers_no_resume()
    {
        var duration = TimeSpan.FromMinutes(40);

        Assert.False(ProgressPolicy.ShouldOfferResume(duration, duration));
        Assert.False(ProgressPolicy.ShouldOfferResume(duration + TimeSpan.FromSeconds(10), duration));
        Assert.True(ProgressPolicy.ShouldOfferResume(duration - TimeSpan.FromSeconds(1), duration));
    }

    [Fact]
    public void An_unknown_duration_still_offers_a_resume_past_the_minimum()
    {
        Assert.True(ProgressPolicy.ShouldOfferResume(TimeSpan.FromSeconds(45), duration: null));
        Assert.False(ProgressPolicy.ShouldOfferResume(TimeSpan.FromSeconds(15), duration: null));
    }

    [Fact]
    public void A_position_is_always_clamped_into_the_observed_range()
    {
        var duration = TimeSpan.FromMinutes(10);

        Assert.Equal(TimeSpan.Zero, ProgressPolicy.ClampPosition(TimeSpan.FromSeconds(-30), duration));
        Assert.Equal(duration, ProgressPolicy.ClampPosition(TimeSpan.FromMinutes(12), duration));
        Assert.Equal(TimeSpan.FromMinutes(4), ProgressPolicy.ClampPosition(TimeSpan.FromMinutes(4), duration));
        Assert.Equal(
            TimeSpan.FromMinutes(12),
            ProgressPolicy.ClampPosition(TimeSpan.FromMinutes(12), duration: null));
        Assert.Equal(TimeSpan.Zero, ProgressPolicy.ClampPosition(TimeSpan.FromSeconds(-1), duration: null));
    }

    [Theory]
    [InlineData(PersistenceTrigger.Pause)]
    [InlineData(PersistenceTrigger.Seek)]
    [InlineData(PersistenceTrigger.ModeChange)]
    [InlineData(PersistenceTrigger.FileChange)]
    [InlineData(PersistenceTrigger.Close)]
    [InlineData(PersistenceTrigger.EngineFailure)]
    public void Every_critical_trigger_persists_even_when_the_position_did_not_move(PersistenceTrigger trigger)
    {
        Assert.True(ProgressPolicy.IsCritical(trigger));
        Assert.True(ProgressPolicy.ShouldPersist(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(3), trigger));
    }

    [Fact]
    public void A_tick_that_repeats_the_stored_position_is_debounced()
    {
        Assert.False(ProgressPolicy.IsCritical(PersistenceTrigger.Tick));
        Assert.False(ProgressPolicy.ShouldPersist(
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(3),
            PersistenceTrigger.Tick));
        Assert.False(ProgressPolicy.ShouldPersist(
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(3) + TimeSpan.FromMilliseconds(400),
            PersistenceTrigger.Tick));
        Assert.True(ProgressPolicy.ShouldPersist(
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(3) + ProgressPolicy.SaveInterval,
            PersistenceTrigger.Tick));
        Assert.True(ProgressPolicy.ShouldPersist(null, TimeSpan.FromSeconds(2), PersistenceTrigger.Tick));
    }

    [Fact]
    public void A_duration_of_zero_means_unobserved_rather_than_an_immediate_end()
    {
        Assert.Equal(
            TimeSpan.FromMinutes(12),
            ProgressPolicy.ClampPosition(TimeSpan.FromMinutes(12), TimeSpan.Zero));
        Assert.True(ProgressPolicy.ShouldOfferResume(TimeSpan.FromMinutes(12), TimeSpan.Zero));
    }

    [Fact]
    public void A_content_key_round_trips_and_distinguishes_a_movie_from_an_episode()
    {
        var episodeId = new EpisodeId(Guid.Parse("6f1a1f9c-0000-4000-8000-0000000000e1"));
        var movie = ContentKey.ForTitle(Movie);
        var episode = ContentKey.ForEpisode(Movie, episodeId);

        Assert.NotEqual(movie.Value, episode.Value);
        Assert.Equal(movie, ContentKey.Parse(movie.Value));
        Assert.Equal(episode, ContentKey.Parse(episode.Value));
        Assert.Null(movie.EpisodeId);
        Assert.Equal(episodeId, episode.EpisodeId);
        Assert.Equal(Movie, episode.TitleId);
        Assert.Equal(episode.Value, episode.ToString());
    }

    [Fact]
    public void A_key_that_is_not_a_content_key_is_refused_instead_of_guessed()
    {
        Assert.Throws<FormatException>(() => ContentKey.Parse("file:6f1a1f9c-0000-4000-8000-000000000001"));
        Assert.Throws<ArgumentException>(() => ContentKey.Parse("   "));
    }
}
