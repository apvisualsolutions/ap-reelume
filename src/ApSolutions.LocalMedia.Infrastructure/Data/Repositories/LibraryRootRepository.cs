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

    public async Task RemoveAsync(
        LibraryRootId id,
        bool preserveCatalog = true,
        CancellationToken cancellationToken = default)
    {
        _ = preserveCatalog;
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM library_roots WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static LibraryRoot ReadRoot(SqliteDataReader reader) => new(
        new LibraryRootId(Guid.Parse(reader.GetString(0))),
        reader.GetString(1),
        (RootKind)reader.GetInt32(2),
        (RootAvailability)reader.GetInt32(3),
        (ScanPolicy)reader.GetInt32(4));
}
