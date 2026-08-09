using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Continuity;

/// <summary>
/// Which episode follows which. Ordering is season then episode, specials are never chained into by
/// accident, and an episode with no playable file is skipped rather than offered.
/// </summary>
public sealed class NextEpisodePolicyTests
{
    private static readonly TitleId Show = new(Guid.Parse("81f30001-0000-4000-8000-000000000001"));

    [Fact]
    public void Episodes_are_ordered_by_season_and_number_with_specials_last()
    {
        var ordered = NextEpisodePolicy.Order(
        [
            Episode(2, 1),
            Episode(0, 2),
            Episode(1, 10),
            Episode(1, 2),
            Episode(0, 1),
            Episode(1, 1),
        ]);

        Assert.Equal(
            [(1, 1), (1, 2), (1, 10), (2, 1), (0, 1), (0, 2)],
            ordered.Select(entry => (entry.SeasonNumber, entry.EpisodeNumber)));
    }

    [Fact]
    public void The_next_episode_is_the_one_that_follows_in_the_same_season()
    {
        var episodes = new[] { Episode(1, 1), Episode(1, 2), Episode(1, 3) };

        var next = NextEpisodePolicy.FindNext(episodes, episodes[0].Id);

        Assert.Equal(episodes[1].Id, next!.Id);
    }

    [Fact]
    public void The_end_of_a_season_continues_into_the_first_episode_of_the_next()
    {
        var episodes = new[] { Episode(1, 9), Episode(1, 10), Episode(2, 1) };

        var next = NextEpisodePolicy.FindNext(episodes, episodes[1].Id);

        Assert.Equal(episodes[2].Id, next!.Id);
    }

    [Fact]
    public void A_gap_in_the_numbering_is_stepped_over_rather_than_stopping_the_run()
    {
        var episodes = new[] { Episode(1, 1), Episode(1, 4) };

        var next = NextEpisodePolicy.FindNext(episodes, episodes[0].Id);

        Assert.Equal(episodes[1].Id, next!.Id);
    }

    [Fact]
    public void An_unavailable_or_fileless_episode_is_skipped()
    {
        var episodes = new[]
        {
            Episode(1, 1),
            Episode(1, 2, isAvailable: false),
            Episode(1, 3, hasFile: false),
            Episode(1, 4),
        };

        var next = NextEpisodePolicy.FindNext(episodes, episodes[0].Id);

        Assert.Equal(episodes[3].Id, next!.Id);
    }

    [Fact]
    public void A_regular_episode_never_chains_into_a_special()
    {
        var episodes = new[] { Episode(1, 1), Episode(1, 2), Episode(0, 1) };

        var afterLast = NextEpisodePolicy.FindNext(episodes, episodes[1].Id);

        Assert.Null(afterLast);
    }

    [Fact]
    public void A_special_only_chains_into_another_special()
    {
        var episodes = new[] { Episode(0, 1), Episode(0, 2), Episode(1, 1) };

        var next = NextEpisodePolicy.FindNext(episodes, episodes[0].Id);
        var afterLastSpecial = NextEpisodePolicy.FindNext(episodes, episodes[1].Id);

        Assert.Equal(episodes[1].Id, next!.Id);
        Assert.Null(afterLastSpecial);
    }

    [Fact]
    public void The_last_episode_of_the_series_has_no_next()
    {
        var episodes = new[] { Episode(1, 1), Episode(2, 1) };

        Assert.Null(NextEpisodePolicy.FindNext(episodes, episodes[1].Id));
    }

    [Fact]
    public void An_episode_the_series_does_not_contain_has_no_next()
    {
        var episodes = new[] { Episode(1, 1), Episode(1, 2) };

        Assert.Null(NextEpisodePolicy.FindNext(episodes, new EpisodeId(Guid.NewGuid())));
    }

    [Fact]
    public void A_series_whose_remaining_episodes_are_all_missing_has_no_next()
    {
        var episodes = new[]
        {
            Episode(1, 1),
            Episode(1, 2, isAvailable: false),
            Episode(1, 3, isAvailable: false),
        };

        Assert.Null(NextEpisodePolicy.FindNext(episodes, episodes[0].Id));
    }

    [Fact]
    public void An_entry_knows_whether_it_is_special_and_whether_it_can_be_played()
    {
        Assert.True(Episode(0, 1).IsSpecial);
        Assert.False(Episode(1, 1).IsSpecial);
        Assert.True(Episode(1, 1).IsPlayable);
        Assert.False(Episode(1, 1, isAvailable: false).IsPlayable);
        Assert.False(Episode(1, 1, hasFile: false).IsPlayable);
    }

    private static EpisodeSequenceEntry Episode(
        int season,
        int number,
        bool isAvailable = true,
        bool hasFile = true)
    {
        var id = new EpisodeId(Guid.Parse($"81f3{season:D4}-{number:D4}-4000-8000-000000000001"));
        return new EpisodeSequenceEntry(
            id,
            Show,
            season,
            number,
            hasFile ? new MediaFileId(id.Value) : null,
            hasFile ? $@"D:\Media\S{season:D2}E{number:D2}.mkv" : null,
            isAvailable);
    }
}
