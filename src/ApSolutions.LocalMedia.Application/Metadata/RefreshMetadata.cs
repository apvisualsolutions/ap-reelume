// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Application.Metadata;

public sealed record RefreshMetadataCommand(
    TitleId TitleId,
    int ExpectedRevision,
    bool RestoreProviderFields);

/// <summary>
/// Asks the provider again for a title that was already identified.
/// </summary>
/// <remarks>
/// <para>
/// It resolves the reference itself, from what the row stores, rather than being handed a
/// <see cref="MetadataDetails"/>. The previous shape took one as input and the only caller that
/// could have supplied it was a test: the application built the editor without it, so both provider
/// buttons were visible, enabled, and left through the first guard.
/// </para>
/// <para>
/// A title nobody identified is not a failure here. It has nothing to refresh against, and saying so
/// is the difference between a button that explains itself and one that does nothing.
/// </para>
/// </remarks>
public sealed class RefreshMetadata(
    ICatalogMetadataRepository repository,
    IMetadataProvider provider,
    MetadataMergePolicy mergePolicy,
    MetadataLanguage language,
    TimeProvider timeProvider)
{
    public async Task<MetadataWriteResult> ExecuteAsync(
        RefreshMetadataCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var current = await repository.GetAsync(command.TitleId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return new MetadataWriteResult(MetadataWriteOutcome.NotFound, null);
        }

        // A key stored under another provider's name is not this provider's to read, so it counts as
        // unidentified here rather than being guessed at.
        if (current.ProviderKey is not { } key
            || !string.Equals(current.Provider, provider.Name, StringComparison.Ordinal)
            || provider.TryCreateReference(key) is not { } reference)
        {
            return new MetadataWriteResult(MetadataWriteOutcome.NotIdentified, current);
        }

        var details = await provider
            .GetDetailsAsync(reference, language, cancellationToken)
            .ConfigureAwait(false);
        if (details is null)
        {
            return new MetadataWriteResult(MetadataWriteOutcome.Unavailable, current);
        }

        var baseMetadata = command.RestoreProviderFields
            ? current.Metadata with { LockedFields = new HashSet<MetadataField>() }
            : current.Metadata;
        var merged = mergePolicy.Merge(baseMetadata, details);

        return await repository.TrySaveAsync(
            current with
            {
                Metadata = merged,
                Provider = details.Reference.Provider,
                ProviderKey = details.Reference.Key,
                RefreshedUtc = timeProvider.GetUtcNow(),
            },
            command.ExpectedRevision,
            cancellationToken).ConfigureAwait(false);
    }
}
