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
public sealed class HomeReadModel : IHomeReadModel
{
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
        command.CommandText = """
            SELECT state.content_key, state.title_id, state.episode_id, titles.kind, titles.primary_title,
                   episodes.season_number, episodes.episode_number, episodes.title,
                   state.position_ticks, state.observed_duration_ticks, state.status,
                   CASE
                       WHEN state.episode_id IS NULL THEN titles.is_available
                       ELSE CASE WHEN COALESCE(episodes.is_available, 0) = 1
                                  AND COALESCE(media.is_available, 0) = 1
                                 THEN 1 ELSE 0 END
                   END AS effective_availability,
                   state.updated_utc
            FROM watch_state state
            INNER JOIN titles ON titles.id = state.title_id
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
                reader.GetInt32(11) == 1,
                ParseDate(reader.GetString(12))));
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
        command.CommandText = """
            SELECT id, kind, primary_title, release_year, is_available, added_utc
            FROM titles
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
        command.CommandText = """
            SELECT
                COALESCE(SUM(CASE WHEN kind = 0 THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN kind = 1 THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN is_available = 0 THEN 1 ELSE 0 END), 0)
            FROM titles;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new LibrarySummary(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2))
            : new LibrarySummary(0, 0, 0);
    }

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
