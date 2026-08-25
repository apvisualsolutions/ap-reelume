// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Identification;

/// <summary>
/// A folder of episodes is a series, and this is the rule that says so.
/// </summary>
/// <remarks>
/// Written against the two shows the owner actually put on the disk on 2026-08-25 — eight seasons
/// and seventy-four episodes of one, three and twenty-five of the other — which arrived as a hundred
/// and two loose cards because nothing in the application ever asked where an episode belonged.
/// </remarks>
public sealed class LocalSeriesPolicyTests
{
    private static readonly Guid Root = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherRoot = new("22222222-2222-2222-2222-222222222222");
    private static readonly MediaNameParser Parser = new();

    [Theory]
    [InlineData("Temporada 1")]
    [InlineData("Season 1")]
    [InlineData("S01")]
    [InlineData("T01")]
    [InlineData("temporada  1")]
    public void A_season_folder_lets_the_folder_above_it_name_the_show(string seasonFolder)
    {
        var placement = Place(["Juego de Tronos", seasonFolder], "S01E03.1080p.mkv");

        Assert.NotNull(placement);
        Assert.Equal("Juego de Tronos", placement!.SeriesTitle);
        Assert.Equal(1, placement.SeasonNumber);
        Assert.Equal(3, placement.EpisodeNumber);
    }

    [Fact]
    public void Without_a_season_folder_the_show_is_the_folder_the_file_is_in()
    {
        var placement = Place(["La Casa del Dragon"], "La.Casa.del.Dragon.S02E05.mkv");

        Assert.NotNull(placement);
        Assert.Equal("La Casa del Dragon", placement!.SeriesTitle);
        Assert.Equal(2, placement.SeasonNumber);
        Assert.Equal(5, placement.EpisodeNumber);
    }

    /// <summary>
    /// The folder is what names the show, and the file name is the fallback and not the other way
    /// round.
    /// </summary>
    /// <remarks>
    /// A file called <c>Juego.de.Tronos.S01E03.1080p.WEB-DL.mkv</c> parses to a clean title that is
    /// the show's name plus whatever survived the noise list. The folder says it once, correctly, and
    /// that is the whole reason to prefer it.
    /// </remarks>
    [Fact]
    public void The_folders_name_wins_over_the_files()
    {
        var placement = Place(["Juego de Tronos (2011)", "Temporada 1"], "jdt.s01e03.1080p.mkv");

        Assert.NotNull(placement);
        Assert.Equal("Juego de Tronos", placement!.SeriesTitle);
    }

    [Fact]
    public void An_episode_loose_in_a_root_falls_back_to_the_name_it_carries()
    {
        var placement = Place([], "Puerto Sombra S02E05.mkv");

        Assert.NotNull(placement);
        Assert.Equal("Puerto Sombra", placement!.SeriesTitle);
        Assert.Equal(2, placement.SeasonNumber);
        Assert.Equal(5, placement.EpisodeNumber);
    }

    [Fact]
    public void A_season_folder_directly_in_a_root_still_falls_back_to_the_file()
    {
        var placement = Place(["Temporada 4"], "Registro Nocturno 4x02.mkv");

        Assert.NotNull(placement);
        Assert.Equal("Registro Nocturno", placement!.SeriesTitle);
        Assert.Equal(4, placement.SeasonNumber);
        Assert.Equal(2, placement.EpisodeNumber);
    }

    [Fact]
    public void A_film_is_not_a_series_and_gets_no_placement()
    {
        Assert.Null(Place(["Cine"], "El Faro de Piedra 2019.mkv"));
    }

    [Fact]
    public void A_name_that_says_nothing_at_all_gets_no_placement()
    {
        Assert.Null(Place([], "video0001.mkv"));
    }

    /// <summary>
    /// A parse that yields neither a folder name nor a title of its own has nothing to be a series.
    /// </summary>
    /// <remarks>
    /// The empty folder is what makes this reachable: <c>1x02</c> alone parses as an episode with an
    /// empty clean title, so both the folder arm and the fallback come back with nothing.
    /// </remarks>
    [Fact]
    public void An_episode_with_no_name_anywhere_gets_no_placement()
    {
        Assert.Null(Place([string.Empty], "1x02.mkv"));
    }

    [Fact]
    public void Two_episodes_of_one_show_meet_at_one_key_and_one_identifier()
    {
        var first = Place(["Juego de Tronos", "Temporada 1"], "S01E01.mkv");
        var last = Place(["Juego de Tronos", "Temporada 8"], "S08E06.mkv");

        Assert.NotNull(first);
        Assert.NotNull(last);
        Assert.Equal(first!.SeriesKey, last!.SeriesKey);
        Assert.Equal(
            LocalSeriesPolicy.ShowIdFor(first.SeriesKey),
            LocalSeriesPolicy.ShowIdFor(last.SeriesKey));
    }

