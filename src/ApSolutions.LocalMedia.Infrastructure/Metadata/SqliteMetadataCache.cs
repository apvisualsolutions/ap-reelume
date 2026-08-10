// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Data;

namespace ApSolutions.LocalMedia.Infrastructure.Metadata;

public sealed class SqliteMetadataCache : IMetadataCache
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteMetadataCache(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<MetadataCacheEntry?> GetAsync(
        MetadataCacheKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT payload, etag, stored_utc, expires_utc
            FROM metadata_cache
            WHERE provider = $provider
              AND cache_key = $cacheKey
              AND language = $language
              AND provider_version = $providerVersion;
            """;
        AddKeyParameters(command, key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new MetadataCacheEntry(
            key,
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture));
    }

    public async Task StoreAsync(
        MetadataCacheEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO metadata_cache (
                provider, cache_key, language, provider_version,
                payload, etag, stored_utc, expires_utc)
            VALUES (
                $provider, $cacheKey, $language, $providerVersion,
                $payload, $etag, $storedUtc, $expiresUtc)
            ON CONFLICT(provider, cache_key, language, provider_version)
            DO UPDATE SET
                payload = excluded.payload,
                etag = excluded.etag,
                stored_utc = excluded.stored_utc,
                expires_utc = excluded.expires_utc;
            """;
        AddKeyParameters(command, entry.Key);
        command.Parameters.AddWithValue("$payload", entry.Payload);
        command.Parameters.AddWithValue("$etag", (object?)entry.ETag ?? DBNull.Value);
        command.Parameters.AddWithValue("$storedUtc", entry.StoredUtc.ToString("O"));
        command.Parameters.AddWithValue("$expiresUtc", entry.ExpiresUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(
        MetadataCacheKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM metadata_cache
            WHERE provider = $provider
              AND cache_key = $cacheKey
              AND language = $language
              AND provider_version = $providerVersion;
            """;
        AddKeyParameters(command, key);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddKeyParameters(Microsoft.Data.Sqlite.SqliteCommand command, MetadataCacheKey key)
    {
        command.Parameters.AddWithValue("$provider", key.Provider);
        command.Parameters.AddWithValue("$cacheKey", key.Key);
        command.Parameters.AddWithValue("$language", key.Language);
        command.Parameters.AddWithValue("$providerVersion", key.ProviderVersion);
    }
}
