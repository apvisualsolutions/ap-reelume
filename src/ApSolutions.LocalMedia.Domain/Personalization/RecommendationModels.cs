// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Personalization;

/// <summary>
/// Why something was suggested. These are codes rather than sentences, so the words shown to a person
/// come from the resource dictionaries and can be translated.
/// </summary>
public enum RecommendationReason
{
    GenreMatch,
    CastMatch,
    RatingAffinity,
    YearProximity,
    Freshness,
}

/// <summary>
/// One title the recommender may suggest, with only the facts the formula uses. There is no path, no
/// file name, and nothing that identifies the machine.
/// </summary>
public sealed record RecommendationCandidate(
    TitleId Id,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Cast,
    int? Year,
    bool IsAvailable,
    bool IsWatched,
    int? Rating);

/// <summary>
/// One title the person has already watched, which is the whole of the history the formula reads.
/// </summary>
public sealed record WatchedTitle(
    TitleId Id,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Cast,
    int? Year,
    int? Rating);

/// <summary>
/// What the local history says about someone's taste, summarised into affinities between minus one
/// and one. This is derived on the machine, from the machine, and never leaves it: it is not an
/// account, not a remote profile, and not shared with anything.
/// </summary>
public sealed record RecommendationTaste(
    IReadOnlyDictionary<string, double> Genres,
    IReadOnlyDictionary<string, double> Cast,
    double? AverageRating,
    int? PreferredYear)
{
    public static RecommendationTaste Empty { get; } = new(
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
        null,
        null);

    public bool IsEmpty => Genres.Count == 0
        && Cast.Count == 0
        && AverageRating is null
        && PreferredYear is null;

    public bool Equals(RecommendationTaste? other) =>
        other is not null
        && AverageRating == other.AverageRating
        && PreferredYear == other.PreferredYear
        && SameEntries(Genres, other.Genres)
        && SameEntries(Cast, other.Cast);

    public override int GetHashCode() =>
        HashCode.Combine(Genres.Count, Cast.Count, AverageRating, PreferredYear);

    private static bool SameEntries(
        IReadOnlyDictionary<string, double> first,
        IReadOnlyDictionary<string, double> second) =>
        first.Count == second.Count
        && first.All(entry => second.TryGetValue(entry.Key, out var value) && value.Equals(entry.Value));
}

/// <summary>One suggestion: what, how strongly, and which signals produced it.</summary>
public sealed record Recommendation(
    TitleId ContentId,
    double Score,
    IReadOnlyList<RecommendationReason> ReasonCodes);
