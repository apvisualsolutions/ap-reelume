// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// One row per episode file and kind. The repository stores exactly what it is given: which rows
/// survive a re-detection is the policy's decision, made before anything reaches here.
/// </summary>
public sealed class DetectedMarkerRepository : IDetectedMarkerRepository
{
    private const string Columns = """
        id, series_id, file_id, kind, start_ticks, end_ticks, confidence, detector_version,
        user_corrected
        """;

    private readonly SqliteConnectionFactory _connectionFactory;

    public DetectedMarkerRepository(SqliteConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<IReadOnlyList<DetectedMarker>> GetForSeriesAsync(
        SeriesId seriesId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM detected_markers
            WHERE series_id = $seriesId
            ORDER BY file_id, start_ticks, kind;
            """;
        _ = command.Parameters.AddWithValue("$seriesId", seriesId.Value.ToString("D"));
        return await ReadAllAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DetectedMarker>> GetForFileAsync(
        MediaFileId fileId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM detected_markers
            WHERE file_id = $fileId
            ORDER BY start_ticks, kind;
            """;
        _ = command.Parameters.AddWithValue("$fileId", fileId.Value.ToString("D"));
        return await ReadAllAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DetectedMarker?> GetAsync(Guid markerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM detected_markers
            WHERE id = $id;
            """;
        _ = command.Parameters.AddWithValue("$id", markerId.ToString("D"));
        var markers = await ReadAllAsync(command, cancellationToken).ConfigureAwait(false);
        return markers.Count == 0 ? null : markers[0];
    }

    public async Task ReplaceForSeriesAsync(
        SeriesId seriesId,
        IReadOnlyList<DetectedMarker> markers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markers);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = (SqliteTransaction)transaction;
            delete.CommandText = "DELETE FROM detected_markers WHERE series_id = $seriesId;";
            _ = delete.Parameters.AddWithValue("$seriesId", seriesId.Value.ToString("D"));
            _ = await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var marker in markers)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = (SqliteTransaction)transaction;
            AppendUpsert(insert, marker);
            _ = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(DetectedMarker marker, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(marker);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            AppendUpsert(command, marker);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid markerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM detected_markers WHERE id = $id;";
        _ = command.Parameters.AddWithValue("$id", markerId.ToString("D"));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AppendUpsert(SqliteCommand command, DetectedMarker marker)
    {
        command.CommandText = """
            INSERT INTO detected_markers (
                id, series_id, file_id, kind, start_ticks, end_ticks, confidence, detector_version,
                user_corrected, updated_utc)
            VALUES (
                $id, $seriesId, $fileId, $kind, $start, $end, $confidence, $version, $corrected,
                $updatedUtc)
            ON CONFLICT(id) DO UPDATE SET
                series_id = excluded.series_id,
                file_id = excluded.file_id,
                kind = excluded.kind,
                start_ticks = excluded.start_ticks,
                end_ticks = excluded.end_ticks,
                confidence = excluded.confidence,
                detector_version = excluded.detector_version,
                user_corrected = excluded.user_corrected,
                updated_utc = excluded.updated_utc;
            """;
        _ = command.Parameters.AddWithValue("$id", marker.Id.ToString("D"));
        _ = command.Parameters.AddWithValue("$seriesId", marker.SeriesId.Value.ToString("D"));
        _ = command.Parameters.AddWithValue("$fileId", marker.FileId.Value.ToString("D"));
        _ = command.Parameters.AddWithValue("$kind", (int)marker.Kind);
        _ = command.Parameters.AddWithValue("$start", marker.Start.Ticks);
        _ = command.Parameters.AddWithValue("$end", marker.End.Ticks);
        _ = command.Parameters.AddWithValue("$confidence", marker.Confidence);
        _ = command.Parameters.AddWithValue("$version", marker.DetectorVersion);
        _ = command.Parameters.AddWithValue("$corrected", marker.UserCorrected ? 1 : 0);
        _ = command.Parameters.AddWithValue(
            "$updatedUtc",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
    }

    private static async Task<IReadOnlyList<DetectedMarker>> ReadAllAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var markers = new List<DetectedMarker>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            markers.Add(new DetectedMarker(
                Guid.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                new SeriesId(Guid.Parse(reader.GetString(1), CultureInfo.InvariantCulture)),
                new MediaFileId(Guid.Parse(reader.GetString(2), CultureInfo.InvariantCulture)),
                (MarkerKind)reader.GetInt32(3),
                TimeSpan.FromTicks(reader.GetInt64(4)),
                TimeSpan.FromTicks(reader.GetInt64(5)),
                reader.GetDouble(6),
                reader.GetInt32(7),
                reader.GetInt32(8) == 1));
        }

        return markers;
    }
}
