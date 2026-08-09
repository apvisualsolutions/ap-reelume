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
}
