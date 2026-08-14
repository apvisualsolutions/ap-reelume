// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Application.Metadata;

public sealed record MetadataFieldChanges(
    string? Title = null,
    string? OriginalTitle = null,
    string? Overview = null,
    int? ReleaseYear = null,
    IReadOnlyList<string>? Genres = null,
    string? PosterPath = null,
    string? BackdropPath = null);

/// <param name="Provider">
/// Which provider identified this title, or nothing when nobody has. Stored beside its key rather
/// than as a <see cref="MetadataReference"/> because the kind that reference also carries belongs to
/// the provider's own key format: reconstructing it is the provider's job, not the database's.
/// </param>
/// <param name="ProviderKey">
/// The provider's own identifier for this title. It is what lets a refresh resolve on its own
/// instead of waiting for a caller to hand it details that no caller ever handed it — the defect
/// that made both provider buttons inert.
/// </param>
/// <param name="RefreshedUtc">When the provider last answered for this title.</param>
public sealed record CatalogMetadata(
    TitleId TitleId,
    EditableMetadata Metadata,
    int Revision,
    string? Provider = null,
    string? ProviderKey = null,
    DateTimeOffset? RefreshedUtc = null);

public enum MetadataWriteOutcome
{
    Applied,
    Conflict,
    NotFound,
}

public sealed record MetadataWriteResult(
    MetadataWriteOutcome Outcome,
    CatalogMetadata? Catalog);

public interface ICatalogMetadataRepository
{
    Task<CatalogMetadata?> GetAsync(
        TitleId titleId,
        CancellationToken cancellationToken = default);

    Task<MetadataWriteResult> TrySaveAsync(
        CatalogMetadata catalog,
        int expectedRevision,
        CancellationToken cancellationToken = default);
}

public sealed record UpdateMetadataCommand(
    TitleId TitleId,
    MetadataFieldChanges FieldChanges,
    IReadOnlySet<MetadataField> LockedFields,
    int ExpectedRevision);

public sealed class UpdateMetadata(ICatalogMetadataRepository repository)
{
    public async Task<MetadataWriteResult> ExecuteAsync(
        UpdateMetadataCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var current = await repository.GetAsync(command.TitleId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return new MetadataWriteResult(MetadataWriteOutcome.NotFound, null);
        }

        var changes = command.FieldChanges;
        var updated = current.Metadata with
        {
            Title = changes.Title ?? current.Metadata.Title,
            OriginalTitle = changes.OriginalTitle ?? current.Metadata.OriginalTitle,
            Overview = changes.Overview ?? current.Metadata.Overview,
            ReleaseYear = changes.ReleaseYear ?? current.Metadata.ReleaseYear,
            Genres = changes.Genres is null ? current.Metadata.Genres : [.. changes.Genres],
            PosterPath = changes.PosterPath ?? current.Metadata.PosterPath,
            BackdropPath = changes.BackdropPath ?? current.Metadata.BackdropPath,
            LockedFields = command.LockedFields.ToHashSet(),
        };

        return await repository.TrySaveAsync(
            current with { Metadata = updated },
            command.ExpectedRevision,
            cancellationToken).ConfigureAwait(false);
    }
}
