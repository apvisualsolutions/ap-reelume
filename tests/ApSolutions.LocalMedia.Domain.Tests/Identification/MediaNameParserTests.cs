// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Identification;

public sealed class MediaNameParserTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static TheoryData<MediaNameCase> ApprovedCases
    {
        get
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "media-name-cases.json");
            var cases = JsonSerializer.Deserialize<MediaNameCase[]>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidOperationException("The approved media-name fixture is empty.");
            var data = new TheoryData<MediaNameCase>();
            foreach (var item in cases)
            {
                data.Add(item);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ApprovedCases))]
    public void Approved_corpus_is_parsed_into_explainable_media_signals(MediaNameCase fixture)
    {
        var parser = new MediaNameParser();

        var parsed = parser.Parse(new FileNameContext(fixture.FileName, fixture.Folders));

        Assert.Equal(fixture.FileName, parsed.OriginalName);
        Assert.Equal(Enum.Parse<ParsedMediaKind>(fixture.Kind), parsed.Kind);
        Assert.Equal(fixture.CleanTitle, parsed.CleanTitle);
        Assert.Equal(fixture.Year, parsed.Year);
        Assert.Equal(fixture.Season, parsed.Season);
        Assert.Equal(fixture.Episode, parsed.Episode);
        Assert.Equal(fixture.AbsoluteEpisode, parsed.AbsoluteEpisode);
        Assert.Equal(fixture.IsSpecial, parsed.IsSpecial);
        Assert.Equal(fixture.NoiseTags, parsed.NoiseTags);
        Assert.Equal(fixture.Warnings, parsed.ParseWarnings);
    }

    [Fact]
    public void Compact_episode_without_supporting_season_context_requires_review()
    {
        var parser = new MediaNameParser();

        var parsed = parser.Parse(new FileNameContext("Serie.Cap.803.mkv", ["Serie"]));

        Assert.Equal(ParsedMediaKind.Unknown, parsed.Kind);
        Assert.Null(parsed.Season);
        Assert.Null(parsed.Episode);
        Assert.Contains("AmbiguousCompactEpisode", parsed.ParseWarnings);
    }

    /// <summary>
    /// A compact number ending in 00 is the special of its season, and the folder is the only thing
    /// that can say which season that is. When the folder agrees with the compact number the season
    /// survives; when there is no folder to agree with, the parser keeps the folder's answer — which
    /// is nothing — rather than trusting the digits it could not confirm. Both arms end up flagged
    /// for review, and that is the point: a special is stored under a season or not at all.
    /// </summary>
    [Fact]
    public void A_compact_special_takes_its_season_from_the_folder_and_never_from_the_digits_alone()
    {
        var parser = new MediaNameParser();

        var confirmed = parser.Parse(new FileNameContext("Serie.Cap.100.mkv", ["Serie", "Temporada 1"]));
        var unconfirmed = parser.Parse(new FileNameContext("Serie.Cap.100.mkv", ["Serie"]));

        Assert.Equal(1, confirmed.Season);
        Assert.True(confirmed.IsSpecial);
        Assert.Contains("AmbiguousCompactEpisode", confirmed.ParseWarnings);
        Assert.Null(unconfirmed.Season);
        Assert.True(unconfirmed.IsSpecial);
        Assert.Contains("AmbiguousCompactEpisode", unconfirmed.ParseWarnings);
    }

    /// <summary>
    /// Four digits are a year only inside the range a film can have been made in. The pattern matches
    /// any four digits, so a resolution, a bitrate or a serial number would otherwise become a release
    /// year and, worse, promote an otherwise unidentified file to Movie on the strength of it.
    /// </summary>
    [Fact]
    public void Four_digits_outside_the_range_a_film_can_come_from_are_not_a_year()
    {
        var parser = new MediaNameParser();

        var future = parser.Parse(new FileNameContext("Pelicula (2200).mkv", []));
        var tooEarly = parser.Parse(new FileNameContext("Pelicula (1800).mkv", []));
        var real = parser.Parse(new FileNameContext("Pelicula (1999).mkv", []));

        Assert.Null(future.Year);
        Assert.Null(tooEarly.Year);
        Assert.Equal(1999, real.Year);
        Assert.Equal(ParsedMediaKind.Movie, real.Kind);
    }

    /// <summary>
    /// The year promotes a file to Movie only when nothing else has claimed it and nothing went wrong.
    /// An episode carries a year without ceasing to be an episode, and a name the parser could not
    /// resolve stays Unknown however plausible its year is — a warning means the reading is in doubt,
    /// and calling it a film would settle a doubt the parser has already raised.
    /// </summary>
    [Fact]
    public void A_year_promotes_to_movie_only_for_an_unclaimed_name_that_raised_no_warning()
    {
        var parser = new MediaNameParser();

        var episode = parser.Parse(new FileNameContext("Serie.S01E02.1999.mkv", ["Serie"]));
        var doubted = parser.Parse(new FileNameContext("Serie.Cap.803.1999.mkv", ["Serie"]));

        Assert.Equal(ParsedMediaKind.Episode, episode.Kind);
        Assert.Equal(1999, episode.Year);
        Assert.Equal(ParsedMediaKind.Unknown, doubted.Kind);
        Assert.Equal(1999, doubted.Year);
        Assert.Contains("AmbiguousCompactEpisode", doubted.ParseWarnings);
    }

    /// <summary>
    /// The folder pattern reads up to three digits, and a season number is at most two. A folder
    /// named for something that merely looks like a season must not hand the parser a season of 150,
    /// because that number would go on to confirm or contradict a compact episode and decide where
    /// the file is filed.
    /// <para>
    /// This closes the upper half of IsValidSeason. The lower half — a season below zero — is
    /// unreachable and was measured so on 2026-08-30 rather than assumed: both callers pass
    /// ParseNumber over a group that every pattern writes as <c>\d{1,3}</c>, so the value is always
    /// in [0, 999]. MediaNameParser.cs therefore tops out at 69 of 70 branches, which is above the
    /// bar and needs no floor of its own.
    /// </para>
    /// </summary>
    [Fact]
    public void A_folder_season_outside_the_possible_range_is_no_season_at_all()
    {
        var parser = new MediaNameParser();

        var impossible = parser.Parse(new FileNameContext("Serie.Cap.100.mkv", ["Serie", "Temporada 150"]));
        var boundary = parser.Parse(new FileNameContext("Serie.Cap.9900.mkv", ["Serie", "Temporada 99"]));

        Assert.Null(impossible.Season);
        Assert.Equal(99, boundary.Season);
    }

    [Fact]
    public void Long_unicode_path_is_bounded_and_preserves_the_source_name()
    {
        var parser = new MediaNameParser();
        var fileName = $"{new string('界', 270)}.S01E02.[1080p].mkv";

        var started = DateTime.UtcNow;
        var parsed = parser.Parse(new FileNameContext(fileName, ["長いシリーズ", "Season 1"]));

        Assert.Equal(fileName, parsed.OriginalName);
        Assert.Equal(ParsedMediaKind.Episode, parsed.Kind);
        Assert.Equal(1, parsed.Season);
        Assert.Equal(2, parsed.Episode);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(2));
    }

    public sealed record MediaNameCase
    {
        public required string FileName { get; init; }

        public string[] Folders { get; init; } = [];

        public required string Kind { get; init; }

        public required string CleanTitle { get; init; }

        public int? Year { get; init; }

        public int? Season { get; init; }

        public int? Episode { get; init; }

        public int? AbsoluteEpisode { get; init; }

        public bool IsSpecial { get; init; }

        public string[] NoiseTags { get; init; } = [];

        public string[] Warnings { get; init; } = [];
    }
}