    [Fact]
    public void Two_shows_never_do()
    {
        var thrones = Place(["Juego de Tronos", "Temporada 1"], "S01E01.mkv");
        var dragon = Place(["La Casa del Dragon", "Temporada 1"], "S01E01.mkv");

        Assert.NotEqual(thrones!.SeriesKey, dragon!.SeriesKey);
    }

    /// <summary>
    /// A folder of the same name under another root is another entry, on purpose.
    /// </summary>
    /// <remarks>
    /// A library and its backup hold folders with identical names. Merging them by name would fold a
    /// copy into the original and count its episodes twice, which is worse than showing two entries
    /// for what the disk really holds twice.
    /// </remarks>
    [Fact]
    public void The_same_folder_under_another_root_is_another_series()
    {
        var original = LocalSeriesPolicy.Place(
            Root,
            FileNameContext.ForFile(@"D:\Series\Juego de Tronos\S01E01.mkv", @"D:\Series"),
            Parser.Parse(FileNameContext.ForFile(@"D:\Series\Juego de Tronos\S01E01.mkv", @"D:\Series")));
        var backup = LocalSeriesPolicy.Place(
            OtherRoot,
            FileNameContext.ForFile(@"E:\Respaldo\Juego de Tronos\S01E01.mkv", @"E:\Respaldo"),
            Parser.Parse(FileNameContext.ForFile(@"E:\Respaldo\Juego de Tronos\S01E01.mkv", @"E:\Respaldo")));

        Assert.NotEqual(original!.SeriesKey, backup!.SeriesKey);
    }

    [Fact]
    public void An_episode_titled_after_its_own_show_carries_no_episode_title()
    {
        var placement = Place(["Juego de Tronos", "Temporada 1"], "Juego de Tronos S01E01.mkv");

        Assert.Equal(string.Empty, placement!.EpisodeTitle);
    }

    [Fact]
    public void An_episode_with_a_name_of_its_own_keeps_it()
    {
        var placement = Place(["Puerto Sombra", "Temporada 2"], "Puerto de invierno S02E05.mkv");

        Assert.Equal("Puerto de invierno", placement!.EpisodeTitle);
    }

    [Fact]
    public void Every_episode_of_a_season_gets_an_identifier_of_its_own()
    {
        var key = Place(["Juego de Tronos", "Temporada 1"], "S01E01.mkv")!.SeriesKey;

        Assert.NotEqual(
            LocalSeriesPolicy.EpisodeIdFor(key, 1, 1),
            LocalSeriesPolicy.EpisodeIdFor(key, 1, 2));
        Assert.NotEqual(
            LocalSeriesPolicy.EpisodeIdFor(key, 1, 1),
            LocalSeriesPolicy.EpisodeIdFor(key, 2, 1));
        Assert.Equal(
            LocalSeriesPolicy.EpisodeIdFor(key, 1, 1),
            LocalSeriesPolicy.EpisodeIdFor(key, 1, 1));
        Assert.NotEqual(LocalSeriesPolicy.ShowIdFor(key), LocalSeriesPolicy.EpisodeIdFor(key, 1, 1));
    }

    /// <summary>
    /// The derived identifiers are well-formed UUIDs and not sixteen bytes wearing a GUID's clothes.
    /// </summary>
    [Fact]
    public void The_derived_identifiers_carry_a_version_and_a_variant()
    {
        var id = LocalSeriesPolicy.ShowIdFor("root/juego de tronos").ToByteArray();

        Assert.Equal(0x80, id[7] & 0xF0);
        Assert.Equal(0x80, id[8] & 0xC0);
    }

    [Fact]
    public void The_policy_refuses_what_it_cannot_read()
    {
        Assert.Throws<ArgumentNullException>(() =>
            LocalSeriesPolicy.Place(Root, null!, Parser.Parse(new FileNameContext("a.mkv", []))));
        Assert.Throws<ArgumentNullException>(() =>
            LocalSeriesPolicy.Place(Root, new FileNameContext("a.mkv", []), null!));
        Assert.Throws<ArgumentException>(() => LocalSeriesPolicy.ShowIdFor("  "));
        Assert.Throws<ArgumentException>(() => LocalSeriesPolicy.EpisodeIdFor("  ", 1, 1));
    }

    private static LocalSeriesPlacement? Place(string[] folders, string fileName)
    {
        var context = new FileNameContext(fileName, folders);
        return LocalSeriesPolicy.Place(Root, context, Parser.Parse(context));
    }
}
