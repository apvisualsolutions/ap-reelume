// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Application.Metadata;

public sealed record RefreshMetadataCommand(
    TitleId TitleId,
    MetadataDetails ProviderMetadata,
    int ExpectedRevision,
    bool RestoreProviderFields);

public sealed class RefreshMetadata(
    ICatalogMetadataRepository repository,
    MetadataMergePolicy mergePolicy)
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

        var baseMetadata = command.RestoreProviderFields
            ? current.Metadata with { LockedFields = new HashSet<MetadataField>() }
            : current.Metadata;
        var merged = mergePolicy.Merge(baseMetadata, command.ProviderMetadata);

        return await repository.TrySaveAsync(
            current with { Metadata = merged },
            command.ExpectedRevision,
            cancellationToken).ConfigureAwait(false);
    }
}
