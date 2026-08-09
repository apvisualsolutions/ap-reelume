using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Personalization;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Personalization;

/// <summary>
/// Recommendation v1 is a fixed, local, explainable formula: genres 0.40, cast 0.25, rating affinity
/// 0.20, year proximity 0.10, freshness 0.05. Nothing here reads a network or builds a remote profile.
/// </summary>
public sealed class RecommendationPolicyTests
{
    [Fact]
    public void The_approved_weights_and_the_model_version_are_exactly_the_policy_constants()
    {
        Assert.Equal(1, RecommendationPolicy.ScoringModelVersion);
        Assert.Equal(0.40, RecommendationPolicy.GenreWeight, 10);
        Assert.Equal(0.25, RecommendationPolicy.CastWeight, 10);
        Assert.Equal(0.20, RecommendationPolicy.RatingWeight, 10);
        Assert.Equal(0.10, RecommendationPolicy.YearWeight, 10);
        Assert.Equal(0.05, RecommendationPolicy.FreshnessWeight, 10);
        Assert.Equal(
            1.0,
            RecommendationPolicy.GenreWeight
            + RecommendationPolicy.CastWeight
            + RecommendationPolicy.RatingWeight
            + RecommendationPolicy.YearWeight
            + RecommendationPolicy.FreshnessWeight,
            10);
    }

    [Fact]
    public void A_brand_new_catalog_still_ranks_by_freshness_alone_and_says_so()
    {
        var taste = RecommendationTaste.Empty;
        var candidates = new[]
        {
            Candidate(1, ["Drama"], ["Ada"], 2016, isWatched: false),
            Candidate(2, ["Drama"], ["Ada"], 2016, isWatched: true),
        };

        var ranked = RecommendationPolicy.Rank(taste, candidates);

        Assert.Equal(2, ranked.Count);
        Assert.Equal(Title(1), ranked[0].ContentId);
        Assert.Equal(RecommendationPolicy.FreshnessWeight, ranked[0].Score, 10);
        Assert.Equal([RecommendationReason.Freshness], ranked[0].ReasonCodes);
        Assert.Equal(0d, ranked[1].Score, 10);
        Assert.Empty(ranked[1].ReasonCodes);
    }

    [Fact]
    public void A_liked_genre_raises_a_candidate_and_a_disliked_one_lowers_it()
    {
        var taste = new RecommendationTaste(
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Drama"] = 1.0,
                ["Terror"] = -1.0,
            },
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            AverageRating: null,
            PreferredYear: null);

        var ranked = RecommendationPolicy.Rank(
            taste,
            [
                Candidate(1, ["Drama"], [], null, isWatched: true),
                Candidate(2, ["Terror"], [], null, isWatched: true),
                Candidate(3, ["Comedia"], [], null, isWatched: true),
            ]);

