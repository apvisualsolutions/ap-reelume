// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Application.Tests.Identification;

/// <summary>
/// A provider that answers only for the references it was given. Answering nothing is the shipped
/// default and not a fault: without a token the real provider serves its cache, and a title nobody
/// looked up yet has no cached answer.
/// </summary>
internal sealed class StubMetadataProvider(params MetadataDetails[] details) : IMetadataProvider
{
    private readonly Dictionary<string, MetadataDetails> _details =
        details.ToDictionary(entry => entry.Reference.Key, StringComparer.Ordinal);

    public string Name => "tmdb";

    public List<MetadataReference> Requested { get; } = [];

    /// <summary>A key the provider refuses to answer for, the way a request that fails behaves.</summary>
    public string? ThrowOnKey { get; set; }

    public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        MetadataSearchQuery query,
        MetadataLanguage language,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MetadataSearchResult>>([]);

    public Task<MetadataDetails?> GetDetailsAsync(
        MetadataReference reference,
        MetadataLanguage language,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Requested.Add(reference);
        if (string.Equals(ThrowOnKey, reference.Key, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The provider refused this reference.");
        }

        return Task.FromResult(_details.TryGetValue(reference.Key, out var found) ? found : null);
    }
}

/// <summary>
/// Everything a title's metadata row is, in memory, with the same optimistic revision the SQLite
/// repository enforces inside its write statement.
/// </summary>
internal sealed class MemoryCatalogMetadataRepository : ICatalogMetadataRepository
{
    public Dictionary<TitleId, CatalogMetadata> Rows { get; } = [];

    /// <summary>
    /// Runs after a read has been answered, which is where a second window's write lands when two
    /// of them race for the same title.
    /// </summary>
    public Action? OnRead { get; set; }

    public Task<CatalogMetadata?> GetAsync(TitleId titleId, CancellationToken cancellationToken = default)
    {
        var stored = Rows.TryGetValue(titleId, out var found) ? found : null;
        OnRead?.Invoke();
        return Task.FromResult(stored);
    }

    public Task<MetadataWriteResult> TrySaveAsync(
        CatalogMetadata catalog,
        int expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Rows.TryGetValue(catalog.TitleId, out var stored);
        if ((stored?.Revision ?? 0) != expectedRevision)
        {
            return Task.FromResult(new MetadataWriteResult(MetadataWriteOutcome.Conflict, stored));
        }

        Rows[catalog.TitleId] = catalog;
        return Task.FromResult(new MetadataWriteResult(MetadataWriteOutcome.Applied, catalog));
    }
}

internal static class TestIdentification
{
    public static MetadataLanguage Language { get; } = new("es-ES", "en-US");

    /// <summary>
    /// The use case wired the way the composition root wires it. Suites that are not about applying
    /// an identification pass no details, which makes it the no-op an unidentified library gets.
    /// </summary>
    public static ApplyIdentification Apply(
        ICatalogMetadataRepository repository,
        IMetadataProvider provider,
        TimeProvider? timeProvider = null) =>
        new(repository, provider, new MetadataMergePolicy(), Language, timeProvider ?? TimeProvider.System);

    public static ApplyIdentification Silent() =>
        Apply(new MemoryCatalogMetadataRepository(), new StubMetadataProvider());
}
