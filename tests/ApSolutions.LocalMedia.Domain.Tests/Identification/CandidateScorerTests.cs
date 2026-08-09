using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Identification;

public sealed class CandidateScorerTests
{
    [Fact]
    public void Scoring_v1_renormalizes_applicable_weights_and_returns_localizable_signals()
    {
        var scorer = new CandidateScorer();
        var parsed = ParsedMovie(year: 2021);
        var facts = Facts(
            "tmdb:movie:438631",
            CandidateContentKind.Movie,
            title: 0.90,
            year: 1.00,
            duration: 1.00);

        var candidate = scorer.Score(new MediaFileId(Guid.Parse("10000000-0000-0000-0000-000000000001")), parsed, facts);

        Assert.Equal(1, candidate.ScoringModelVersion);
        Assert.Equal(0.9231, candidate.Score, precision: 4);
        Assert.Equal(ReviewState.Automatic, candidate.ReviewState);
        Assert.Equal(
            ["Identification.Signal.Title", "Identification.Signal.Year", "Identification.Signal.Duration"],
            candidate.Signals.Select(signal => signal.Code));
        Assert.All(candidate.ExplanationCodes, code => Assert.StartsWith("Identification.Signal.", code, StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_year_is_neutral_instead_of_lowering_the_score()
    {
        var scorer = new CandidateScorer();
        var candidate = scorer.Score(
            new MediaFileId(Guid.NewGuid()),
            ParsedMovie(year: null),
            Facts("local:arrival", CandidateContentKind.Movie, title: 0.90, duration: 1.00));

        Assert.Equal(0.9091, candidate.Score, precision: 4);
        Assert.DoesNotContain(candidate.Signals, signal => signal.Code == "Identification.Signal.Year");
        Assert.Equal(ReviewState.Automatic, candidate.ReviewState);
    }

    [Fact]
    public void Episode_or_season_contradiction_caps_the_candidate_below_suggestion()
    {
        var scorer = new CandidateScorer();
        var parsed = ParsedEpisode(warnings: []);
        var facts = Facts(
            "tv:1399:s05e10",
            CandidateContentKind.Episode,
            title: 1,
            season: 1,
            episode: 0,
            duration: 1);

        var candidate = scorer.Score(new MediaFileId(Guid.NewGuid()), parsed, facts);

        Assert.Equal(0.59, candidate.Score);
        Assert.Equal(ReviewState.Pending, candidate.ReviewState);
        Assert.Contains("Identification.Warning.EpisodeContradiction", candidate.ExplanationCodes);
    }

    [Fact]
    public void Ambiguous_compact_episode_can_never_be_automatic()
    {
        var scorer = new CandidateScorer();
        var parsed = ParsedEpisode(warnings: ["AmbiguousCompactEpisode"]);
        var facts = Facts(
            "tv:1399:s08e03",
            CandidateContentKind.Episode,
            title: 1,
            season: 1,
            episode: 1,
            duration: 1);

        var candidate = scorer.Score(new MediaFileId(Guid.NewGuid()), parsed, facts);

        Assert.Equal(0.89, candidate.Score);
        Assert.Equal(ReviewState.Suggested, candidate.ReviewState);
        Assert.Contains("Identification.Warning.AmbiguousName", candidate.ExplanationCodes);
    }

    [Fact]
    public void Kind_conflict_rejects_the_candidate()
    {
        var scorer = new CandidateScorer();

        var candidate = scorer.Score(
            new MediaFileId(Guid.NewGuid()),
            ParsedMovie(2021),
            Facts("tv:438631:s01e01", CandidateContentKind.Episode, title: 1));

        Assert.Equal(0, candidate.Score);
        Assert.Equal(ReviewState.Rejected, candidate.ReviewState);
        Assert.Equal(["Identification.Error.KindConflict"], candidate.ExplanationCodes);
    }

    [Fact]
    public void Unknown_parse_kind_and_missing_optional_signals_use_title_only()
    {
        var scorer = new CandidateScorer();
        var parsed = new ParsedMediaName(
            ParsedMediaKind.Unknown,
            "Arrival",
            Year: null,
            Season: null,
            Episode: null,
            AbsoluteEpisode: null,
            IsSpecial: false,
            NoiseTags: [],
            ParseWarnings: []);

        var candidate = scorer.Score(
            new MediaFileId(Guid.NewGuid()),
            parsed,
            Facts("tmdb:movie:329865", CandidateContentKind.Movie, title: 0.75));

        Assert.Equal(0.75, candidate.Score);
        Assert.Equal(ReviewState.Suggested, candidate.ReviewState);
        Assert.Single(candidate.Signals);
    }

    [Fact]
    public void Season_contradiction_alone_caps_the_candidate_below_suggestion()
    {
        var scorer = new CandidateScorer();

        var candidate = scorer.Score(
            new MediaFileId(Guid.NewGuid()),
            ParsedEpisode(warnings: []),
            Facts(
                "tv:1399:s04e10",
                CandidateContentKind.Episode,
                title: 1,
                season: 0,
                episode: 1));

        Assert.Equal(0.59, candidate.Score);
        Assert.Equal(ReviewState.Pending, candidate.ReviewState);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Signal_values_outside_the_closed_unit_interval_are_rejected(double invalidValue)
    {
        var scorer = new CandidateScorer();

        Assert.Throws<ArgumentOutOfRangeException>(() => scorer.Score(
            new MediaFileId(Guid.NewGuid()),
            ParsedMovie(2021),
            Facts("tmdb:movie:438631", CandidateContentKind.Movie, title: invalidValue)));
    }

    [Fact]
    public void Blank_stable_key_is_rejected()
    {
        var scorer = new CandidateScorer();

        Assert.Throws<ArgumentException>(() => scorer.Score(
            new MediaFileId(Guid.NewGuid()),
            ParsedMovie(2021),
            Facts(" ", CandidateContentKind.Movie, title: 1)));
    }

    private static ParsedMediaName ParsedMovie(int? year) => new(
        ParsedMediaKind.Movie,
        "Dune",
        year,
        Season: null,
        Episode: null,
        AbsoluteEpisode: null,
        IsSpecial: false,
        NoiseTags: [],
        ParseWarnings: []);

    private static ParsedMediaName ParsedEpisode(IReadOnlyList<string> warnings) => new(
        ParsedMediaKind.Episode,
        "Northern Chronicles",
        Year: null,
        Season: 5,
        Episode: 10,
        AbsoluteEpisode: null,
        IsSpecial: false,
        NoiseTags: [],
        ParseWarnings: warnings);

    private static CandidateFacts Facts(
        string stableKey,
        CandidateContentKind kind,
        double title,
        double? season = null,
        double? episode = null,
        double? year = null,
        double? duration = null) => new(
            new CandidateId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
            stableKey,
            kind,
            TitleSimilarity: title,
            SeasonMatch: season,
            EpisodeMatch: episode,
            YearMatch: year,
            DurationMatch: duration);
}
