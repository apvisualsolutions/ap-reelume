using ApSolutions.LocalMedia.Domain.Catalog;

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

public sealed record CatalogItem(
    TitleId Id,
    CatalogTitleKind Kind,
    string Title,
    int? Year,
    bool IsAvailable,
    bool HasProgress,
    bool IsPersonal,
    DateTimeOffset AddedUtc,
    DateTimeOffset? LastPlayedUtc);

public sealed record CatalogPage(
    IReadOnlyList<CatalogItem> Items,
    string? NextCursor);

public interface ICatalogQueryService
{
    Task<CatalogPage> QueryAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default);
}
