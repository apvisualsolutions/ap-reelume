// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// The three projections Home needs, answered by SQLite. Each one is bounded and indexed, so drawing
/// Home costs the same on a library of ten titles and one of ten thousand.
/// </summary>
/// <remarks>
/// All three read the <b>same catalogue the library lists</b>, which is not the <c>titles</c> table
/// on its own. Nothing in the application writes that table — <c>ApplyIdentification</c> says so in
/// its own words, and its only writer has no caller outside integration tests — so a real library is
/// a set of scanned files whose title id is the media file's id. Reading <c>titles</c> alone is why
/// Home came up empty on a machine with 102 scanned files and four things half watched: measured on
/// 2026-08-25 against the owner's own database, which held 102 rows in <c>scanned_titles</c>, zero in
/// <c>titles</c>, and four in <c>watch_state</c> that all joined to a scanned file and to nothing else.
/// </remarks>
public sealed class HomeReadModel : IHomeReadModel
{
    /// <summary>
    /// The union the library already uses: identified titles, plus every scanned file that no
    /// identified title has claimed. Kept as one string so the two readers cannot drift apart.
    /// </summary>
    private const string CatalogItems = """
        WITH catalog_items AS (
            SELECT t.id AS id, t.kind AS kind, t.primary_title AS primary_title,
                   t.release_year AS release_year, t.is_available AS is_available,
                   t.added_utc AS added_utc,
                   (SELECT group_concat(g.genre, '|') FROM title_genres g WHERE g.title_id = t.id)
                       AS genres
            FROM titles t
            UNION ALL
            SELECT scanned.media_file_id, 2, scanned.display_title, NULL,
                   media.is_available, scanned.added_utc, NULL
            FROM scanned_titles scanned
            INNER JOIN media_files media ON media.id = scanned.media_file_id
            WHERE NOT EXISTS (
                SELECT 1 FROM titles identified WHERE identified.id = scanned.media_file_id)
        )
        """;

    private readonly SqliteConnectionFactory _connectionFactory;

    public HomeReadModel(SqliteConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<IReadOnlyList<HomeProgressEntry>> ReadProgressAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = CatalogItems + """

            SELECT state.content_key, state.title_id, state.episode_id, item.kind, item.primary_title,
                   episodes.season_number, episodes.episode_number, episodes.title,
                   state.position_ticks, state.observed_duration_ticks, state.status,
                   item.release_year, item.genres,
                   CASE
                       WHEN state.episode_id IS NULL THEN item.is_available
                       ELSE CASE WHEN COALESCE(episodes.is_available, 0) = 1
                                  AND COALESCE(media.is_available, 0) = 1
                                 THEN 1 ELSE 0 END
                   END AS effective_availability,
                   state.updated_utc
            FROM watch_state state
            INNER JOIN catalog_items item ON item.id = state.title_id
            LEFT JOIN episodes ON episodes.id = state.episode_id
            LEFT JOIN episode_media link ON link.episode_id = episodes.id
            LEFT JOIN media_files media ON media.id = link.media_file_id
            WHERE state.status = $inProgress
            ORDER BY state.updated_utc DESC, state.content_key
            LIMIT $limit;
            """;
        _ = command.Parameters.AddWithValue("$inProgress", (int)WatchStatus.InProgress);
        _ = command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<HomeProgressEntry>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new HomeProgressEntry(
                ContentKey.Parse(reader.GetString(0)),
                new TitleId(Guid.Parse(reader.GetString(1))),
                (CatalogTitleKind)reader.GetInt32(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                TimeSpan.FromTicks(reader.GetInt64(8)),
                reader.IsDBNull(9) ? null : TimeSpan.FromTicks(reader.GetInt64(9)),
                (WatchStatus)reader.GetInt32(10),
                // Thirteen and fourteen, not eleven and twelve: the year and the genres were added
                // to this SELECT for the hero's line, and every column after them moved along.
                reader.GetInt32(13) == 1,
                ParseDate(reader.GetString(14)),
                reader.IsDBNull(11) ? null : reader.GetInt32(11),
                reader.IsDBNull(12)
                    ? []
                    : reader.GetString(12).Split('|', StringSplitOptions.RemoveEmptyEntries)));
        }

        return entries;
    }

    public async Task<IReadOnlyList<RecentlyAddedItem>> ReadRecentlyAddedAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = CatalogItems + """

            SELECT id, kind, primary_title, release_year, is_available, added_utc
            FROM catalog_items
            ORDER BY added_utc DESC, id
            LIMIT $limit;
            """;
        _ = command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<RecentlyAddedItem>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new RecentlyAddedItem(
                new TitleId(Guid.Parse(reader.GetString(0))),
                (CatalogTitleKind)reader.GetInt32(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.GetInt32(4) == 1,
                ParseDate(reader.GetString(5))));
        }

        return items;
    }

    public async Task<LibrarySummary> ReadLibrarySummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        // Films and series are counted by the kind the catalogue actually knows; a scanned file
        // nobody has identified is neither, and inventing one for it would be a number the
        // catalogue made up. What it does join is availability, which is true of every item.
        command.CommandText = CatalogItems + """

            SELECT
                COALESCE(SUM(CASE WHEN kind = 0 THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN kind = 1 THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN is_available = 0 THEN 1 ELSE 0 END), 0)
            FROM catalog_items;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new LibrarySummary(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2))
            : new LibrarySummary(0, 0, 0);
    }

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
