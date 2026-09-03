// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Identification;

/// <summary>
/// The end-to-end walk LIB-006/007 demand: real files scanned into a real SQLite catalogue, handed
/// to real identification, stored as real candidates, and read back by the review inbox. Before
/// WP-2 nothing joined the scan to identification, so this whole chain existed only piecewise.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ScanIdentificationTests
{
    private static readonly MetadataLanguage Spanish = new("es-ES", "en-US");

    [Fact]
    public async Task A_scanned_file_is_identified_end_to_end_and_the_inbox_has_something_to_review()
    {
        using var directory = new DatabaseTestDirectory();
        var mediaPath = Path.Combine(directory.Path, "media", "Movies");
        Directory.CreateDirectory(mediaPath);
        await File.WriteAllBytesAsync(
            Path.Combine(mediaPath, "Dune.2021.mkv"),
            [0x41, 0x50],
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(mediaPath, "Arrival.2016.mkv"),
            [0x41, 0x50],
            TestContext.Current.CancellationToken);

        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);
        var rootRepository = new LibraryRootRepository(factory);
        var root = new LibraryRoot(
            new LibraryRootId(Guid.NewGuid()),
            Path.Combine(directory.Path, "media"),
            RootKind.Local,
            RootAvailability.Available,
            ScanPolicy.Manual);
        await rootRepository.AddAsync(root, TestContext.Current.CancellationToken);
        var mediaFiles = new MediaFileRepository(factory);
        var candidates = new MatchCandidateRepository(factory);

        // The provider is the only stub: it answers "Dune" exactly and answers "Arrival" with a
        // title that shares almost nothing, so one file identifies automatically and the other
        // lands in front of a person — the two halves of the promise.
        var identify = new IdentifyMediaFile(
            new MediaNameParser(),
            new CandidateScorer(),
            new MetadataCandidateSource(
                new TitleKeyedProvider(),
                new SqliteMetadataCache(factory),
                Spanish,
                () => true),
            candidates);
        var catalogMetadata = new CatalogMetadataRepository(factory);
        var useCase = new IdentifyScannedFiles(
            rootRepository,
            mediaFiles,
            candidates,
            identify,
            new ApplyIdentification(
                catalogMetadata,
                new TitleKeyedProvider(),
                new MetadataMergePolicy(),
                Spanish,
                TimeProvider.System,
                new CacheTitleArtwork(new NoArtwork())));
        var coordinator = new ScanCoordinator(
            rootRepository,
            mediaFiles,
            new MediaFileEnumerator(),
            new StubProbe(),
            new InProcessApplicationEventPublisher());

        var summary = await coordinator.StartAsync(
            new StartScanCommand(root.Id, ScanTrigger.Initial, 16),
            TestContext.Current.CancellationToken);
        var result = await useCase.ExecuteAsync(summary, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.AttemptedCount);
        Assert.Equal(2, result.IdentifiedCount);
        Assert.Equal(0, result.FailedCount);

        var dune = await mediaFiles.FindByPathAsync(
            root.Id,
            Path.Combine(mediaPath, "Dune.2021.mkv"),
            TestContext.Current.CancellationToken);
        Assert.NotNull(dune);
        var duneCandidate = Assert.Single(
            await candidates.GetForMediaFileAsync(dune.Id, TestContext.Current.CancellationToken));
        Assert.Equal("movie:dune", duneCandidate.StableKey);
        Assert.Equal(ReviewState.Automatic, duneCandidate.ReviewState);

        // The half nobody was measuring: what the confident match knows has to be in the row the
        // catalogue reads, under the media file's own id, or the identification changed nothing a
        // person can see.
        var stored = await catalogMetadata.GetAsync(
            new TitleId(dune.Id.Value),
            TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal("Dune", stored.Metadata.Title);
        Assert.Equal("Paul Atreides llega a Arrakis.", stored.Metadata.Overview);
        Assert.Equal(2021, stored.Metadata.ReleaseYear);
        Assert.Equal(["Ciencia ficción"], stored.Metadata.Genres);
        Assert.Equal("tmdb", stored.Provider);
        Assert.Equal("movie:dune", stored.ProviderKey);
        Assert.NotNull(stored.RefreshedUtc);

        var arrival = await mediaFiles.FindByPathAsync(
            root.Id,
            Path.Combine(mediaPath, "Arrival.2016.mkv"),
            TestContext.Current.CancellationToken);
        Assert.NotNull(arrival);
        Assert.Null(await catalogMetadata.GetAsync(
            new TitleId(arrival.Id.Value),
            TestContext.Current.CancellationToken));

        var inbox = await new GetReviewInbox(candidates).ExecuteAsync(
            new GetReviewInboxQuery(PageSize: 10),
            TestContext.Current.CancellationToken);
        var reviewable = Assert.Single(inbox.Items);
        Assert.Equal("movie:unrelated", reviewable.StableKey);

        // Running identification again replaces nothing: what a person may already have decided on
        // has to survive every later scan.
        var again = await useCase.ExecuteAsync(summary, TestContext.Current.CancellationToken);
        Assert.Equal(new IdentifyScannedFilesResult(0, 0, 0), again);
    }

    /// <summary>
    /// Artwork with no disk and no network behind it. What this suite measures is the scan reaching
    /// the metadata row, and a poster fetched in the middle of it would be a second subject.
    /// </summary>
    private sealed class NoArtwork : IArtworkStore
    {
        public string? Find(TitleId titleId, Uri source) => null;

        public Task<string?> FetchAsync(
            TitleId titleId,
            Uri source,
            string alternativeText,
            CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        /// <summary>
        /// Refuses rather than pretending: this double exists for the remote path, and a personal
        /// import that quietly answered a made-up path would let a test pass over a call it never
        /// meant to make.
        /// </summary>
        public Task<ArtworkReference> ImportPersonalAsync(
            TitleId titleId,
            string sourcePath,
            string alternativeText,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This double covers the remote path only.");
    }

    private sealed class TitleKeyedProvider : IMetadataProvider
    {
        public string Name => "tmdb";

        public MetadataReference? TryCreateReference(string key) =>
            new("tmdb", key, MetadataContentKind.Movie);

        public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
            MetadataSearchQuery query,
            MetadataLanguage language,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MetadataSearchResult> results =
                query.Title.Contains("dune", StringComparison.OrdinalIgnoreCase)
                    ?
                    [
                        new MetadataSearchResult(
                            new MetadataReference("tmdb", "movie:dune", MetadataContentKind.Movie),
                            "Dune",
                            "Dune",
                            2021),
                    ]
                    :
                    [
                        new MetadataSearchResult(
                            new MetadataReference("tmdb", "movie:unrelated", MetadataContentKind.Movie),
                            "Zzz Otra Cosa",
                            null,
                            2016),
                    ];
            return Task.FromResult(results);
        }

        public Task<MetadataDetails?> GetDetailsAsync(
            MetadataReference reference,
            MetadataLanguage language,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reference);
            return Task.FromResult(reference.Key == "movie:dune"
                ? new MetadataDetails(
                    reference,
                    "Dune",
                    "Dune",
                    "Paul Atreides llega a Arrakis.",
                    2021,
                    ["Ciencia ficción"],
                    "/dune-poster.jpg",
                    "/dune-backdrop.jpg",
                    TrailerKey: null)
                : null);
        }
    }

    private sealed class StubProbe : IMediaProbe
    {
        public Task<TechnicalMetadata> ProbeAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            _ = path;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new TechnicalMetadata(
                TimeSpan.FromMinutes(90),
                "mkv",
                ["h264"],
                ["aac"],
                1920,
                1080));
        }
    }
}
