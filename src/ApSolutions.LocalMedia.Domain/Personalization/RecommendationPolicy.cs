using System.Globalization;

namespace ApSolutions.LocalMedia.Domain.Personalization;

/// <summary>
/// Recommendation v1: a fixed, local, deterministic formula over the approved weights. Every input
/// comes from this machine, the result is reproducible from the same inputs, and every suggestion
/// carries the signals that produced it, so nothing is unexplainable.
/// </summary>
public static class RecommendationPolicy
{
    /// <summary>Version of the scoring model, recorded so a later change is visible rather than silent.</summary>
    public const int ScoringModelVersion = 1;

    public const double GenreWeight = 0.40;

    public const double CastWeight = 0.25;

    public const double RatingWeight = 0.20;

    public const double YearWeight = 0.10;

    public const double FreshnessWeight = 0.05;

    /// <summary>Beyond this many years apart, year proximity contributes nothing.</summary>
    private const double YearHorizon = 20;

    /// <summary>The middle of the one-to-ten range; a rating above it is liked, below it is not.</summary>
    private const double RatingMidpoint = 5.5;

    private const double RatingHalfRange = 4.5;

    /// <summary>
    /// Turns watched history into affinities. A title that was watched but never rated counts as
    /// neutral rather than liked, because watching something is not the same as enjoying it.
    /// </summary>
    public static RecommendationTaste Summarize(IEnumerable<WatchedTitle> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        var watched = history.ToArray();
        if (watched.Length == 0)
        {
            return RecommendationTaste.Empty;
        }

        var genres = Average(watched.SelectMany(title => title.Genres.Select(genre => (genre, Affinity(title.Rating)))));
        var cast = Average(watched.SelectMany(title => title.Cast.Select(person => (person, Affinity(title.Rating)))));
        var ratings = watched.Where(title => title.Rating is not null).Select(title => (double)title.Rating!.Value).ToArray();
        var years = watched.Where(title => title.Year is not null).Select(title => title.Year!.Value).ToArray();
        return new RecommendationTaste(
            genres,
            cast,
            ratings.Length == 0 ? null : ratings.Average(),
            years.Length == 0 ? null : (int)Math.Round(years.Average(), MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Scores and orders the candidates. Ties break on the identifier, so the same catalogue always
    /// produces the same order and the rail does not shuffle between two visits to Home.
    /// </summary>
    public static IReadOnlyList<Recommendation> Rank(
        RecommendationTaste taste,
        IEnumerable<RecommendationCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(taste);
        ArgumentNullException.ThrowIfNull(candidates);
        return
        [
            .. candidates
                .Select(candidate => Score(taste, candidate))
                .OrderByDescending(recommendation => recommendation.Score)
                .ThenBy(
                    recommendation => recommendation.ContentId.Value.ToString("D", CultureInfo.InvariantCulture),
                    StringComparer.Ordinal)
        ];
    }

    /// <summary>Scores one candidate and names the signals that were not zero, heaviest first.</summary>
    public static Recommendation Score(RecommendationTaste taste, RecommendationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(taste);
        ArgumentNullException.ThrowIfNull(candidate);

        var genre = MeanAffinity(taste.Genres, candidate.Genres);
        var cast = MeanAffinity(taste.Cast, candidate.Cast);
        var rating = candidate.Rating is { } value ? Affinity(value) : 0;
        var year = YearProximity(taste.PreferredYear, candidate.Year);
        var freshness = candidate.IsWatched ? 0 : 1;

        var score = (GenreWeight * genre)
            + (CastWeight * cast)
            + (RatingWeight * rating)
            + (YearWeight * year)
            + (FreshnessWeight * freshness);

        var reasons = new List<RecommendationReason>(5);
        AddReason(genre, RecommendationReason.GenreMatch);
        AddReason(cast, RecommendationReason.CastMatch);
        AddReason(rating, RecommendationReason.RatingAffinity);
        AddReason(year, RecommendationReason.YearProximity);
        AddReason(freshness, RecommendationReason.Freshness);
        return new Recommendation(candidate.Id, score, reasons);

        void AddReason(double signal, RecommendationReason reason)
        {
            if (signal != 0)
            {
                reasons.Add(reason);
            }
        }
    }

    /// <summary>A rating of ten reads as plus one, one as minus one, and the midpoint as zero.</summary>
    private static double Affinity(int? rating) =>
        rating is { } value ? (value - RatingMidpoint) / RatingHalfRange : 0;

    private static double MeanAffinity(
        IReadOnlyDictionary<string, double> affinities,
        IReadOnlyList<string> values)
    {
        if (affinities.Count == 0 || values.Count == 0)
        {
            return 0;
        }

        var known = values
            .Where(value => affinities.ContainsKey(value))
            .Select(value => affinities[value])
            .ToArray();
        return known.Length == 0 ? 0 : known.Average();
    }

    private static double YearProximity(int? preferredYear, int? candidateYear)
    {
        if (preferredYear is not { } preferred || candidateYear is not { } year)
        {
            return 0;
        }

        var distance = Math.Abs(year - preferred);
        return distance >= YearHorizon ? 0 : 1 - (distance / YearHorizon);
    }

    private static Dictionary<string, double> Average(IEnumerable<(string Key, double Value)> entries)
    {
        var totals = new Dictionary<string, (double Sum, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var current = totals.GetValueOrDefault(key, (Sum: 0d, Count: 0));
            totals[key] = (current.Sum + value, current.Count + 1);
        }

        return totals.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Sum / entry.Value.Count,
            StringComparer.OrdinalIgnoreCase);
    }
}
