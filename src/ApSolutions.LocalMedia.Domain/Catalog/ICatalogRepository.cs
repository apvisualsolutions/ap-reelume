// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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

    /// <summary>
    /// Says which file is behind an episode.
    /// </summary>
    /// <remarks>
    /// The table has existed since migration 0014 and nothing had ever written a row into it, so
    /// every episode in the catalogue was an episode with no file: the sequence reader returns it
    /// marked as not playable, the card draws it greyed, and the next-episode countdown has nothing
    /// to chain to. It is a port of its own rather than a field on the episode because a file can
    /// arrive after the episode it backs — a season catalogued from a provider, then the copies.
    /// </remarks>
    Task LinkEpisodeMediaAsync(
        EpisodeId episodeId,
        MediaFileId mediaFileId,
        CancellationToken cancellationToken = default);
}
