// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using Microsoft.Data.Sqlite;

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
        // One row per FILE rather than one per group, because the page shows the files: the group's
        // own facts repeat down its rows and are folded back together here. Two queries would read
        // the same join twice and could disagree between them.
        command.CommandText = """
            SELECT g.content_key,
                   COALESCE(t.primary_title, s.display_title, g.content_key) AS display_title,
                   g.id,
                   media.id,
                   media.normalized_path,
                   media.width,
                   media.height,
                   media.video_codecs,
                   media.audio_codecs,
                   media.size_bytes,
                   media.duration_ticks,
                   media.is_available,
                   CASE WHEN g.preferred_media_file_id = media.id THEN 1 ELSE 0 END AS is_preferred,
                   (SELECT COUNT(*) FROM media_version_group_members c WHERE c.group_id = g.id) AS version_count
            FROM media_version_groups g
            INNER JOIN media_version_group_members m ON m.group_id = g.id
            INNER JOIN media_files media ON media.id = m.media_file_id
            LEFT JOIN titles t ON g.content_key = 'title:' || t.id
            LEFT JOIN scanned_titles s ON g.content_key = 'title:' || s.media_file_id
            WHERE (SELECT COUNT(*) FROM media_version_group_members c WHERE c.group_id = g.id) >= 2
            ORDER BY display_title COLLATE NOCASE, g.id, media.normalized_path;
            """;

        var groups = new List<DuplicateOverviewEntry>();
        var files = new Dictionary<string, List<DuplicateFileRow>>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var contentKey = reader.GetString(0);
            if (!contentKey.StartsWith(TitlePrefix, StringComparison.Ordinal)
                || !Guid.TryParseExact(contentKey[TitlePrefix.Length..], "D", out var titleId))
            {
                continue;
            }

            var groupId = reader.GetString(2);
            if (!files.TryGetValue(groupId, out var rows))
            {
                rows = [];
                files[groupId] = rows;
                groups.Add(new DuplicateOverviewEntry(
                    new TitleId(titleId),
                    reader.GetString(1),
                    reader.GetInt32(13),
                    new MediaVersionId(Guid.Parse(groupId)),
                    rows));
            }

            rows.Add(new DuplicateFileRow(
                new MediaFileId(Guid.Parse(reader.GetString(3))),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                FirstCodec(reader, 7),
                FirstCodec(reader, 8),
                // Read rather than checked: size_bytes is NOT NULL in the schema, unlike the four
                // columns around it, so a null test would be a branch nothing can take.
                reader.GetInt64(9),
                reader.IsDBNull(10) ? null : TimeSpan.FromTicks(reader.GetInt64(10)),
                reader.GetInt32(11) == 1,
                reader.GetInt32(12) == 1));
        }

        return groups;
    }

    /// <summary>
    /// The first codec of the stored list, which is the one a person compares by. They are kept as
    /// JSON because a file can carry several, and a row that printed all of them would be a column
    /// of arrays.
    /// </summary>
    private static string FirstCodec(SqliteDataReader reader, int column)
    {
        // Read rather than checked for null: both codec columns are NOT NULL in the schema, so a
        // null test here would be a branch nothing can take.
        var stored = reader.GetString(column);
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string[]>(stored) is { Length: > 0 } codecs
                ? codecs[0]
                : string.Empty;
        }
        catch (System.Text.Json.JsonException)
        {
            // A value written before the column held JSON is the codec itself, and printing it is
            // better than printing nothing.
            return stored;
        }
    }
}
