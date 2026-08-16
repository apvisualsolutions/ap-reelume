// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Discovery;

public sealed class TitleFileNamePolicyTests
{
    [Theory]
    [InlineData("Arrival", 2016, ".mkv", "Arrival (2016).mkv")]
    [InlineData("Arrival", null, ".mkv", "Arrival.mkv")]
    [InlineData("  Northern   Chronicles ", 2016, ".mkv", "Northern Chronicles (2016).mkv")]
    [InlineData("Arrival", 2016, "mkv", "Arrival (2016).mkv")]
    [InlineData("Arrival", 2016, "  ", "Arrival (2016)")]
    public void A_film_is_its_title_and_its_year(string title, int? year, string extension, string expected)
    {
        var name = TitleFileNamePolicy.Compose(new TitleNaming(title, year, Extension: extension));

        Assert.Equal(expected, name);
    }

    /// <summary>
    /// A year outside four digits is not a year the convention can write, so it is left out rather
    /// than written wrong: the name still improves, and nothing claims a release that never happened.
    /// </summary>
    [Theory]
    [InlineData(999)]
    [InlineData(10_000)]
    [InlineData(0)]
    [InlineData(-1)]
    public void An_impossible_year_is_left_out_instead_of_written(int year)
    {
        var name = TitleFileNamePolicy.Compose(new TitleNaming("Arrival", year, Extension: ".mkv"));

        Assert.Equal("Arrival.mkv", name);
    }

    [Theory]
    [InlineData(2016, "The Storm", "Northern Chronicles (2016) - S01E02 - The Storm.mkv")]
    [InlineData(2016, null, "Northern Chronicles (2016) - S01E02.mkv")]
    [InlineData(2016, "   ", "Northern Chronicles (2016) - S01E02.mkv")]
    [InlineData(null, "The Storm", "Northern Chronicles - S01E02 - The Storm.mkv")]
    public void An_episode_carries_its_series_its_number_and_its_own_title(
        int? year,
        string? episodeTitle,
        string expected)
    {
        var name = TitleFileNamePolicy.Compose(new TitleNaming(
            "Northern Chronicles",
            year,
            SeasonNumber: 1,
            EpisodeNumber: 2,
            EpisodeTitle: episodeTitle,
            Extension: ".mkv"));

        Assert.Equal(expected, name);
    }

    /// <summary>
    /// Season zero is the specials season and is written like any other; an episode past ninety-nine
    /// grows a digit rather than losing one, which is what padding to a fixed width would do.
    /// </summary>
    [Theory]
    [InlineData(0, 5, "Northern Chronicles - S00E05.mkv")]
    [InlineData(1, 100, "Northern Chronicles - S01E100.mkv")]
    [InlineData(12, 3, "Northern Chronicles - S12E03.mkv")]
    public void Specials_and_long_seasons_are_numbered_without_losing_a_digit(
        int season,
        int episode,
        string expected)
    {
        var name = TitleFileNamePolicy.Compose(new TitleNaming(
            "Northern Chronicles",
            SeasonNumber: season,
            EpisodeNumber: episode,
            Extension: ".mkv"));

        Assert.Equal(expected, name);
    }

    /// <summary>
    /// Half an episode number, or a negative one, describes an entry that is not placed in its
    /// series. Writing it as a film would be a claim about it that nobody made.
    /// </summary>
    [Theory]
    [InlineData(1, null)]
    [InlineData(null, 2)]
    [InlineData(-1, 2)]
    [InlineData(1, -2)]
    public void An_incomplete_episode_number_proposes_nothing(int? season, int? episode)
    {
        var name = TitleFileNamePolicy.Compose(new TitleNaming(
            "Northern Chronicles",
            SeasonNumber: season,
            EpisodeNumber: episode,
            Extension: ".mkv"));

        Assert.Null(name);
    }

    /// <summary>
    /// An entry nobody has identified has no better name than the one the file already carries, and
    /// this says so instead of proposing a rename to nothing.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_entry_without_a_title_proposes_nothing(string? title)
    {
        var name = TitleFileNamePolicy.Compose(new TitleNaming(title, 2016, Extension: ".mkv"));

        Assert.Null(name);
    }

    /// <summary>
    /// The characters Windows refuses are not this policy's business: <see cref="RenamePolicy"/>
    /// sanitizes every destination it is given, and a second sanitizer would be a second opinion
    /// about what is safe to write.
    /// </summary>
    [Fact]
    public void Sanitizing_is_left_to_the_policy_that_owns_it()
    {
        var name = TitleFileNamePolicy.Compose(new TitleNaming("Blade Runner: Final Cut", 2007, Extension: ".mkv"));

        Assert.Equal("Blade Runner: Final Cut (2007).mkv", name);
    }

    [Fact]
    public void Nothing_at_all_is_refused_rather_than_answered()
    {
        Assert.Throws<ArgumentNullException>(() => TitleFileNamePolicy.Compose(null!));
    }
}
