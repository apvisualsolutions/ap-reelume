// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Application.Home;

/// <summary>
/// One row of stored progress joined to what the catalogue knows about it. The read model produces
/// these; the use case decides which one is worth offering.
/// </summary>
public sealed record HomeProgressEntry(
    ContentKey Content,
    TitleId TitleId,
    CatalogTitleKind Kind,
    string Title,
    int? SeasonNumber,
    int? EpisodeNumber,
    string? EpisodeTitle,
    TimeSpan Position,
    TimeSpan? ObservedDuration,
    WatchStatus Status,
    bool IsAvailable,
    DateTimeOffset UpdatedUtc,
    int? Year = null,
    IReadOnlyList<string>? Genres = null);

/// <summary>The single thing Home offers to continue, or nothing at all.</summary>
public sealed record ResumeItem(
    ContentKey Content,
    TitleId TitleId,
    CatalogTitleKind Kind,
    string Title,
    int? SeasonNumber,
    int? EpisodeNumber,
    string? EpisodeTitle,
    TimeSpan Position,
    TimeSpan? ObservedDuration,
    double CompletedFraction,
    int? Year = null,
    IReadOnlyList<string>? Genres = null);

/// <summary>An item on the in-progress rail, including one whose file is currently out of reach.</summary>
public sealed record InProgressItem(
    ContentKey Content,
    TitleId TitleId,
    CatalogTitleKind Kind,
    string Title,
    int? SeasonNumber,
    int? EpisodeNumber,
    string? EpisodeTitle,
    double CompletedFraction,
    bool IsAvailable,
    DateTimeOffset UpdatedUtc,
    TimeSpan Position = default,
    int? Year = null);

public sealed record RecentlyAddedItem(
    TitleId Id,
    CatalogTitleKind Kind,
    string Title,
    int? Year,
    bool IsAvailable,
    DateTimeOffset AddedUtc);

/// <summary>What the library holds, so Home can say so without listing it.</summary>
public sealed record LibrarySummary(int MovieCount, int ShowCount, int UnavailableCount);

/// <summary>Everything the hybrid Home shows in one read.</summary>
public sealed record HomeSnapshot(
    ResumeItem? Resume,
    IReadOnlyList<InProgressItem> InProgress,
    IReadOnlyList<RecentlyAddedItem> RecentlyAdded,
    LibrarySummary Library)
{
    /// <summary>True when Continue is a real action and therefore the primary one.</summary>
    public bool HasResume => Resume is not null;
}

/// <summary>How much of each list Home wants; the read model never returns the whole catalogue.</summary>
public sealed record GetHomeQuery(int InProgressLimit = 12, int RecentlyAddedLimit = 12)
{
    public int InProgressLimit { get; } = Positive(InProgressLimit, nameof(InProgressLimit));

    public int RecentlyAddedLimit { get; } = Positive(RecentlyAddedLimit, nameof(RecentlyAddedLimit));

    private static int Positive(int value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, parameterName);
        return value;
    }
}

/// <summary>
/// Reads exactly what Home needs. Progress arrives already ordered and already limited, because a
/// ten-thousand-file catalogue must never be loaded to draw one screen.
/// </summary>
public interface IHomeReadModel
{
    /// <summary>Stored progress, most recently touched first.</summary>
    Task<IReadOnlyList<HomeProgressEntry>> ReadProgressAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecentlyAddedItem>> ReadRecentlyAddedAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<LibrarySummary> ReadLibrarySummaryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the hybrid Home. Continue is offered only when there is progress worth resuming from
/// something that can actually be played right now; everything else still reaches the rail, marked
/// for what it is.
/// </summary>
public sealed class GetHome
{
    private readonly IHomeReadModel _readModel;

    public GetHome(IHomeReadModel readModel) =>
        _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));

    public async Task<HomeSnapshot> ExecuteAsync(
        GetHomeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var progress = await _readModel
            .ReadProgressAsync(query.InProgressLimit, cancellationToken)
            .ConfigureAwait(false);
        var recentlyAdded = await _readModel
            .ReadRecentlyAddedAsync(query.RecentlyAddedLimit, cancellationToken)
            .ConfigureAwait(false);
        var library = await _readModel
            .ReadLibrarySummaryAsync(cancellationToken)
            .ConfigureAwait(false);

        var underway = progress
            .Where(entry => entry.Status == WatchStatus.InProgress)
            .OrderByDescending(entry => entry.UpdatedUtc)
            .ThenBy(entry => entry.Content.Value, StringComparer.Ordinal)
            .ToArray();
        return new HomeSnapshot(
            SelectResume(underway),
            [.. underway.Select(ToRailItem)],
            recentlyAdded,
            library);
    }

    /// <summary>
    /// The most recent entry that is both reachable and past the resume floor. Something that cannot
    /// be opened is never the primary action, because pressing Continue would only fail.
    /// </summary>
    private static ResumeItem? SelectResume(IReadOnlyList<HomeProgressEntry> underway)
    {
        foreach (var entry in underway)
        {
            if (!entry.IsAvailable)
            {
                continue;
            }

            var position = ProgressPolicy.ClampPosition(entry.Position, entry.ObservedDuration);
            if (!ProgressPolicy.ShouldOfferResume(position, entry.ObservedDuration))
            {
                continue;
            }

            return new ResumeItem(
                entry.Content,
                entry.TitleId,
                entry.Kind,
                entry.Title,
                entry.SeasonNumber,
                entry.EpisodeNumber,
                entry.EpisodeTitle,
                position,
                entry.ObservedDuration,
                CompletedFraction(position, entry.ObservedDuration),
                entry.Year,
                entry.Genres);
        }

        return null;
    }

    private static InProgressItem ToRailItem(HomeProgressEntry entry) => new(
        entry.Content,
        entry.TitleId,
        entry.Kind,
        entry.Title,
        entry.SeasonNumber,
        entry.EpisodeNumber,
        entry.EpisodeTitle,
        CompletedFraction(
            ProgressPolicy.ClampPosition(entry.Position, entry.ObservedDuration),
            entry.ObservedDuration),
        entry.IsAvailable,
        entry.UpdatedUtc,
        ProgressPolicy.ClampPosition(entry.Position, entry.ObservedDuration),
        entry.Year);

    /// <summary>Zero when the length was never observed; an unobserved media has no percentage.</summary>
    private static double CompletedFraction(TimeSpan position, TimeSpan? duration) =>
        duration is { } observed && observed > TimeSpan.Zero
            ? Math.Clamp(position.TotalSeconds / observed.TotalSeconds, 0, 1)
            : 0;
}
