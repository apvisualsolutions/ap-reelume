// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.Json;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

public sealed class MediaFileRepository : IMediaFileRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public MediaFileRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<MediaFile?> FindByPathAsync(
        LibraryRootId rootId,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, library_root_id, normalized_path, size_bytes, last_write_utc,
                   duration_ticks, container, video_codecs, audio_codecs, width, height, is_available
            FROM media_files
            WHERE library_root_id = $rootId AND normalized_path = $path COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$rootId", rootId.Value.ToString("D"));
        command.Parameters.AddWithValue("$path", path);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadMediaFile(reader)
            : null;
    }

    public async Task<MediaFile?> FindByIdAsync(
        MediaFileId id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, library_root_id, normalized_path, size_bytes, last_write_utc,
                   duration_ticks, container, video_codecs, audio_codecs, width, height, is_available
            FROM media_files
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadMediaFile(reader)
            : null;
    }

    public async Task<IReadOnlyDictionary<string, MediaFile>> FindByPathsAsync(
        LibraryRootId rootId,
        IReadOnlyCollection<string> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return new Dictionary<string, MediaFile>(StringComparer.OrdinalIgnoreCase);
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        var pathParameters = paths.Select((_, index) => $"$path{index}").ToArray();
        command.CommandText = $"""
            SELECT id, library_root_id, normalized_path, size_bytes, last_write_utc,
                   duration_ticks, container, video_codecs, audio_codecs, width, height, is_available
            FROM media_files
            WHERE library_root_id = $rootId
              AND normalized_path COLLATE NOCASE IN ({string.Join(", ", pathParameters)});
            """;
        command.Parameters.AddWithValue("$rootId", rootId.Value.ToString("D"));
        var index = 0;
        foreach (var path in paths)
        {
            command.Parameters.AddWithValue(pathParameters[index], path);
            index++;
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var found = new Dictionary<string, MediaFile>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var mediaFile = ReadMediaFile(reader);
            found[mediaFile.Path] = mediaFile;
        }

        return found;
    }

    public Task UpsertAsync(
        MediaFile mediaFile,
        CancellationToken cancellationToken = default) =>
        UpsertBatchAsync([mediaFile], cancellationToken);

    public async Task UpsertBatchAsync(
        IReadOnlyCollection<MediaFile> mediaFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mediaFiles);
        if (mediaFiles.Count == 0)
        {
            return;
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        foreach (var mediaFile in mediaFiles)
        {
            ArgumentNullException.ThrowIfNull(mediaFile);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO media_files (
                    id, library_root_id, normalized_path, size_bytes, last_write_utc,
                    duration_ticks, container, video_codecs, audio_codecs, width, height, is_available)
                VALUES (
                    $id, $rootId, $path, $sizeBytes, $lastWriteUtc,
                    $durationTicks, $container, $videoCodecs, $audioCodecs, $width, $height, $isAvailable)
                ON CONFLICT(library_root_id, normalized_path) DO UPDATE SET
                    id = excluded.id,
                    size_bytes = excluded.size_bytes,
                    last_write_utc = excluded.last_write_utc,
                    duration_ticks = excluded.duration_ticks,
                    container = excluded.container,
                    video_codecs = excluded.video_codecs,
                    audio_codecs = excluded.audio_codecs,
                    width = excluded.width,
                    height = excluded.height,
                    is_available = excluded.is_available;
                """;
            command.Parameters.AddWithValue("$id", mediaFile.Id.Value.ToString("D"));
            command.Parameters.AddWithValue("$rootId", mediaFile.LibraryRootId.Value.ToString("D"));
            command.Parameters.AddWithValue("$path", mediaFile.Path);
            command.Parameters.AddWithValue("$sizeBytes", mediaFile.SizeBytes);
            command.Parameters.AddWithValue(
                "$lastWriteUtc",
                mediaFile.LastWriteUtc.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue(
                "$durationTicks",
                mediaFile.TechnicalMetadata.Duration is { } duration ? duration.Ticks : DBNull.Value);
            command.Parameters.AddWithValue("$container", mediaFile.TechnicalMetadata.Container);
            command.Parameters.AddWithValue(
                "$videoCodecs",
                JsonSerializer.Serialize(mediaFile.TechnicalMetadata.VideoCodecs));
            command.Parameters.AddWithValue(
                "$audioCodecs",
                JsonSerializer.Serialize(mediaFile.TechnicalMetadata.AudioCodecs));
            command.Parameters.AddWithValue("$width", (object?)mediaFile.TechnicalMetadata.Width ?? DBNull.Value);
            command.Parameters.AddWithValue("$height", (object?)mediaFile.TechnicalMetadata.Height ?? DBNull.Value);
            command.Parameters.AddWithValue("$isAvailable", mediaFile.IsAvailable ? 1 : 0);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await UpsertScannedTitleAsync(connection, transaction, mediaFile, cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IdentifiedMediaFile?> FindByStableIdentityAsync(
        FileIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!identity.HasStableFileId)
        {
            throw new ArgumentException("A stable file identity is required.", nameof(identity));
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {IdentifiedMediaSelect}
            WHERE identity.volume_id = $volumeId COLLATE NOCASE
              AND identity.file_id = $fileId COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$volumeId", identity.VolumeId!);
        command.Parameters.AddWithValue("$fileId", identity.FileId!);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadIdentifiedMediaFile(reader)
            : null;
    }

    public async Task<IReadOnlyList<IdentifiedMediaFile>> FindByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {IdentifiedMediaSelect}
            WHERE identity.fingerprint = $fingerprint COLLATE NOCASE
            ORDER BY media.id;
            """;
        command.Parameters.AddWithValue("$fingerprint", fingerprint);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var matches = new List<IdentifiedMediaFile>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            matches.Add(ReadIdentifiedMediaFile(reader));
        }

        return matches;
    }

    public async Task SaveIdentityAsync(
        MediaFileId mediaFileId,
        FileIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(identity);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = IdentityUpsert;
        AddIdentityParameters(command, mediaFileId, identity);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FileIdentity?> GetIdentityAsync(
        MediaFileId mediaFileId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT volume_id, file_id, fingerprint
            FROM media_file_identities
            WHERE media_file_id = $mediaFileId;
            """;
        command.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new FileIdentity(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    public async Task RemoveAsync(MediaFileId mediaFileId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();

        // The projection row goes first so its delete trigger clears the FTS entry; the identity
        // and candidate rows would otherwise keep pointing at a file that no longer exists.
        foreach (var statement in new[]
        {
            "DELETE FROM scanned_titles WHERE media_file_id = $mediaFileId;",
            "DELETE FROM media_file_identities WHERE media_file_id = $mediaFileId;",
            "DELETE FROM match_candidates WHERE media_file_id = $mediaFileId;",
            "DELETE FROM media_files WHERE id = $mediaFileId;",
        })
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            command.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReassignAsync(
        MediaFileId mediaFileId,
        LibraryRootId libraryRootId,
        string newPath,
        FileIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);
        ValidateIdentity(identity);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE media_files
                SET library_root_id = $rootId,
                    normalized_path = $path,
                    is_available = 1
                WHERE id = $mediaFileId;
                """;
            update.Parameters.AddWithValue("$rootId", libraryRootId.Value.ToString("D"));
            update.Parameters.AddWithValue("$path", newPath);
            update.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
            var rows = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (rows != 1)
            {
                throw new InvalidOperationException("The media file selected for reassignment no longer exists.");
            }
        }

        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = IdentityUpsert;
            AddIdentityParameters(upsert, mediaFileId, identity);
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var projection = connection.CreateCommand())
        {
            projection.Transaction = transaction;
            projection.CommandText = """
                UPDATE scanned_titles
                SET display_title = $displayTitle,
                    sort_title = $displayTitle
                WHERE media_file_id = $mediaFileId;
                """;
            projection.Parameters.AddWithValue("$displayTitle", CreateDisplayTitle(newPath));
            projection.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
            await projection.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetRootAvailabilityAsync(
        LibraryRootId libraryRootId,
        bool isAvailable,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE media_files
            SET is_available = $isAvailable
            WHERE library_root_id = $rootId;
            """;
        command.Parameters.AddWithValue("$isAvailable", isAvailable ? 1 : 0);
        command.Parameters.AddWithValue("$rootId", libraryRootId.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetScanCheckpointAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT resume_after_path FROM scan_checkpoints WHERE library_root_id = $rootId;";
        command.Parameters.AddWithValue("$rootId", rootId.Value.ToString("D"));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    public async Task SaveScanCheckpointAsync(
        LibraryRootId rootId,
        string resumeAfterPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resumeAfterPath);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO scan_checkpoints (library_root_id, resume_after_path, updated_utc)
            VALUES ($rootId, $path, $updatedUtc)
            ON CONFLICT(library_root_id) DO UPDATE SET
                resume_after_path = excluded.resume_after_path,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$rootId", rootId.Value.ToString("D"));
        command.Parameters.AddWithValue("$path", resumeAfterPath);
        command.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearScanCheckpointAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM scan_checkpoints WHERE library_root_id = $rootId;";
        command.Parameters.AddWithValue("$rootId", rootId.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static MediaFile ReadMediaFile(SqliteDataReader reader)
    {
        TimeSpan? duration = reader.IsDBNull(5) ? null : TimeSpan.FromTicks(reader.GetInt64(5));
        var metadata = new TechnicalMetadata(
            duration,
            reader.GetString(6),
            JsonSerializer.Deserialize<string[]>(reader.GetString(7)) ?? [],
            JsonSerializer.Deserialize<string[]>(reader.GetString(8)) ?? [],
            reader.IsDBNull(9) ? null : reader.GetInt32(9),
            reader.IsDBNull(10) ? null : reader.GetInt32(10));
        return new MediaFile(
            new MediaFileId(Guid.Parse(reader.GetString(0))),
            new LibraryRootId(Guid.Parse(reader.GetString(1))),
            reader.GetString(2),
            reader.GetInt64(3),
            DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            metadata,
            reader.GetInt32(11) == 1);
    }

    private static async Task UpsertScannedTitleAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        MediaFile mediaFile,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO scanned_titles (media_file_id, display_title, sort_title, added_utc)
            VALUES ($mediaFileId, $displayTitle, $displayTitle, $addedUtc)
            ON CONFLICT(media_file_id) DO UPDATE SET
                display_title = excluded.display_title,
                sort_title = excluded.sort_title;
            """;
        command.Parameters.AddWithValue("$mediaFileId", mediaFile.Id.Value.ToString("D"));
        command.Parameters.AddWithValue("$displayTitle", CreateDisplayTitle(mediaFile.Path));
        command.Parameters.AddWithValue(
            "$addedUtc",
            mediaFile.LastWriteUtc.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The name every scanned file has from the instant it is stored, before anything reads it
    /// properly.
    /// </summary>
    /// <remarks>
    /// This used to be the <b>only</b> name a card ever showed, which is why the grid said «El Faro
    /// de Piedra 2019» with an empty year beside it. It is the floor now: <c>NameScannedTitles</c>
    /// runs after every scan and writes the parsed name over it. The floor stays because this row is
    /// <c>NOT NULL</c> and a file has to be findable between being stored and being named.
    /// </remarks>
    private static string CreateDisplayTitle(string path)
    {
        var title = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(title) ? Path.GetFileName(path) : title;
    }

    /// <inheritdoc />
    public async Task SetScannedTitleAsync(
        MediaFileId mediaFileId,
        ScannedTitle title,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(title);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        // Only where there is a row to name. A file stored without a projection is not a state this
        // repository can produce — the two are written in one transaction — and an INSERT here would
        // be a second place that decides what scanned_titles holds.
        command.CommandText = """
            UPDATE scanned_titles
            SET display_title = $displayTitle,
                sort_title = $displayTitle,
                release_year = $releaseYear
            WHERE media_file_id = $mediaFileId;
            """;
        command.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
        command.Parameters.AddWithValue("$displayTitle", title.DisplayTitle);
        command.Parameters.AddWithValue("$releaseYear", (object?)title.Year ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IdentifiedMediaFile ReadIdentifiedMediaFile(SqliteDataReader reader) => new(
        ReadMediaFile(reader),
        new FileIdentity(
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetString(14)));

    private static void ValidateIdentity(FileIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!identity.HasStableFileId && !identity.HasFingerprint)
        {
            throw new ArgumentException("A stable file id or fingerprint is required.", nameof(identity));
        }
    }

    private static void AddIdentityParameters(
        SqliteCommand command,
        MediaFileId mediaFileId,
        FileIdentity identity)
    {
        command.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
        command.Parameters.AddWithValue("$volumeId", (object?)identity.VolumeId ?? DBNull.Value);
        command.Parameters.AddWithValue("$fileId", (object?)identity.FileId ?? DBNull.Value);
        command.Parameters.AddWithValue("$fingerprint", (object?)identity.Fingerprint ?? DBNull.Value);
    }

    private const string IdentifiedMediaSelect = """
        SELECT media.id, media.library_root_id, media.normalized_path, media.size_bytes,
               media.last_write_utc, media.duration_ticks, media.container, media.video_codecs,
               media.audio_codecs, media.width, media.height, media.is_available,
               identity.volume_id, identity.file_id, identity.fingerprint
        FROM media_files AS media
        INNER JOIN media_file_identities AS identity ON identity.media_file_id = media.id
        """;

    private const string IdentityUpsert = """
        INSERT INTO media_file_identities (media_file_id, volume_id, file_id, fingerprint)
        VALUES ($mediaFileId, $volumeId, $fileId, $fingerprint)
        ON CONFLICT(media_file_id) DO UPDATE SET
            volume_id = excluded.volume_id,
            file_id = excluded.file_id,
            fingerprint = excluded.fingerprint;
        """;
}
