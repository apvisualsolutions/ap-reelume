// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Personalization;

/// <summary>
/// Personal state is three independent facts about one piece of content. Nothing here is a profile,
/// a collection, or a list: those are deliberately outside the MVP.
/// </summary>
public sealed class PersonalStateTests
{
    private static readonly ContentKey Content = ContentKey.ForTitle(
        new TitleId(Guid.Parse("f1000000-0000-4000-8000-000000000001")));

    [Fact]
    public void An_empty_state_is_neither_marked_nor_rated()
    {
        var state = PersonalState.Empty(Content);

        Assert.Equal(Content, state.Content);
        Assert.False(state.IsFavorite);
        Assert.False(state.IsWatchLater);
        Assert.Null(state.Rating);
        Assert.False(state.HasRating);
        Assert.True(state.IsEmpty);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void A_rating_inside_one_to_five_is_accepted(int rating)
    {
        var state = PersonalState.Empty(Content).WithRating(rating);

        Assert.Equal(rating, state.Rating);
        Assert.True(state.HasRating);
        Assert.False(state.IsEmpty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void A_rating_outside_one_to_five_is_rejected_rather_than_clamped(int rating)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => PersonalState.Empty(Content).WithRating(rating));

        Assert.Equal("rating", exception.ParamName);
        Assert.False(PersonalStatePolicy.IsValidRating(rating));
    }

    [Fact]
    public void Clearing_a_rating_is_a_null_and_leaves_the_other_two_facts_alone()
    {
        var rated = PersonalState.Empty(Content)
            .WithFavorite(true)
            .WithWatchLater(true)
            .WithRating(4);

        var cleared = rated.WithRating(null);

        Assert.Null(cleared.Rating);
        Assert.False(cleared.HasRating);
        Assert.True(cleared.IsFavorite);
        Assert.True(cleared.IsWatchLater);
        Assert.False(cleared.IsEmpty);
        Assert.True(PersonalStatePolicy.IsValidRating(null));
    }

    [Fact]
    public void Setting_the_same_value_twice_changes_nothing()
    {
        var once = PersonalState.Empty(Content).WithFavorite(true);
        var twice = once.WithFavorite(true);

        Assert.Equal(once, twice);
        Assert.Equal(once.WithWatchLater(false), once);
        Assert.Equal(once.WithRating(null), once);
    }

    [Fact]
    public void Toggling_alternates_and_toggling_twice_returns_to_the_start()
    {
        var start = PersonalState.Empty(Content);

        var favorite = start.ToggleFavorite();
        Assert.True(favorite.IsFavorite);
        Assert.Equal(start, favorite.ToggleFavorite());

        var watchLater = start.ToggleWatchLater();
        Assert.True(watchLater.IsWatchLater);
        Assert.Equal(start, watchLater.ToggleWatchLater());
    }

    [Fact]
    public void The_three_facts_are_independent_of_each_other()
    {
        var state = PersonalState.Empty(Content).WithFavorite(true);

        Assert.False(state.IsWatchLater);
        Assert.Null(state.Rating);

        state = state.WithRating(2);
        Assert.True(state.IsFavorite);
        Assert.False(state.IsWatchLater);

        state = state.WithFavorite(false);
        Assert.Equal(2, state.Rating);
        Assert.False(state.IsEmpty);
    }

    [Fact]
    public void A_state_with_nothing_marked_is_empty_again_and_can_be_dropped()
    {
        var state = PersonalState.Empty(Content)
            .WithFavorite(true)
            .WithRating(5)
            .WithFavorite(false)
            .WithRating(null);

        Assert.True(state.IsEmpty);
        Assert.True(PersonalStatePolicy.IsEmpty(state));
    }

    [Fact]
    public void Personal_state_belongs_to_content_and_can_key_an_episode()
    {
        var showId = new TitleId(Guid.Parse("f1000000-0000-4000-8000-000000000002"));
        var episodeId = new EpisodeId(Guid.Parse("f1000000-0000-4000-8000-000000000003"));
        var episode = PersonalState.Empty(ContentKey.ForEpisode(showId, episodeId)).WithFavorite(true);

        Assert.Equal(showId, episode.Content.TitleId);
        Assert.Equal(episodeId, episode.Content.EpisodeId);
        Assert.StartsWith("title:", episode.Content.Value, StringComparison.Ordinal);
        Assert.Contains("/episode:", episode.Content.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// One to five since 2026-08-25, and what was stored on the old scale comes across with it.
    /// </summary>
    /// <remarks>
    /// The arithmetic is migration 0020's, written here too because a migration runs once against a
    /// file and this runs against a number: a backup restored from before it, or a value arriving
    /// from anywhere else, is answered by the same rule rather than by a second one.
    /// </remarks>
    [Fact]
    public void The_boundaries_of_the_accepted_range_are_exactly_one_and_five()
    {
        Assert.Equal(1, PersonalStatePolicy.MinimumRating);
        Assert.Equal(5, PersonalStatePolicy.MaximumRating);

        // Halved and rounded up: a 1 survives as one star rather than falling to a zero this
        // application cannot hold, and a 10 lands on the fifth.
        Assert.Equal(1, PersonalStatePolicy.ToFiveStars(1));
        Assert.Equal(1, PersonalStatePolicy.ToFiveStars(2));
        Assert.Equal(2, PersonalStatePolicy.ToFiveStars(3));
        Assert.Equal(5, PersonalStatePolicy.ToFiveStars(9));
        Assert.Equal(5, PersonalStatePolicy.ToFiveStars(10));
        Assert.Equal(5, PersonalStatePolicy.ToFiveStars(int.MaxValue));
        Assert.Null(PersonalStatePolicy.ToFiveStars(null));
        Assert.Null(PersonalStatePolicy.ToFiveStars(0));
        Assert.Null(PersonalStatePolicy.ToFiveStars(-3));
        Assert.False(PersonalStatePolicy.IsValidRating(PersonalStatePolicy.MinimumRating - 1));
        Assert.True(PersonalStatePolicy.IsValidRating(PersonalStatePolicy.MinimumRating));
        Assert.True(PersonalStatePolicy.IsValidRating(PersonalStatePolicy.MaximumRating));
        Assert.False(PersonalStatePolicy.IsValidRating(PersonalStatePolicy.MaximumRating + 1));
    }

    [Fact]
    public void No_profile_or_arbitrary_list_concept_exists_in_the_personalization_namespace()
    {
        var forbidden = typeof(PersonalState).Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "ApSolutions.LocalMedia.Domain.Personalization",
                StringComparison.Ordinal) is true)
            .Where(type => type.Name.Contains("Profile", StringComparison.OrdinalIgnoreCase)
                || type.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase)
                || type.Name.Contains("Playlist", StringComparison.OrdinalIgnoreCase)
                || type.Name.Contains("CustomList", StringComparison.OrdinalIgnoreCase))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbidden);
    }
}
