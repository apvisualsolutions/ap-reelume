namespace ApSolutions.LocalMedia.Domain.Catalog;

public enum CatalogTitleKind
{
    Movie,
    Show,
    Unidentified,
}

public sealed record CatalogTitle(
    TitleId Id,
    CatalogTitleKind Kind,
    string Title,
    string SortTitle,
    int? Year,
    IReadOnlyList<string> AlternateTitles,
    IReadOnlyList<string> Cast,
    IReadOnlyList<string> Genres,
    DateTimeOffset AddedUtc,
    DateTimeOffset? LastPlayedUtc,
    bool HasProgress,
    bool IsPersonal,
    bool IsAvailable);

public sealed record CatalogSeason(
    TitleId ShowId,
    int SeasonNumber,
    string Title);

public sealed record CatalogEpisode(
    EpisodeId Id,
    TitleId ShowId,
    int SeasonNumber,
    int EpisodeNumber,
    int? AbsoluteNumber,
    string Title,
    int SortOrder,
    bool IsAvailable);

public interface ICatalogRepository
{
    Task UpsertTitleAsync(CatalogTitle title, CancellationToken cancellationToken = default);

    Task UpsertSeasonAsync(CatalogSeason season, CancellationToken cancellationToken = default);

    Task UpsertEpisodeAsync(CatalogEpisode episode, CancellationToken cancellationToken = default);
}
