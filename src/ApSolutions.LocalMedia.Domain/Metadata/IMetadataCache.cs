// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Metadata;

public sealed record MetadataCacheKey(
    string Provider,
    string Key,
    string Language,
    int ProviderVersion);

public sealed record MetadataCacheEntry(
    MetadataCacheKey Key,
    string Payload,
    string? ETag,
    DateTimeOffset StoredUtc,
    DateTimeOffset ExpiresUtc);

public interface IMetadataCache
{
    Task<MetadataCacheEntry?> GetAsync(
        MetadataCacheKey key,
        CancellationToken cancellationToken = default);

    Task StoreAsync(
        MetadataCacheEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops a cached entry. A provider whose licence caps how long content may be kept needs to
    /// forget, not merely to stop reading.
    /// </summary>
    Task RemoveAsync(
        MetadataCacheKey key,
        CancellationToken cancellationToken = default);
}
