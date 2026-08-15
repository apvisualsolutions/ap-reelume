// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// One row per title, holding what a person edited and what they locked.
/// <para>
/// The revision is checked inside the same statement that writes, so two windows editing the same
/// film cannot both win: the second one is told its copy is stale instead of quietly overwriting the
/// first. Locks are stored by name rather than by ordinal, because renumbering an enumeration must
/// never silently unlock somebody's title.
/// </para>
/// </summary>
public sealed class CatalogMetadataRepository : ICatalogMetadataRepository
{
    private const string Columns =
        "title_id, title, original_title, overview, release_year, genres, poster_path, backdrop_path, "
        + "trailer_key, locked_fields, revision, provider, provider_key, refreshed_utc";

    // A unit separator cannot occur inside a genre name or a field name, so no stored value can be
    // cut in half by the character that joins them.
    private const char FieldSeparator = (char)0x1F;

    private readonly SqliteConnectionFactory _connectionFactory;

    public CatalogMetadataRepository(SqliteConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<CatalogMetadata?> GetAsync(
        TitleId titleId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM catalog_metadata
            WHERE title_id = $id;
            """;
        _ = command.Parameters.AddWithValue("$id", titleId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>
    /// The stalest identified entries first. Dates are stored as round-trip UTC strings of fixed
    /// shape, so comparing them as text orders them as moments — which is why the column is written
    /// through <see cref="DateTimeOffset.ToUniversalTime"/> in every path that writes it.
    /// </summary>
    /// <remarks>
    /// <c>refreshed_utc IS NOT NULL</c> leads the ordering so that an entry with no date comes
    /// first: it was never refreshed, which makes it the stalest there is. The order is the policy
    /// here — with a cap on the pass, whatever sorts last is what does not get asked about.
    /// </remarks>
    public async Task<IReadOnlyList<CatalogMetadata>> ListStaleAsync(
        DateTimeOffset staleBefore,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM catalog_metadata
            WHERE provider IS NOT NULL
              AND provider_key IS NOT NULL
              AND (refreshed_utc IS NULL OR refreshed_utc < $staleBefore)
            ORDER BY refreshed_utc IS NOT NULL, refreshed_utc
            LIMIT $limit;
            """;
        _ = command.Parameters.AddWithValue(
            "$staleBefore",
            staleBefore.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        _ = command.Parameters.AddWithValue("$limit", limit);
        var stale = new List<CatalogMetadata>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            stale.Add(Read(reader));
        }

        return stale;
    }

    public async Task<MetadataWriteResult> TrySaveAsync(
        CatalogMetadata catalog,
        int expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO catalog_metadata ({Columns})
            VALUES ($id, $title, $originalTitle, $overview, $year, $genres, $poster, $backdrop, $trailer, $locked,
                    $revision, $provider, $providerKey, $refreshedUtc)
            ON CONFLICT (title_id) DO UPDATE SET
                title = excluded.title,
                original_title = excluded.original_title,
                overview = excluded.overview,
                release_year = excluded.release_year,
                genres = excluded.genres,
                poster_path = excluded.poster_path,
                backdrop_path = excluded.backdrop_path,
                trailer_key = excluded.trailer_key,
                locked_fields = excluded.locked_fields,
                revision = excluded.revision,
                provider = excluded.provider,
                provider_key = excluded.provider_key,
                refreshed_utc = excluded.refreshed_utc
            WHERE catalog_metadata.revision = $expected;
            """;
        // The next revision is computed here rather than taken from the caller. Every caller used to
        // pass the row back unchanged, so the stored revision never moved and two windows editing
        // the same title could both win — the check was running against a number that never changed.
        // The in-memory doubles raised it themselves, which is why no unit test could see it.
        var nextRevision = expectedRevision + 1;
        var metadata = catalog.Metadata;
        _ = command.Parameters.AddWithValue("$id", catalog.TitleId.Value.ToString("D"));
        _ = command.Parameters.AddWithValue("$title", metadata.Title);
        _ = command.Parameters.AddWithValue("$originalTitle", (object?)metadata.OriginalTitle ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$overview", (object?)metadata.Overview ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$year", (object?)metadata.ReleaseYear ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$genres", Join(metadata.Genres));
        _ = command.Parameters.AddWithValue("$poster", (object?)metadata.PosterPath ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$backdrop", (object?)metadata.BackdropPath ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$trailer", (object?)metadata.TrailerKey ?? DBNull.Value);
        _ = command.Parameters.AddWithValue(
            "$locked",
            Join(metadata.LockedFields.Select(field => field.ToString()).ToArray()));
        _ = command.Parameters.AddWithValue("$revision", nextRevision);
        _ = command.Parameters.AddWithValue("$provider", (object?)catalog.Provider ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$providerKey", (object?)catalog.ProviderKey ?? DBNull.Value);
        _ = command.Parameters.AddWithValue(
            "$refreshedUtc",
            catalog.RefreshedUtc is { } refreshed
                ? refreshed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : DBNull.Value);
        _ = command.Parameters.AddWithValue("$expected", expectedRevision);
        var written = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return written == 0
            ? new MetadataWriteResult(
                MetadataWriteOutcome.Conflict,
                await GetAsync(catalog.TitleId, cancellationToken).ConfigureAwait(false))
            : new MetadataWriteResult(
                MetadataWriteOutcome.Applied,
                catalog with { Revision = nextRevision });
    }

    private static CatalogMetadata Read(SqliteDataReader reader) => new(
        new TitleId(Guid.Parse(reader.GetString(0), CultureInfo.InvariantCulture)),
        new EditableMetadata(
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetInt32(4),
            Split(reader.GetString(5)),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            ReadLockedFields(reader.GetString(9))),
        reader.GetInt32(10),
        reader.IsDBNull(11) ? null : reader.GetString(11),
        reader.IsDBNull(12) ? null : reader.GetString(12),
        reader.IsDBNull(13)
            ? null
            : DateTimeOffset.Parse(reader.GetString(13), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static string Join(IReadOnlyList<string> values) => string.Join(FieldSeparator, values);

    private static string[] Split(string value) =>
        value.Split(FieldSeparator, StringSplitOptions.RemoveEmptyEntries);

    private static HashSet<MetadataField> ReadLockedFields(string value) =>
    [
        .. Split(value)
            .Select(name => Enum.TryParse<MetadataField>(name, out var field) ? field : (MetadataField?)null)
            .Where(field => field is not null)
            .Select(field => field!.Value)
    ];
}