        Assert.Equal(Title(1), ranked[0].ContentId);
        Assert.Equal(RecommendationPolicy.GenreWeight, ranked[0].Score, 10);
        Assert.Equal(Title(3), ranked[1].ContentId);
        Assert.Equal(0d, ranked[1].Score, 10);
        Assert.Equal(Title(2), ranked[2].ContentId);
        Assert.Equal(-RecommendationPolicy.GenreWeight, ranked[2].Score, 10);
    }

    [Fact]
    public void Cast_affinity_contributes_its_own_weight_and_its_own_reason()
    {
        var taste = new RecommendationTaste(
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Ada"] = 1.0 },
            AverageRating: null,
            PreferredYear: null);

        var ranked = RecommendationPolicy.Rank(taste, [Candidate(1, [], ["Ada"], null, isWatched: true)]);

        var single = Assert.Single(ranked);
        Assert.Equal(RecommendationPolicy.CastWeight, single.Score, 10);
        Assert.Equal([RecommendationReason.CastMatch], single.ReasonCodes);
    }

    [Theory]
    [InlineData(10, 1.0)]
    [InlineData(1, -1.0)]
    [InlineData(6, 0.1111111111)]
    public void A_personal_rating_maps_onto_the_affinity_signal(int rating, double expectedAffinity)
    {
        var ranked = RecommendationPolicy.Rank(
            RecommendationTaste.Empty,
            [Candidate(1, [], [], null, isWatched: true, rating: rating)]);

        var single = Assert.Single(ranked);
        Assert.Equal(RecommendationPolicy.RatingWeight * expectedAffinity, single.Score, 6);
        Assert.Contains(RecommendationReason.RatingAffinity, single.ReasonCodes);
    }

    [Fact]
    public void Year_proximity_falls_to_nothing_twenty_years_away_and_is_full_on_the_same_year()
    {
        var taste = new RecommendationTaste(
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            AverageRating: null,
            PreferredYear: 2000);

        var ranked = RecommendationPolicy.Rank(
            taste,
            [
                Candidate(1, [], [], 2000, isWatched: true),
                Candidate(2, [], [], 2010, isWatched: true),
                Candidate(3, [], [], 1980, isWatched: true),
                Candidate(4, [], [], null, isWatched: true),
            ]).ToDictionary(item => item.ContentId, item => item);

        Assert.Equal(RecommendationPolicy.YearWeight, ranked[Title(1)].Score, 10);
        Assert.Equal(RecommendationPolicy.YearWeight * 0.5, ranked[Title(2)].Score, 10);
        Assert.Equal(0d, ranked[Title(3)].Score, 10);
        Assert.Equal(0d, ranked[Title(4)].Score, 10);
        Assert.Empty(ranked[Title(4)].ReasonCodes);
    }

    [Fact]
    public void The_explanation_lists_the_non_zero_signals_heaviest_first()
    {
        var taste = new RecommendationTaste(
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Drama"] = 0.5 },
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Ada"] = 0.5 },
            AverageRating: null,
            PreferredYear: 2016);

        var single = Assert.Single(RecommendationPolicy.Rank(
            taste,
            [Candidate(1, ["Drama"], ["Ada"], 2016, isWatched: false, rating: 9)]));

        Assert.Equal(
            [
                RecommendationReason.GenreMatch,
                RecommendationReason.CastMatch,
                RecommendationReason.RatingAffinity,
                RecommendationReason.YearProximity,
                RecommendationReason.Freshness,
            ],
            single.ReasonCodes);
    }

    [Fact]
    public void Ties_break_on_the_identifier_so_two_runs_produce_the_same_order()
    {
        var candidates = Enumerable.Range(1, 50)
            .Select(seed => Candidate(seed, ["Drama"], [], 2016, isWatched: false))
            .Reverse()
            .ToArray();

        var first = RecommendationPolicy.Rank(RecommendationTaste.Empty, candidates);
        var second = RecommendationPolicy.Rank(RecommendationTaste.Empty, candidates.Reverse().ToArray());

        Assert.Equal(first.Select(item => item.ContentId), second.Select(item => item.ContentId));
        Assert.Equal(
            first.Select(item => item.ContentId),
            first.Select(item => item.ContentId).OrderBy(id => id.Value.ToString("D"), StringComparer.Ordinal));
    }

    [Fact]
    public void Summarizing_history_produces_affinities_between_minus_one_and_one()
    {
        var taste = RecommendationPolicy.Summarize(
            [
                new WatchedTitle(Title(1), ["Drama", "Terror"], ["Ada"], 2010, 10),
                new WatchedTitle(Title(2), ["Terror"], ["Bob"], 2020, 1),
                new WatchedTitle(Title(3), ["Drama"], ["Ada"], 2012, null),
            ]);

        Assert.InRange(taste.Genres["Drama"], 0.0, 1.0);
        Assert.InRange(taste.Genres["Terror"], -1.0, 1.0);
        Assert.True(taste.Genres["Drama"] > taste.Genres["Terror"]);
        Assert.True(taste.Cast["Ada"] > taste.Cast["Bob"]);
        Assert.Equal(5.5, taste.AverageRating!.Value, 6);
        Assert.NotNull(taste.PreferredYear);
        Assert.InRange(taste.PreferredYear!.Value, 2010, 2020);
    }

    [Fact]
    public void Summarizing_nothing_gives_the_empty_taste_rather_than_a_failure()
    {
        var taste = RecommendationPolicy.Summarize([]);

        Assert.Empty(taste.Genres);
        Assert.Empty(taste.Cast);
        Assert.Null(taste.AverageRating);
        Assert.Null(taste.PreferredYear);
        Assert.Equal(RecommendationTaste.Empty, taste);
    }

    [Fact]
    public void Two_tastes_with_the_same_affinities_compare_as_equal_whatever_the_dictionary()
    {
        var first = new RecommendationTaste(
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Drama"] = 0.5 },
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Ada"] = 0.25 },
            AverageRating: 7,
            PreferredYear: 2016);
        var second = new RecommendationTaste(
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Drama"] = 0.5 },
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Ada"] = 0.25 },
            AverageRating: 7,
            PreferredYear: 2016);
        var different = second with { AverageRating = 8 };
        var extraGenre = new RecommendationTaste(
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Drama"] = 0.5, ["Terror"] = 0.1 },
            second.Cast,
            second.AverageRating,
            second.PreferredYear);
        var otherAffinity = new RecommendationTaste(
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Drama"] = 0.9 },
            second.Cast,
            second.AverageRating,
            second.PreferredYear);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, different);
        Assert.NotEqual(first, extraGenre);
        Assert.NotEqual(first, otherAffinity);
        Assert.False(first.Equals(null));
        Assert.False(first.IsEmpty);
        Assert.True(RecommendationTaste.Empty.IsEmpty);
    }

    [Fact]
    public void The_policy_refuses_missing_input_rather_than_inventing_a_result()
    {
        Assert.Throws<ArgumentNullException>(() => RecommendationPolicy.Rank(null!, []));
        Assert.Throws<ArgumentNullException>(() => RecommendationPolicy.Rank(RecommendationTaste.Empty, null!));
        Assert.Throws<ArgumentNullException>(() => RecommendationPolicy.Summarize(null!));
    }

    [Fact]
    public void No_provider_or_remote_type_lives_in_the_recommendation_namespace()
    {
        var forbidden = typeof(RecommendationPolicy).Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "ApSolutions.LocalMedia.Domain.Personalization",
                StringComparison.Ordinal) is true)
            .Where(type => type.Name.Contains("Http", StringComparison.OrdinalIgnoreCase)
                || type.Name.Contains("Remote", StringComparison.OrdinalIgnoreCase)
                || type.Name.Contains("Provider", StringComparison.OrdinalIgnoreCase)
                || type.Name.Contains("Telemetry", StringComparison.OrdinalIgnoreCase))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(forbidden);
    }

    private static RecommendationCandidate Candidate(
        int seed,
        string[] genres,
        string[] cast,
        int? year,
        bool isWatched,
        int? rating = null) => new(
        Title(seed),
        genres,
        cast,
        year,
        IsAvailable: true,
        isWatched,
        rating);

    private static TitleId Title(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new TitleId(new Guid(bytes));
    }
}
