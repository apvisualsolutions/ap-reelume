// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// One row per piece of content. The write is a single upsert inside a transaction, so a process that
/// dies mid-session leaves either the previous position or the new one, never a torn row.
/// </summary>
public sealed class WatchStateRepository : IWatchStateRepository
{
    private const string Columns = """
        content_key, title_id, episode_id, position_ticks, observed_duration_ticks,
        source_media_file_id, status, is_manual_override, started_utc, updated_utc
        """;

    private readonly SqliteConnectionFactory _connectionFactory;

    public WatchStateRepository(SqliteConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<WatchState?> GetAsync(ContentKey content, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM watch_state
            WHERE content_key = $key;
            """;
        _ = command.Parameters.AddWithValue("$key", content.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<WatchState>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM watch_state
            ORDER BY content_key;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var states = new List<WatchState>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            states.Add(Read(reader));
        }

        return states;
    }

    public async Task SaveAsync(WatchState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO watch_state (
                    content_key, title_id, episode_id, position_ticks, observed_duration_ticks,
                    source_media_file_id, status, is_manual_override, started_utc, updated_utc)
                VALUES (
                    $key, $titleId, $episodeId, $position, $duration,
                    $source, $status, $override, $startedUtc, $updatedUtc)
                ON CONFLICT(content_key) DO UPDATE SET
                    title_id = excluded.title_id,
                    episode_id = excluded.episode_id,
                    position_ticks = excluded.position_ticks,
                    observed_duration_ticks = excluded.observed_duration_ticks,
                    source_media_file_id = excluded.source_media_file_id,
                    status = excluded.status,
                    is_manual_override = excluded.is_manual_override,
                    started_utc = excluded.started_utc,
                    updated_utc = excluded.updated_utc;
                """;
            _ = command.Parameters.AddWithValue("$key", state.Content.Value);
            _ = command.Parameters.AddWithValue("$titleId", state.Content.TitleId.Value.ToString("D"));
            _ = command.Parameters.AddWithValue(
                "$episodeId",
                state.Content.EpisodeId is { } episode ? episode.Value.ToString("D") : DBNull.Value);
            _ = command.Parameters.AddWithValue("$position", state.Position.Ticks);
            _ = command.Parameters.AddWithValue(
                "$duration",
                state.ObservedDuration is { } observed ? observed.Ticks : DBNull.Value);
            _ = command.Parameters.AddWithValue("$source", state.SourceMediaFileId.Value.ToString("D"));
            _ = command.Parameters.AddWithValue("$status", (int)state.Status);
            _ = command.Parameters.AddWithValue("$override", state.IsManualOverride ? 1 : 0);
            _ = command.Parameters.AddWithValue("$startedUtc", Format(state.StartedUtc));
            _ = command.Parameters.AddWithValue("$updatedUtc", Format(state.UpdatedUtc));
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Format(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    private static WatchState Read(SqliteDataReader reader)
    {
        var titleId = new TitleId(Guid.Parse(reader.GetString(1), CultureInfo.InvariantCulture));
        var content = reader.IsDBNull(2)
            ? ContentKey.ForTitle(titleId)
            : ContentKey.ForEpisode(
                titleId,
                new EpisodeId(Guid.Parse(reader.GetString(2), CultureInfo.InvariantCulture)));
        return new WatchState
        {
            Content = content,
            Position = TimeSpan.FromTicks(reader.GetInt64(3)),
            ObservedDuration = reader.IsDBNull(4) ? null : TimeSpan.FromTicks(reader.GetInt64(4)),
            SourceMediaFileId = new MediaFileId(Guid.Parse(reader.GetString(5), CultureInfo.InvariantCulture)),
            Status = (WatchStatus)reader.GetInt32(6),
            IsManualOverride = reader.GetInt32(7) == 1,
            StartedUtc = DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture),
            UpdatedUtc = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture),
        };
    }
}
