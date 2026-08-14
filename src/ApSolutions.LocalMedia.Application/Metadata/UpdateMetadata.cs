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

    /// <summary>
    /// Nobody has identified this title, so there is no provider entry to refresh it against. It is
    /// a state to explain, not a fault to report.
    /// </summary>
    NotIdentified,

    /// <summary>
    /// The title is identified but the provider had no answer to give — which, with no consented
    /// connection, is what an entry that is not already cached looks like.
    /// </summary>
    Unavailable,
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
    private static readonly EditableMetadata Empty = new(
        string.Empty,
        OriginalTitle: null,
        Overview: null,
        ReleaseYear: null,
        Genres: [],
        PosterPath: null,
        BackdropPath: null,
        TrailerKey: null,
        LockedFields: new HashSet<MetadataField>());


    public async Task<MetadataWriteResult> ExecuteAsync(
        UpdateMetadataCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // A title nobody has edited has no row yet, and its first edit is what creates one — which is
        // what CompositionRoot's own comment always claimed happened. It did not: this returned
        // NotFound, the editor turned that into neither a conflict nor a change, and Save on a fresh
        // title was a button that did nothing.
        var current = await repository.GetAsync(command.TitleId, cancellationToken).ConfigureAwait(false)
            ?? new CatalogMetadata(command.TitleId, Empty, Revision: 0);

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
