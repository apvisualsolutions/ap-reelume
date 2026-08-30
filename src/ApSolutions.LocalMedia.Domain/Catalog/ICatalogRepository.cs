// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Catalog;

/// <remarks>
/// These are written to SQLite as their ordinal, so a value may only be <b>appended</b>. Putting
/// <see cref="Course"/> where it reads best — beside <see cref="Movie"/> and <see cref="Show"/> —
/// would renumber <see cref="Unidentified"/> from 2 to 3 and every unidentified title already in
/// somebody's database would come back a course, silently and on the first read.
/// </remarks>
public enum CatalogTitleKind
{
    Movie,
    Show,
    Unidentified,

    /// <summary>
    /// A folder of numbered videos studied in order (ADR-0006, CRS-001). It is a kind of its own and
    /// not a show with different words, because the kind is what decides whether a title is ever
    /// identified against a remote provider, and a rule that hangs off a name heuristic instead of a
    /// kind is a rule that will be wrong in both directions.
    /// </summary>
    Course,
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
