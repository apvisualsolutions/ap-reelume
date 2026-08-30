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
public sealed class RefreshMetadata
{
    private readonly ICatalogMetadataRepository _repository;
    private readonly IMetadataProvider _provider;
    private readonly MetadataMergePolicy _mergePolicy;
    private readonly MetadataLanguage _language;
    private readonly TimeProvider _timeProvider;

    public RefreshMetadata(
        ICatalogMetadataRepository repository,
        IMetadataProvider provider,
        MetadataMergePolicy mergePolicy,
        MetadataLanguage language,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _mergePolicy = mergePolicy ?? throw new ArgumentNullException(nameof(mergePolicy));
        _language = language ?? throw new ArgumentNullException(nameof(language));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<MetadataWriteResult> ExecuteAsync(
        RefreshMetadataCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var current = await _repository.GetAsync(command.TitleId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return new MetadataWriteResult(MetadataWriteOutcome.NotFound, null);
        }

        // A key stored under another provider's name is not this provider's to read, so it counts as
        // unidentified here rather than being guessed at.
        if (current.ProviderKey is not { } key
            || !string.Equals(current.Provider, _provider.Name, StringComparison.Ordinal)
            || _provider.TryCreateReference(key) is not { } reference)
        {
            return new MetadataWriteResult(MetadataWriteOutcome.NotIdentified, current);
        }

        var details = await _provider
            .GetDetailsAsync(reference, _language, cancellationToken)
            .ConfigureAwait(false);
        if (details is null)
        {
            return new MetadataWriteResult(MetadataWriteOutcome.Unavailable, current);
        }

        var baseMetadata = command.RestoreProviderFields
            ? current.Metadata with { LockedFields = new HashSet<MetadataField>() }
            : current.Metadata;
        var merged = _mergePolicy.Merge(baseMetadata, details);

        return await _repository.TrySaveAsync(
            current with
            {
                Metadata = merged,
                Provider = details.Reference.Provider,
                ProviderKey = details.Reference.Key,
                RefreshedUtc = _timeProvider.GetUtcNow(),
            },
            command.ExpectedRevision,
            cancellationToken).ConfigureAwait(false);
    }
}
