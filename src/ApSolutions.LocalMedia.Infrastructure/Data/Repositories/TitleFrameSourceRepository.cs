// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Globalization;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Courses;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>The titles that need a frame for a cover, read from the catalogue (LIB-021).</summary>
public sealed class TitleFrameSourceRepository(SqliteConnectionFactory connectionFactory) : ITitleFrameSources
{
    private readonly SqliteConnectionFactory _connectionFactory =
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    /// <remarks>
    /// Three branches, one per kind of card the grid draws. A film's title id is its media file's id,
    /// which is what ApplyIdentification writes; a series looks up its first available episode outside
    /// season 0; a scanned file answers only while no title and no episode claims it, the same two
    /// conditions the catalogue query uses, so a file is never asked for twice.
    /// </remarks>
    public async Task<IReadOnlyList<TitleFrameSource>> ListWithoutCoverAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH candidates (title_id, media_id) AS (
                SELECT t.id, t.id FROM titles t WHERE t.kind = 0
                UNION ALL
                SELECT t.id, (
                    SELECT link.media_file_id
                    FROM episodes e
                    INNER JOIN episode_media link ON link.episode_id = e.id
                    INNER JOIN media_files file ON file.id = link.media_file_id
                    WHERE e.show_id = t.id AND e.season_number > 0 AND file.is_available = 1
                    ORDER BY e.season_number, e.episode_number
                    LIMIT 1)
                FROM titles t WHERE t.kind = 1
                UNION ALL
                SELECT scanned.media_file_id, scanned.media_file_id
                FROM scanned_titles scanned
                WHERE NOT EXISTS (SELECT 1 FROM titles identified WHERE identified.id = scanned.media_file_id)
                  AND NOT EXISTS (SELECT 1 FROM episode_media link WHERE link.media_file_id = scanned.media_file_id)
            )
            SELECT c.title_id, media.normalized_path, media.duration_ticks, media.size_bytes, media.last_write_utc
            FROM candidates c
            INNER JOIN media_files media ON media.id = c.media_id
            WHERE media.is_available = 1
              AND NOT EXISTS (
                  SELECT 1 FROM catalog_metadata m
                  WHERE m.title_id = c.title_id
                    AND (m.poster_path IS NOT NULL OR m.personal_cover IS NOT NULL))
            ORDER BY c.title_id;
            """;

        var sources = new List<TitleFrameSource>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            sources.Add(new TitleFrameSource(
                new TitleId(Guid.Parse(reader.GetString(0), CultureInfo.InvariantCulture)),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : TimeSpan.FromTicks(reader.GetInt64(2)),
                new CourseThumbnailStamp(
                    reader.GetInt64(3),
                    DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))));
        }

        return sources;
    }
}
