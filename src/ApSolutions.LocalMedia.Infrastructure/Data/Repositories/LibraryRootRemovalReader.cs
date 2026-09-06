// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// Counts what a removal would take, over the very rows the removal then deletes.
/// </summary>
/// <remarks>
/// It builds its sets with <see cref="LibraryRootRepository.RemovalScopeSql"/> — the same statement
/// the removal itself runs — rather than with a query of its own. Two queries meaning to say the
/// same thing is how a warning ends up promising a number nobody then deletes, and nothing in a test
/// that compares one SQL against another would catch it.
/// </remarks>
public sealed class LibraryRootRemovalReader : ILibraryRootRemovalReader
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public LibraryRootRemovalReader(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<LibraryRootRemovalSummary> SummarizeAsync(
        LibraryRootId id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, LibraryRootRepository.RemovalScopeSql, id, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var titles = await ScalarAsync(connection, TitleCountSql, id, cancellationToken)
                .ConfigureAwait(false);
            var marks = await ScalarAsync(connection, MarkCountSql, id, cancellationToken)
                .ConfigureAwait(false);
            var progress = await ScalarAsync(connection, ProgressTicksSql, id, cancellationToken)
                .ConfigureAwait(false);
            return new LibraryRootRemovalSummary(
                (int)titles,
                (int)marks,
                TimeSpan.FromTicks(progress));
        }
        finally
        {
            await ExecuteAsync(connection, LibraryRootRepository.DropRemovalScopeSql, id, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A card, not a row. A show counts once however many episodes it loses; an unidentified file
    /// counts only when it is neither an identified title nor an episode of one, because otherwise it
    /// is already being counted by the shelf it belongs to.
    /// </summary>
    private const string TitleCountSql = """
        SELECT
            (SELECT COUNT(*) FROM titles WHERE id IN (SELECT id FROM removing_titles))
          + (SELECT COUNT(*) FROM scanned_titles
             WHERE media_file_id IN (SELECT id FROM removing_files)
               AND media_file_id NOT IN (SELECT id FROM titles)
               AND media_file_id NOT IN (SELECT media_file_id FROM episode_media));
        """;

    private const string MarkCountSql = """
        SELECT
            (SELECT COUNT(*) FROM personal_state WHERE title_id IN (SELECT id FROM removing_titles))
          + (SELECT COUNT(*) FROM intro_markers WHERE series_id IN (SELECT id FROM removing_titles));
        """;

    private const string ProgressTicksSql = """
        SELECT COALESCE(SUM(position_ticks), 0) FROM watch_state
        WHERE title_id IN (SELECT id FROM removing_titles);
        """;

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        LibraryRootId id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", id.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> ScalarAsync(
        SqliteConnection connection,
        string sql,
        LibraryRootId id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", id.Value.ToString("D"));
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull
            ? 0
            : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
