// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.UiTests;

/// <summary>
/// The identification step, wired but with nothing to say — which is what the shipped artifact does
/// without a provider token. Surfaces under test here are about the inbox and the shell, so the
/// answer they need from the provider is the same one an offline library gets: none.
/// </summary>
internal static class SilentIdentification
{
    public static ApplyIdentification Create() => new(
        new NoCatalogMetadata(),
        new SilentProvider(),
        new MetadataMergePolicy(),
        new MetadataLanguage("es-ES", "en-US"),
        TimeProvider.System,
        new CacheTitleArtwork(new NoArtwork()));

    /// <summary>Artwork with no disk and no network behind it, which is what silence looks like.</summary>
    private sealed class NoArtwork : IArtworkStore
    {
        public string? Find(TitleId titleId, Uri source) => null;

        public Task<string?> FetchAsync(
            TitleId titleId,
            Uri source,
            string alternativeText,
            CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    /// <summary>The refresh wired as the composition root wires it, over a provider with no answers.</summary>
    public static RefreshMetadata Refresh(ICatalogMetadataRepository repository) => new(
        repository,
        new SilentProvider(),
        new MetadataMergePolicy(),
        new MetadataLanguage("es-ES", "en-US"),
        TimeProvider.System);

    private sealed class SilentProvider : IMetadataProvider
    {
        public string Name => "tmdb";

        public MetadataReference? TryCreateReference(string key) =>
            new(Name, key, MetadataContentKind.Movie);

        public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
            MetadataSearchQuery query,
            MetadataLanguage language,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MetadataSearchResult>>([]);

        public Task<MetadataDetails?> GetDetailsAsync(
            MetadataReference reference,
            MetadataLanguage language,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MetadataDetails?>(null);
    }

    private sealed class NoCatalogMetadata : ICatalogMetadataRepository
    {
        public Task<CatalogMetadata?> GetAsync(TitleId titleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogMetadata?>(null);

        public Task<IReadOnlyList<CatalogMetadata>> ListStaleAsync(
            DateTimeOffset staleBefore,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogMetadata>>([]);

        public Task<MetadataWriteResult> TrySaveAsync(
            CatalogMetadata catalog,
            int expectedRevision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MetadataWriteResult(
                MetadataWriteOutcome.Applied,
                catalog is null ? null : catalog with { Revision = expectedRevision + 1 }));
    }
}
