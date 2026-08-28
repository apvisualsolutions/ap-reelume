// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Application.Identification;

public enum ApplyIdentificationOutcome
{
    /// <summary>The provider answered and the catalogue now holds what it said.</summary>
    Applied,

    /// <summary>
    /// The provider had nothing to give. Without consent it serves only what it cached, so a title
    /// nobody has looked up yet simply stays as the file name parser left it. That is not a failure.
    /// </summary>
    Unavailable,

    /// <summary>Somebody else wrote this title first, and their revision stands.</summary>
    Conflict,
}

public sealed record ApplyIdentificationResult(
    ApplyIdentificationOutcome Outcome,
    CatalogMetadata? Catalog);

public sealed record ApplyIdentificationCommand(
    MediaFileId MediaFileId,
    string ProviderKey,
    MetadataContentKind Kind);

/// <summary>
/// Turns an identification into stored metadata. This is the link the August audit stopped one
/// short of: identification was invoked, candidates were scored, a decision was recorded — and
/// nothing ever wrote what the provider knew into the row the catalogue reads.
/// </summary>
/// <remarks>
/// <para>
/// It is a use case of its own rather than a branch inside <c>ResolveMatch</c>, which would mix
/// marking a review with talking over the network, or a handler on <c>ReviewInboxChanged</c>, where
/// a failure would have nobody to report to.
/// </para>
/// <para>
/// What a person locked keeps winning: the provider's answer goes through
/// <see cref="MetadataMergePolicy"/> over whatever the row already held, exactly as a manual refresh
/// does. Applying to a title nobody has edited creates the row, which is why the write goes to the
/// repository rather than through <c>UpdateMetadata</c> — that one refuses a title with no row yet.
/// </para>
/// </remarks>
public sealed class ApplyIdentification(
    ICatalogMetadataRepository repository,
    IMetadataProvider provider,
    MetadataMergePolicy mergePolicy,
    MetadataLanguage language,
    TimeProvider timeProvider,
    CacheTitleArtwork artwork)
{
    public async Task<ApplyIdentificationResult> ExecuteAsync(
        ApplyIdentificationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ProviderKey);

        // The bridge, measured rather than assumed: nothing in the application writes the `titles`
        // table — its only writer, UpsertTitleAsync, has no caller outside integration tests — so
        // every title the catalogue projects is a scanned file, and its id is the media file's own.
        // CompositionRoot already crosses this bridge in both directions, for renaming and for
        // playback resume.
        var titleId = new TitleId(command.MediaFileId.Value);
        var reference = new MetadataReference(provider.Name, command.ProviderKey, command.Kind);
        var details = await provider
            .GetDetailsAsync(reference, language, cancellationToken)
            .ConfigureAwait(false);
        if (details is null)
        {
            return new ApplyIdentificationResult(ApplyIdentificationOutcome.Unavailable, null);
        }

        var current = await repository.GetAsync(titleId, cancellationToken).ConfigureAwait(false);
        var expectedRevision = current?.Revision ?? 0;
        var baseMetadata = current?.Metadata ?? Empty(details.Title);
        var merged = mergePolicy.Merge(baseMetadata, details);
        var write = await repository.TrySaveAsync(
            new CatalogMetadata(
                titleId,
                merged,
                expectedRevision,
                details.Reference.Provider,
                details.Reference.Key,
                timeProvider.GetUtcNow()),
            expectedRevision,
            cancellationToken).ConfigureAwait(false);

        // The poster comes down here and nowhere else, which is what keeps the film card from ever
        // opening a connection: this is the one moment somebody has already consented to talk to the
        // provider, and the card only ever reads the disk afterwards. The answer is thrown away on
        // purpose - a title identified with no poster fetched is a title identified, and the path is
        // rebuilt from the same address when a surface asks for it.
        _ = await artwork
            .ExecuteAsync(titleId, merged.PosterPath, merged.Title, cancellationToken)
            .ConfigureAwait(false);

        return new ApplyIdentificationResult(
            write.Outcome == MetadataWriteOutcome.Applied
                ? ApplyIdentificationOutcome.Applied
                : ApplyIdentificationOutcome.Conflict,
            write.Catalog);
    }

    /// <summary>
    /// The row a title with no edits starts from. The provider's title seeds it because the merge
    /// keeps the current value whenever the remote one is blank, and an empty title would then be
    /// the one thing an identification could not fix.
    /// </summary>
    private static EditableMetadata Empty(string title) => new(
        title,
        OriginalTitle: null,
        Overview: null,
        ReleaseYear: null,
        Genres: [],
        PosterPath: null,
        BackdropPath: null,
        TrailerKey: null,
        LockedFields: new HashSet<MetadataField>());
}
