// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// The duplicates destination's list: every stored group with two or more members, joined to the
/// catalogue for the name a person knows the title by.
/// </summary>
/// <remarks>
/// The join walks the same two shelves the catalogue query reads — identified titles first, the
/// scanned name as the fallback — because a group is keyed by <c>title:</c> plus the title's id in
/// its <c>"D"</c> form, which is exactly how both tables store theirs. A group whose key does not
/// parse back to a title id is skipped rather than shown: a row that cannot be opened is an offer
/// of something that cannot occur.
/// </remarks>
public sealed class DuplicateOverviewReader : IDuplicateOverviewReader
{
    private const string TitlePrefix = "title:";

    private readonly SqliteConnectionFactory _connectionFactory;

    public DuplicateOverviewReader(SqliteConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<IReadOnlyList<DuplicateOverviewEntry>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT g.content_key,
                   COALESCE(t.primary_title, s.display_title, g.content_key) AS display_title,
                   COUNT(m.media_file_id) AS version_count
            FROM media_version_groups g
            INNER JOIN media_version_group_members m ON m.group_id = g.id
            LEFT JOIN titles t ON g.content_key = 'title:' || t.id
            LEFT JOIN scanned_titles s ON g.content_key = 'title:' || s.media_file_id
            GROUP BY g.id
            HAVING COUNT(m.media_file_id) >= 2
            ORDER BY display_title COLLATE NOCASE, g.id;
            """;

        var entries = new List<DuplicateOverviewEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var contentKey = reader.GetString(0);
            if (!contentKey.StartsWith(TitlePrefix, StringComparison.Ordinal)
                || !Guid.TryParseExact(contentKey[TitlePrefix.Length..], "D", out var titleId))
            {
                continue;
            }

            entries.Add(new DuplicateOverviewEntry(
                new TitleId(titleId),
                reader.GetString(1),
                reader.GetInt32(2)));
        }

        return entries;
    }
}
