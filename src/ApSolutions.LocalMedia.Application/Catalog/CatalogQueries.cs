// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Application.Catalog;

[Flags]
public enum CatalogFilter
{
    None = 0,
    Movie = 1,
    Show = 2,
    Available = 4,
    Progress = 8,
    Personal = 16,

    /// <summary>Titles the person marked as a favourite.</summary>
    Favorite = 32,

    /// <summary>Titles the person kept for later.</summary>
    WatchLater = 64,

    /// <summary>Titles the person rated, whatever the score.</summary>
    Rated = 128,
}

public enum CatalogSort
{
    Title,
    Year,
    Added,
    LastPlayed,
}

public sealed record CatalogQuery(
    string? Search = null,
    CatalogFilter Filters = CatalogFilter.None,
    CatalogSort Sort = CatalogSort.Title,
    int PageSize = 50,
    string? Cursor = null,
    bool Descending = false);

/// <summary>One row of the library grid, with everything the card on it paints.</summary>
/// <remarks>
/// The last five arrived on 2026-08-24, when the owner compared the grid with the prototype's. Its
/// card carries three lines and two badges — a kind chip, the title, «2024 · 111 min · Suspense», a
/// watch status or an episode count, and a tick when the whole thing has been seen — and this record
/// carried a title, a year and two flags. Every one of the five is in the database already; what was
/// missing was the journey from there to here, which is why they are defaulted rather than required:
/// four view models build a card and only the catalogue's own query can answer all five.
/// </remarks>
public sealed record CatalogItem(
    TitleId Id,
    CatalogTitleKind Kind,
    string Title,
    int? Year,
    bool IsAvailable,
    bool HasProgress,
    bool IsPersonal,
    DateTimeOffset AddedUtc,
    DateTimeOffset? LastPlayedUtc,
    TimeSpan? Runtime = null,
    IReadOnlyList<string>? Genres = null,
    WatchStatus Status = WatchStatus.NotStarted,
    double CompletedFraction = 0,
    int EpisodeCount = 0,
    int EpisodesWatched = 0,
    string? PosterPath = null);

public sealed record CatalogPage(
    IReadOnlyList<CatalogItem> Items,
    string? NextCursor);

public interface ICatalogQueryService
{
    Task<CatalogPage> QueryAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default);
}
