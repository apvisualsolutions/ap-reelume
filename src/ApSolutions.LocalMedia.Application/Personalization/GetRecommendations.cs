// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Personalization;

namespace ApSolutions.LocalMedia.Application.Personalization;

/// <summary>
/// How the rail is configured. Unavailable content is included by default, because seeing that
/// something is suggested but currently out of reach is more useful than it silently disappearing.
/// </summary>
public sealed record RecommendationOptions(
    bool IsEnabled,
    bool ExcludeUnavailable = false,
    int Limit = int.MaxValue)
{
    public int Limit { get; } = Positive(Limit);

    private static int Positive(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return limit;
    }
}

/// <summary>
/// Reads the two local inputs the formula needs. Both come from the catalogue on this machine; there
/// is no provider, no endpoint, and nothing to configure.
/// </summary>
public interface IRecommendationReadModel
{
    Task<RecommendationTaste> ReadTasteAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecommendationCandidate>> ReadCandidatesAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Produces the suggestions Home shows. Switched off, it returns nothing and reads nothing: the work
/// is skipped rather than done and discarded, which is what makes the switch meaningful.
/// </summary>
public sealed class GetRecommendations
{
    private readonly IRecommendationReadModel _readModel;

    public GetRecommendations(IRecommendationReadModel readModel) =>
        _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));

    public async Task<IReadOnlyList<Recommendation>> ExecuteAsync(
        RecommendationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IsEnabled)
        {
            return [];
        }

        var taste = await _readModel.ReadTasteAsync(cancellationToken).ConfigureAwait(false);
        var candidates = await _readModel.ReadCandidatesAsync(cancellationToken).ConfigureAwait(false);
        var visible = options.ExcludeUnavailable
            ? candidates.Where(candidate => candidate.IsAvailable)
            : candidates;
        var ranked = RecommendationPolicy.Rank(taste, visible);
        return options.Limit >= ranked.Count ? ranked : [.. ranked.Take(options.Limit)];
    }
}
