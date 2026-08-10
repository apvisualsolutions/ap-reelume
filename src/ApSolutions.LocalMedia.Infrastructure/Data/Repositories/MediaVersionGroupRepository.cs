// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// The versions of one piece of content and which of them a person pinned.
/// <para>
/// Members are replaced as a set inside one transaction: a group that briefly held half its versions
/// would let the selection policy choose a version that is on its way out. The stored order is kept
/// so the surface lists two copies of the same film the same way every time it is opened.
/// </para>
/// </summary>
public sealed class MediaVersionGroupRepository : IMediaVersionGroupRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public MediaVersionGroupRepository(SqliteConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public Task<MediaVersionGroup?> FindByContentKeyAsync(
        string contentKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentKey);
        return FindAsync("content_key", contentKey, cancellationToken);
    }

    public Task<MediaVersionGroup?> FindByIdAsync(
        MediaVersionId groupId,
        CancellationToken cancellationToken = default) =>
        FindAsync("id", groupId.Value.ToString("D"), cancellationToken);

    public async Task<MediaVersionGroup?> FindByMemberAsync(
        MediaFileId mediaFileId,
        CancellationToken cancellationToken = default)
    {
        string? groupId;
        await using (var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT group_id
                FROM media_version_group_members
                WHERE media_file_id = $mediaFileId
                LIMIT 1;
                """;
            _ = command.Parameters.AddWithValue("$mediaFileId", mediaFileId.Value.ToString("D"));
            groupId = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }

        return groupId is null
            ? null
            : await FindAsync("id", groupId, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(MediaVersionGroup group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var groupId = group.Id.Value.ToString("D");

        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = (SqliteTransaction)transaction;
            upsert.CommandText = """
                INSERT INTO media_version_groups (id, content_key, preferred_media_file_id)
                VALUES ($id, $contentKey, $preferred)
                ON CONFLICT (id) DO UPDATE SET
                    content_key = excluded.content_key,
                    preferred_media_file_id = excluded.preferred_media_file_id;
                """;
            _ = upsert.Parameters.AddWithValue("$id", groupId);
            _ = upsert.Parameters.AddWithValue("$contentKey", group.ContentKey);
            _ = upsert.Parameters.AddWithValue(
                "$preferred",
                group.PreferredMediaFileId is { } preferred
                    ? preferred.Value.ToString("D")
                    : (object)DBNull.Value);
            _ = await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = (SqliteTransaction)transaction;
            clear.CommandText = "DELETE FROM media_version_group_members WHERE group_id = $id;";
            _ = clear.Parameters.AddWithValue("$id", groupId);
            _ = await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        for (var ordinal = 0; ordinal < group.Versions.Count; ordinal++)
        {
            var version = group.Versions[ordinal];
            await using var insert = connection.CreateCommand();
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = """
                INSERT INTO media_version_group_members (
                    group_id, media_file_id, path, is_available, duration_ticks,
                    width, height, is_hdr, video_codec, size_bytes, ordinal)
                VALUES (
                    $groupId, $mediaFileId, $path, $isAvailable, $duration,
                    $width, $height, $isHdr, $codec, $size, $ordinal);
                """;
            _ = insert.Parameters.AddWithValue("$groupId", groupId);
            _ = insert.Parameters.AddWithValue("$mediaFileId", version.MediaFileId.Value.ToString("D"));
            _ = insert.Parameters.AddWithValue("$path", version.Path);
            _ = insert.Parameters.AddWithValue("$isAvailable", version.IsAvailable ? 1 : 0);
            _ = insert.Parameters.AddWithValue(
                "$duration",
                version.Duration is { } duration ? duration.Ticks : (object)DBNull.Value);
            _ = insert.Parameters.AddWithValue("$width", (object?)version.Width ?? DBNull.Value);
            _ = insert.Parameters.AddWithValue("$height", (object?)version.Height ?? DBNull.Value);
            _ = insert.Parameters.AddWithValue("$isHdr", version.IsHdr ? 1 : 0);
            _ = insert.Parameters.AddWithValue("$codec", version.VideoCodec);
            _ = insert.Parameters.AddWithValue("$size", version.SizeBytes);
            _ = insert.Parameters.AddWithValue("$ordinal", ordinal);
            _ = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<MediaVersionGroup?> FindAsync(
        string column,
        string value,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT id, content_key, preferred_media_file_id
            FROM media_version_groups
            WHERE {column} = $value;
            """;
        _ = command.Parameters.AddWithValue("$value", value);
        string groupId;
        string contentKey;
        MediaFileId? preferred;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            groupId = reader.GetString(0);
            contentKey = reader.GetString(1);
            preferred = reader.IsDBNull(2)
                ? null
                : new MediaFileId(Guid.Parse(reader.GetString(2), CultureInfo.InvariantCulture));
        }

        return new MediaVersionGroup(
            new MediaVersionId(Guid.Parse(groupId, CultureInfo.InvariantCulture)),
            contentKey,
            await ReadMembersAsync(connection, groupId, cancellationToken).ConfigureAwait(false),
            preferred);
    }

    private static async Task<IReadOnlyList<MediaVersion>> ReadMembersAsync(
        SqliteConnection connection,
        string groupId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT media_file_id, path, is_available, duration_ticks, width, height, is_hdr,
                   video_codec, size_bytes
            FROM media_version_group_members
            WHERE group_id = $id
            ORDER BY ordinal;
            """;
        _ = command.Parameters.AddWithValue("$id", groupId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var versions = new List<MediaVersion>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            versions.Add(new MediaVersion(
                new MediaFileId(Guid.Parse(reader.GetString(0), CultureInfo.InvariantCulture)),
                reader.GetString(1),
                reader.GetInt32(2) == 1,
                reader.IsDBNull(3) ? null : TimeSpan.FromTicks(reader.GetInt64(3)),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.GetInt32(6) == 1,
                reader.GetString(7),
                reader.GetInt64(8)));
        }

        return versions;
    }
}
