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
        + "locked_fields, revision";

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
            VALUES ($id, $title, $originalTitle, $overview, $year, $genres, $poster, $backdrop, $locked, $revision)
            ON CONFLICT (title_id) DO UPDATE SET
                title = excluded.title,
                original_title = excluded.original_title,
                overview = excluded.overview,
                release_year = excluded.release_year,
                genres = excluded.genres,
                poster_path = excluded.poster_path,
                backdrop_path = excluded.backdrop_path,
                locked_fields = excluded.locked_fields,
                revision = excluded.revision
            WHERE catalog_metadata.revision = $expected;
            """;
        var metadata = catalog.Metadata;
        _ = command.Parameters.AddWithValue("$id", catalog.TitleId.Value.ToString("D"));
        _ = command.Parameters.AddWithValue("$title", metadata.Title);
        _ = command.Parameters.AddWithValue("$originalTitle", (object?)metadata.OriginalTitle ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$overview", (object?)metadata.Overview ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$year", (object?)metadata.ReleaseYear ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$genres", Join(metadata.Genres));
        _ = command.Parameters.AddWithValue("$poster", (object?)metadata.PosterPath ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$backdrop", (object?)metadata.BackdropPath ?? DBNull.Value);
        _ = command.Parameters.AddWithValue(
            "$locked",
            Join(metadata.LockedFields.Select(field => field.ToString()).ToArray()));
        _ = command.Parameters.AddWithValue("$revision", catalog.Revision);
        _ = command.Parameters.AddWithValue("$expected", expectedRevision);
        var written = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return written == 0
            ? new MetadataWriteResult(
                MetadataWriteOutcome.Conflict,
                await GetAsync(catalog.TitleId, cancellationToken).ConfigureAwait(false))
            : new MetadataWriteResult(MetadataWriteOutcome.Applied, catalog);
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
            ReadLockedFields(reader.GetString(8))),
        reader.GetInt32(9));

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
