// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

public sealed partial class CatalogRepository : ICatalogRepository, ICatalogQueryService
{
    private const int MaximumPageSize = 200;
    private readonly SqliteConnectionFactory _connectionFactory;

    public CatalogRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task UpsertTitleAsync(
        CatalogTitle title,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(title.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(title.SortTitle);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO titles (
                    id, kind, primary_title, sort_title, release_year, added_utc,
                    last_played_utc, has_progress, is_personal, is_available)
                VALUES (
                    $id, $kind, $title, $sortTitle, $year, $addedUtc,
                    $lastPlayedUtc, $hasProgress, $isPersonal, $isAvailable)
                ON CONFLICT(id) DO UPDATE SET
                    kind = excluded.kind,
                    primary_title = excluded.primary_title,
                    sort_title = excluded.sort_title,
                    release_year = excluded.release_year,
                    added_utc = excluded.added_utc,
                    last_played_utc = excluded.last_played_utc,
                    has_progress = excluded.has_progress,
                    is_personal = excluded.is_personal,
                    is_available = excluded.is_available;
                """;
            command.Parameters.AddWithValue("$id", title.Id.Value.ToString("D"));
            command.Parameters.AddWithValue("$kind", (int)title.Kind);
            command.Parameters.AddWithValue("$title", title.Title);
            command.Parameters.AddWithValue("$sortTitle", title.SortTitle);
            command.Parameters.AddWithValue("$year", (object?)title.Year ?? DBNull.Value);
            command.Parameters.AddWithValue("$addedUtc", FormatDate(title.AddedUtc));
            command.Parameters.AddWithValue(
                "$lastPlayedUtc",
                title.LastPlayedUtc is { } lastPlayed ? FormatDate(lastPlayed) : DBNull.Value);
            command.Parameters.AddWithValue("$hasProgress", title.HasProgress ? 1 : 0);
            command.Parameters.AddWithValue("$isPersonal", title.IsPersonal ? 1 : 0);
            command.Parameters.AddWithValue("$isAvailable", title.IsAvailable ? 1 : 0);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await ReplaceValuesAsync(
            connection,
            transaction,
            "alternate_titles",
            "value",
            title.Id,
            title.AlternateTitles,
            cancellationToken).ConfigureAwait(false);
        await ReplaceValuesAsync(
            connection,
            transaction,
            "title_cast",
            "person_name",
            title.Id,
            title.Cast,
            cancellationToken).ConfigureAwait(false);
        await ReplaceValuesAsync(
            connection,
            transaction,
            "title_genres",
            "genre",
            title.Id,
            title.Genres,
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertSeasonAsync(
        CatalogSeason season,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(season);
        ArgumentException.ThrowIfNullOrWhiteSpace(season.Title);
        ArgumentOutOfRangeException.ThrowIfNegative(season.SeasonNumber);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO seasons (show_id, season_number, title)
            VALUES ($showId, $seasonNumber, $title)
            ON CONFLICT(show_id, season_number) DO UPDATE SET title = excluded.title;
            """;
        command.Parameters.AddWithValue("$showId", season.ShowId.Value.ToString("D"));
        command.Parameters.AddWithValue("$seasonNumber", season.SeasonNumber);
        command.Parameters.AddWithValue("$title", season.Title);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertEpisodeAsync(
        CatalogEpisode episode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentException.ThrowIfNullOrWhiteSpace(episode.Title);
        ArgumentOutOfRangeException.ThrowIfNegative(episode.SeasonNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(episode.EpisodeNumber);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO episodes (
                id, show_id, season_number, episode_number, absolute_number,
                title, sort_order, is_available)
            VALUES (
                $id, $showId, $seasonNumber, $episodeNumber, $absoluteNumber,
                $title, $sortOrder, $isAvailable)
            ON CONFLICT(id) DO UPDATE SET
                show_id = excluded.show_id,
                season_number = excluded.season_number,
                episode_number = excluded.episode_number,
                absolute_number = excluded.absolute_number,
                title = excluded.title,
                sort_order = excluded.sort_order,
                is_available = excluded.is_available;
            """;
        command.Parameters.AddWithValue("$id", episode.Id.Value.ToString("D"));
        command.Parameters.AddWithValue("$showId", episode.ShowId.Value.ToString("D"));
        command.Parameters.AddWithValue("$seasonNumber", episode.SeasonNumber);
        command.Parameters.AddWithValue("$episodeNumber", episode.EpisodeNumber);
        command.Parameters.AddWithValue("$absoluteNumber", (object?)episode.AbsoluteNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", episode.Title);
        command.Parameters.AddWithValue("$sortOrder", episode.SortOrder);
        command.Parameters.AddWithValue("$isAvailable", episode.IsAvailable ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CatalogPage> QueryAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.PageSize, MaximumPageSize);
        cancellationToken.ThrowIfCancellationRequested();

        var cursor = DecodeCursor(query.Cursor);
        if (cursor is not null && (cursor.Sort != query.Sort || cursor.Descending != query.Descending))
        {
            throw new InvalidDataException("The catalog cursor does not match the requested sort.");
        }

        var sortExpression = query.Sort switch
        {
            CatalogSort.Title => "t.sort_title COLLATE NOCASE",
            CatalogSort.Year => "printf('%011d', COALESCE(t.release_year, -1) + 1000000000)",
            CatalogSort.Added => "t.added_utc",
            CatalogSort.LastPlayed => "COALESCE(t.last_played_utc, '')",
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };
        var direction = query.Descending ? "DESC" : "ASC";
        var comparison = query.Descending ? "<" : ">";
        var predicates = new List<string>();
        var movie = query.Filters.HasFlag(CatalogFilter.Movie);
        var show = query.Filters.HasFlag(CatalogFilter.Show);
        if (movie != show)
        {
            predicates.Add(movie ? "t.kind = 0" : "t.kind = 1");
        }

        AddBooleanFilter(CatalogFilter.Available, "t.is_available = 1");
        AddBooleanFilter(CatalogFilter.Progress, "t.has_progress = 1");
        AddBooleanFilter(CatalogFilter.Personal, "t.is_personal = 1");

        // A mark on an episode belongs to that episode. Filtering titles therefore looks only at rows
        // whose key is the title itself, so a favourite episode never drags its whole series in.
        AddBooleanFilter(CatalogFilter.Favorite, PersonalPredicate("is_favorite = 1"));
        AddBooleanFilter(CatalogFilter.WatchLater, PersonalPredicate("is_watch_later = 1"));
        AddBooleanFilter(CatalogFilter.Rated, PersonalPredicate("rating IS NOT NULL"));
        var matchExpression = CreateMatchExpression(query.Search);
        if (matchExpression is not null)
        {
            predicates.Add("""
                (t.id IN (SELECT title_id FROM catalog_fts WHERE catalog_fts MATCH $search)
                 OR t.id IN (
                     SELECT media_file_id
                     FROM scanned_catalog_fts
                     WHERE scanned_catalog_fts MATCH $search))
                """);
        }

        if (cursor is not null)
        {
            predicates.Add(
                $"({sortExpression} {comparison} $cursorKey OR " +
                $"({sortExpression} = $cursorKey AND t.id {comparison} $cursorId))");
        }

        var where = predicates.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", predicates)}";
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            WITH catalog_items AS (
                SELECT t.id, t.kind, t.primary_title, t.sort_title, t.release_year,
                       t.is_available, t.has_progress, t.is_personal,
                       t.added_utc, t.last_played_utc
                FROM titles t
                UNION ALL
                SELECT scanned.media_file_id, 2, scanned.display_title, scanned.sort_title, NULL,
                       media.is_available, 0, 0, scanned.added_utc, NULL
                FROM scanned_titles scanned
                INNER JOIN media_files media ON media.id = scanned.media_file_id
                WHERE NOT EXISTS (
                    SELECT 1 FROM titles identified WHERE identified.id = scanned.media_file_id)
            )
            SELECT t.id, t.kind, t.primary_title, t.release_year, t.is_available,
                   t.has_progress, t.is_personal, t.added_utc, t.last_played_utc,
                   {sortExpression} AS sort_key
            FROM catalog_items t
            {where}
            ORDER BY {sortExpression} {direction}, t.id {direction}
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", query.PageSize + 1);
        if (matchExpression is not null)
        {
            command.Parameters.AddWithValue("$search", matchExpression);
        }

        if (cursor is not null)
        {
            command.Parameters.AddWithValue("$cursorKey", cursor.SortKey);
            command.Parameters.AddWithValue("$cursorId", cursor.Id);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<CatalogRow>(query.PageSize + 1);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CatalogRow(
                new CatalogItem(
                    new TitleId(Guid.Parse(reader.GetString(0))),
                    (CatalogTitleKind)reader.GetInt32(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    reader.GetInt32(4) == 1,
                    reader.GetInt32(5) == 1,
                    reader.GetInt32(6) == 1,
                    ParseDate(reader.GetString(7)),
                    reader.IsDBNull(8) ? null : ParseDate(reader.GetString(8))),
                Convert.ToString(reader.GetValue(9), CultureInfo.InvariantCulture) ?? string.Empty));
        }

        var hasMore = rows.Count > query.PageSize;
        var visible = rows.Take(query.PageSize).ToArray();
        var nextCursor = hasMore
            ? EncodeCursor(new CursorState(
                visible[^1].SortKey,
                visible[^1].Item.Id.Value.ToString("D"),
                query.Descending,
                query.Sort))
            : null;
        return new CatalogPage(visible.Select(row => row.Item).ToArray(), nextCursor);

        void AddBooleanFilter(CatalogFilter filter, string predicate)
        {
            if (query.Filters.HasFlag(filter))
            {
                predicates.Add(predicate);
            }
        }
    }

    private static string PersonalPredicate(string mark) => $"""
        t.id IN (
            SELECT title_id FROM personal_state
            WHERE episode_id IS NULL AND {mark})
        """;

    private static async Task ReplaceValuesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string valueColumn,
        TitleId titleId,
        IReadOnlyList<string> values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = $"DELETE FROM {table} WHERE title_id = $titleId;";
            delete.Parameters.AddWithValue("$titleId", titleId.Value.ToString("D"));
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var value in values
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = $"INSERT INTO {table} (title_id, {valueColumn}) VALUES ($titleId, $value);";
            insert.Parameters.AddWithValue("$titleId", titleId.Value.ToString("D"));
            insert.Parameters.AddWithValue("$value", value.Trim());
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string? CreateMatchExpression(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var terms = SearchTerm().Matches(search)
            .Select(match => $"\"{match.Value}\"")
            .ToArray();
        return terms.Length == 0 ? null : string.Join(" AND ", terms);
    }

    private static CursorState? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CursorState>(Convert.FromBase64String(cursor));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new InvalidDataException("The catalog cursor is invalid.", exception);
        }
    }

    private static string EncodeCursor(CursorState cursor) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(cursor));

    private static string FormatDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    [GeneratedRegex(@"[\p{L}\p{N}_]+", RegexOptions.CultureInvariant)]
    private static partial Regex SearchTerm();

    private sealed record CursorState(
        string SortKey,
        string Id,
        bool Descending,
        CatalogSort Sort);

    private sealed record CatalogRow(CatalogItem Item, string SortKey);
}
