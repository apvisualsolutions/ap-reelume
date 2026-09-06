// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

public sealed class LibraryRootRepository : ILibraryRootRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public LibraryRootRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, normalized_path, kind, availability, scan_policy
            FROM library_roots
            ORDER BY normalized_path COLLATE NOCASE, id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var roots = new List<LibraryRoot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            roots.Add(ReadRoot(reader));
        }

        return roots;
    }

    public async Task<LibraryRoot?> GetAsync(
        LibraryRootId id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, normalized_path, kind, availability, scan_policy
            FROM library_roots
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadRoot(reader)
            : null;
    }

    public async Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO library_roots (id, normalized_path, kind, availability, scan_policy)
            VALUES ($id, $path, $kind, $availability, $scanPolicy);
            """;
        command.Parameters.AddWithValue("$id", root.Id.Value.ToString("D"));
        command.Parameters.AddWithValue("$path", root.Path);
        command.Parameters.AddWithValue("$kind", (int)root.Kind);
        command.Parameters.AddWithValue("$availability", (int)root.Availability);
        command.Parameters.AddWithValue("$scanPolicy", (int)root.ScanPolicy);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The column has been in the schema since 0002, with a CHECK that already admits all three
    /// values, and until now only the INSERT ever wrote it — always as Available. No migration is
    /// needed to start telling the truth.
    /// </summary>
    public async Task SetAvailabilityAsync(
        LibraryRootId id,
        RootAvailability availability,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE library_roots SET availability = $availability WHERE id = $id;";
        command.Parameters.AddWithValue("$availability", (int)availability);
        command.Parameters.AddWithValue("$id", id.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removing a folder takes its catalogue with it. Three things decide the shape of this method,
    /// and none of them is obvious from the schema:
    ///
    /// <para>Nothing is left to the cascade. <c>recursive_triggers</c> is never turned on, so the
    /// AFTER DELETE triggers that keep <c>catalog_fts</c> and <c>scanned_catalog_fts</c> do not fire
    /// for rows a cascade removed. A removal that leaned on foreign keys would empty the tables and
    /// still answer a search with a title that is gone — and a row count would never notice.</para>
    ///
    /// <para>A title leaves only when it runs out of files. There are two ways to reach one — a film
    /// is its own media file, and a show is reached through its episodes — so the temporary tables
    /// below name exactly the rows that lose their last file, and a show with an episode in another
    /// folder survives with what it has left.</para>
    ///
    /// <para>The same temporary tables answer the notice's counts, which is what stops the warning
    /// from promising a different number than the removal then deletes.</para>
    /// </summary>
    public async Task<IReadOnlyList<TitleId>> RemoveAsync(
        LibraryRootId id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        var rootId = id.Value.ToString("D");

        await ExecuteAsync(connection, transaction, RemovalScopeSql, rootId, cancellationToken)
            .ConfigureAwait(false);
        var leaving = await ReadLeavingTitlesAsync(connection, transaction, cancellationToken)
            .ConfigureAwait(false);

        foreach (var statement in RemovalStatements)
        {
            await ExecuteAsync(connection, transaction, statement, rootId, cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return leaving;
    }

    /// <summary>
    /// The three sets the removal reasons about, built once so the counts and the deletes cannot
    /// disagree. <c>removing_titles</c> folds both ways of reaching a title into one set: a film's id
    /// is its media file's id, and a show's id comes from the episodes that lose their last file.
    /// </summary>
    internal const string RemovalScopeSql = """
        DROP TABLE IF EXISTS temp.removing_files;
        CREATE TEMP TABLE removing_files AS
            SELECT id FROM media_files WHERE library_root_id = $id;

        DROP TABLE IF EXISTS temp.removing_episodes;
        CREATE TEMP TABLE removing_episodes AS
            SELECT episode_id FROM episode_media
            WHERE media_file_id IN (SELECT id FROM removing_files);

        DROP TABLE IF EXISTS temp.removing_titles;
        CREATE TEMP TABLE removing_titles AS
            SELECT DISTINCT e.show_id AS id
            FROM episodes e
            WHERE e.id IN (SELECT episode_id FROM removing_episodes)
              AND NOT EXISTS (
                  SELECT 1
                  FROM episodes kept
                  JOIN episode_media link ON link.episode_id = kept.id
                  WHERE kept.show_id = e.show_id
                    AND link.media_file_id NOT IN (SELECT id FROM removing_files))
            UNION
            SELECT id FROM removing_files;
        """;

    private static readonly string[] RemovalStatements =
    [
        // What a detector proposed and what a person kept. Both go, because their file goes.
        """
        DELETE FROM detected_markers
        WHERE file_id IN (SELECT id FROM removing_files)
           OR series_id IN (SELECT id FROM removing_titles);
        """,
        "DELETE FROM intro_markers WHERE series_id IN (SELECT id FROM removing_titles);",
        "DELETE FROM watch_state WHERE title_id IN (SELECT id FROM removing_titles);",
        "DELETE FROM personal_state WHERE title_id IN (SELECT id FROM removing_titles);",

        // Scope 0 is the global preference and belongs to nobody's folder; 1 is a series and 2 a file.
        """
        DELETE FROM playback_preferences
        WHERE (scope = 2 AND scope_key IN (SELECT id FROM removing_files))
           OR (scope = 1 AND scope_key IN (SELECT id FROM removing_titles));
        """,

        // A version group that keeps a member has to keep pointing at one that still exists, and a
        // group of fewer than two is a row the duplicates screen filters out and nobody would clear.
        "DELETE FROM media_version_group_members WHERE media_file_id IN (SELECT id FROM removing_files);",
        """
        UPDATE media_version_groups
        SET preferred_media_file_id = (
            SELECT media_file_id FROM media_version_group_members
            WHERE group_id = media_version_groups.id
            ORDER BY ordinal LIMIT 1)
        WHERE preferred_media_file_id IS NOT NULL
          AND preferred_media_file_id NOT IN (SELECT media_file_id FROM media_version_group_members);
        """,
        """
        DELETE FROM media_version_groups
        WHERE (SELECT COUNT(*) FROM media_version_group_members
               WHERE group_id = media_version_groups.id) < 2;
        """,

        "DELETE FROM catalog_metadata WHERE title_id IN (SELECT id FROM removing_titles);",
        "DELETE FROM episode_media WHERE media_file_id IN (SELECT id FROM removing_files);",
        "DELETE FROM episodes WHERE id IN (SELECT episode_id FROM removing_episodes);",
        """
        DELETE FROM seasons
        WHERE NOT EXISTS (
            SELECT 1 FROM episodes
            WHERE episodes.show_id = seasons.show_id
              AND episodes.season_number = seasons.season_number);
        """,

        // Explicit, not cascaded, so their AFTER DELETE triggers clear the two search indexes.
        "DELETE FROM titles WHERE id IN (SELECT id FROM removing_titles);",
        "DELETE FROM scanned_titles WHERE media_file_id IN (SELECT id FROM removing_files);",

        "DELETE FROM media_file_identities WHERE media_file_id IN (SELECT id FROM removing_files);",
        "DELETE FROM match_candidates WHERE media_file_id IN (SELECT id FROM removing_files);",
        "DELETE FROM scan_checkpoints WHERE library_root_id = $id;",
        "DELETE FROM media_files WHERE library_root_id = $id;",

        // Last, and the only cascade this method relies on: courses and lessons are addressed by a
        // path relative to the root, so without the root row there is nothing to resolve them against.
        "DELETE FROM library_roots WHERE id = $id;",

        DropRemovalScopeSql,
    ];

    /// <summary>
    /// Temporary tables live on the connection, and connections come from a pool. They are dropped
    /// on the way out so the next caller finds none, and created with a DROP first so it does not
    /// matter if one ever survives.
    /// </summary>
    internal const string DropRemovalScopeSql = """
        DROP TABLE IF EXISTS temp.removing_titles;
        DROP TABLE IF EXISTS temp.removing_episodes;
        DROP TABLE IF EXISTS temp.removing_files;
        """;

    private static async Task<IReadOnlyList<TitleId>> ReadLeavingTitlesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id FROM removing_titles;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var titles = new List<TitleId>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            titles.Add(new TitleId(Guid.Parse(reader.GetString(0))));
        }

        return titles;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        string rootId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", rootId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static LibraryRoot ReadRoot(SqliteDataReader reader) => new(
        new LibraryRootId(Guid.Parse(reader.GetString(0))),
        reader.GetString(1),
        (RootKind)reader.GetInt32(2),
        (RootAvailability)reader.GetInt32(3),
        (ScanPolicy)reader.GetInt32(4));
}
