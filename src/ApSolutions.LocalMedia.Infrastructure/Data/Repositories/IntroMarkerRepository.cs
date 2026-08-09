using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// One row per range. The origin and confidence columns are written as they arrive so a later release
/// can add detected ranges beside the manual ones without migrating anything.
/// </summary>
public sealed class IntroMarkerRepository : IIntroMarkerRepository
{
    private const string Columns = """
        id, series_id, kind, start_ticks, end_ticks, origin, confidence, user_corrected
        """;

    private readonly SqliteConnectionFactory _connectionFactory;

    public IntroMarkerRepository(SqliteConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<IReadOnlyList<IntroMarker>> GetForSeriesAsync(
        SeriesId seriesId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM intro_markers
            WHERE series_id = $seriesId
            ORDER BY start_ticks, kind;
            """;
        _ = command.Parameters.AddWithValue("$seriesId", seriesId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var markers = new List<IntroMarker>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            markers.Add(Read(reader));
        }

        return markers;
    }

    public async Task<IntroMarker?> GetAsync(Guid markerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM intro_markers
            WHERE id = $id;
            """;
        _ = command.Parameters.AddWithValue("$id", markerId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task SaveAsync(IntroMarker marker, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(marker);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO intro_markers (
                    id, series_id, kind, start_ticks, end_ticks, origin, confidence, user_corrected,
                    updated_utc)
                VALUES (
                    $id, $seriesId, $kind, $start, $end, $origin, $confidence, $corrected, $updatedUtc)
                ON CONFLICT(id) DO UPDATE SET
                    series_id = excluded.series_id,
                    kind = excluded.kind,
                    start_ticks = excluded.start_ticks,
                    end_ticks = excluded.end_ticks,
                    origin = excluded.origin,
                    confidence = excluded.confidence,
                    user_corrected = excluded.user_corrected,
                    updated_utc = excluded.updated_utc;
                """;
            _ = command.Parameters.AddWithValue("$id", marker.Id.ToString("D"));
            _ = command.Parameters.AddWithValue("$seriesId", marker.SeriesId.Value.ToString("D"));
            _ = command.Parameters.AddWithValue("$kind", (int)marker.Kind);
            _ = command.Parameters.AddWithValue("$start", marker.Start.Ticks);
            _ = command.Parameters.AddWithValue("$end", marker.End.Ticks);
            _ = command.Parameters.AddWithValue("$origin", (int)marker.Origin);
            _ = command.Parameters.AddWithValue(
                "$confidence",
                marker.Confidence is { } confidence ? confidence : DBNull.Value);
            _ = command.Parameters.AddWithValue("$corrected", marker.UserCorrected ? 1 : 0);
            _ = command.Parameters.AddWithValue(
                "$updatedUtc",
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid markerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM intro_markers WHERE id = $id;";
        _ = command.Parameters.AddWithValue("$id", markerId.ToString("D"));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IntroMarker Read(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
        new SeriesId(Guid.Parse(reader.GetString(1), CultureInfo.InvariantCulture)),
        (MarkerKind)reader.GetInt32(2),
        TimeSpan.FromTicks(reader.GetInt64(3)),
        TimeSpan.FromTicks(reader.GetInt64(4)),
        (MarkerOrigin)reader.GetInt32(5),
        reader.IsDBNull(6) ? null : reader.GetDouble(6),
        reader.GetInt32(7) == 1);
}
