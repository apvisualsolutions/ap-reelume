// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// Reads the episodes of a series with the file behind each one. An episode the catalogue knows but
/// no file backs is returned all the same, marked as not playable, because hiding it would make the
/// season look shorter than it is.
/// </summary>
/// <remarks>
/// The title and the running time joined the projection on 2026-08-25, for the series card's episode
/// rows: a row that says only «Episodio 3» is a number where the prototype writes a name and a
/// length. Both are nullable on purpose — the title of an episode nobody identified is the catalogue
/// row's own placeholder, and the running time is absent for an episode with no file behind it.
/// </remarks>
public sealed class EpisodeSequenceRepository : IEpisodeSequenceRepository
{
    private const string Projection = """
        SELECT episodes.id, episodes.show_id, episodes.season_number, episodes.episode_number,
               media.id, media.normalized_path,
               CASE WHEN episodes.is_available = 1 AND COALESCE(media.is_available, 0) = 1
                    THEN 1 ELSE 0 END AS effective_availability,
               episodes.title, media.duration_ticks
        FROM episodes
        LEFT JOIN episode_media link ON link.episode_id = episodes.id
        LEFT JOIN media_files media ON media.id = link.media_file_id
        """;

    private readonly SqliteConnectionFactory _connectionFactory;

    public EpisodeSequenceRepository(SqliteConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<IReadOnlyList<EpisodeSequenceEntry>> GetSeriesAsync(
        TitleId showId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {Projection}
            WHERE episodes.show_id = $showId
            ORDER BY episodes.season_number, episodes.episode_number;
            """;
        _ = command.Parameters.AddWithValue("$showId", showId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<EpisodeSequenceEntry>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(Read(reader));
        }

        return entries;
    }

    public async Task<EpisodeSequenceEntry?> GetAsync(
        EpisodeId episodeId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {Projection}
            WHERE episodes.id = $episodeId;
            """;
        _ = command.Parameters.AddWithValue("$episodeId", episodeId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task<EpisodeSequenceEntry?> FindByFileAsync(
        MediaFileId fileId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {Projection}
            WHERE media.id = $fileId;
            """;
        _ = command.Parameters.AddWithValue("$fileId", fileId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    private static EpisodeSequenceEntry Read(SqliteDataReader reader) => new(
        new EpisodeId(Guid.Parse(reader.GetString(0))),
        new TitleId(Guid.Parse(reader.GetString(1))),
        reader.GetInt32(2),
        reader.GetInt32(3),
        reader.IsDBNull(4) ? null : new MediaFileId(Guid.Parse(reader.GetString(4))),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.GetInt32(6) == 1,
        // The title is NOT NULL in the schema, so it is read rather than checked; the running time
        // comes from the file, and an episode the catalogue knows with no file behind it has none.
        reader.GetString(7),
        reader.IsDBNull(8) ? null : TimeSpan.FromTicks(reader.GetInt64(8)));
}
