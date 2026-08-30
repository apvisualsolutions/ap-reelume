// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Metadata;

/// <summary>
/// Identifying a title puts its poster on this disk, over the whole chain and not link by link.
/// </summary>
/// <remarks>
/// <para>
/// Every link of this chain had a test of its own the day it was written — the address policy, the
/// use case, the cache, the view model, the two views — and none of them ran together. That is
/// exactly the shape this repository's characteristic defect takes: a chain whose every link returns
/// success and whose end does nothing. <c>ApplyIdentificationTests</c> exists for the same reason on
/// the metadata row, and its own summary says so.
/// </para>
/// <para>
/// So this one is deliberately end to end: a real SQLite catalogue, a real <c>ArtworkCache</c> over a
/// real directory, and the address composed by the policy rather than written here — if
/// <c>PosterAddressPolicy</c> ever changed its size segment, the file the cache writes and the file
/// the card looks for would part company, and only a test that lets both sides compute it would
/// notice.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class IdentifiedArtworkTests
{
    private static readonly MetadataLanguage Spanish = new("es-ES", "en-US");
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Identifying_a_title_puts_its_poster_on_the_disk_where_the_card_looks_for_it()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);

        var handler = new ArtworkHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var cache = new ArtworkCache(directory.Path, client);
        var mediaFileId = new MediaFileId(Guid.Parse("70000000-0000-0000-0000-000000000001"));

        var applied = await Apply(factory, cache).ExecuteAsync(
            new ApplyIdentificationCommand(mediaFileId, "movie:6289", MetadataContentKind.Movie),
            TestContext.Current.CancellationToken);

        Assert.Equal(ApplyIdentificationOutcome.Applied, applied.Outcome);

        // The row first, because artwork is decoration over a card that has to say something in words
        // either way — and because a chain that stored nothing would make everything below vacuous.
        var stored = await new CatalogMetadataRepository(factory).GetAsync(
            new TitleId(mediaFileId.Value),
            TestContext.Current.CancellationToken);
        Assert.Equal("/wXsQvli6tWqja51pYxXNG1LFIGV.jpg", stored?.Metadata.PosterPath);

        // One request, to the one declared host, at the address the policy composes.
        Assert.Equal(1, handler.Requests);
        Assert.Equal(
            "https://image.tmdb.org/t/p/w780/wXsQvli6tWqja51pYxXNG1LFIGV.jpg",
            handler.LastAddress?.AbsoluteUri);

        // And the file is where the card will look: the address is composed by the policy on this
        // side too, so a change to the size segment moves both together or fails here.
        var address = PosterAddressPolicy.TryBuildPosterAddress(stored?.Metadata.PosterPath);
        Assert.NotNull(address);
        var onDisk = cache.Find(new TitleId(mediaFileId.Value), new Uri(address!, UriKind.Absolute));
        Assert.NotNull(onDisk);
        Assert.True(File.Exists(onDisk));
        Assert.Contains(Path.Combine("cache", "artwork"), onDisk!, StringComparison.Ordinal);

        // Identifying the same title again does not fetch it twice: the card is opened far more often
        // than a title is identified, and a refresh is an identification.
        _ = await Apply(factory, cache).ExecuteAsync(
            new ApplyIdentificationCommand(mediaFileId, "movie:6289", MetadataContentKind.Movie),
            TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.Requests);
    }

    /// <summary>
    /// A poster that cannot be had leaves the identification intact.
    /// </summary>
    /// <remarks>
    /// The whole reason the fetch answers a path or nothing rather than a result with an outcome: a
    /// title identified with no poster is a title identified, and the row is what a person came for.
    /// </remarks>
    [Fact]
    public async Task A_poster_that_cannot_be_had_does_not_cost_the_identification()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);

        var handler = new ArtworkHandler(HttpStatusCode.ServiceUnavailable);
        using var client = new HttpClient(handler);
        var cache = new ArtworkCache(directory.Path, client);
        var mediaFileId = new MediaFileId(Guid.Parse("70000000-0000-0000-0000-000000000002"));

        var applied = await Apply(factory, cache).ExecuteAsync(
            new ApplyIdentificationCommand(mediaFileId, "movie:6289", MetadataContentKind.Movie),
            TestContext.Current.CancellationToken);

        Assert.Equal(ApplyIdentificationOutcome.Applied, applied.Outcome);
        Assert.Equal(1, handler.Requests);

        var stored = await new CatalogMetadataRepository(factory).GetAsync(
            new TitleId(mediaFileId.Value),
            TestContext.Current.CancellationToken);
        Assert.Equal("La llegada", stored?.Metadata.Title);

        var address = PosterAddressPolicy.TryBuildPosterAddress(stored?.Metadata.PosterPath);
        Assert.Null(cache.Find(new TitleId(mediaFileId.Value), new Uri(address!, UriKind.Absolute)));
    }

    /// <summary>
    /// A provider that sends no poster path reaches the network not at all.
    /// </summary>
    /// <remarks>
    /// This is the ordinary case in a library identified before artwork existed, and the promise is
    /// that opening one costs nothing: no address composes, so nothing is even looked up.
    /// </remarks>
    [Fact]
    public async Task A_title_with_no_poster_path_never_reaches_the_network()
    {
        using var directory = new DatabaseTestDirectory();
        var factory = new SqliteConnectionFactory(directory.DatabasePath);
        await new MigrationRunner(factory).MigrateAsync(TestContext.Current.CancellationToken);

        var handler = new ArtworkHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var cache = new ArtworkCache(directory.Path, client);
        var mediaFileId = new MediaFileId(Guid.Parse("70000000-0000-0000-0000-000000000003"));

        var applied = await Apply(factory, cache, posterPath: null).ExecuteAsync(
            new ApplyIdentificationCommand(mediaFileId, "movie:6289", MetadataContentKind.Movie),
            TestContext.Current.CancellationToken);

        Assert.Equal(ApplyIdentificationOutcome.Applied, applied.Outcome);
        Assert.Equal(0, handler.Requests);
    }

    private static ApplyIdentification Apply(
        SqliteConnectionFactory factory,
        ArtworkCache cache,
        string? posterPath = "/wXsQvli6tWqja51pYxXNG1LFIGV.jpg") => new(
        new CatalogMetadataRepository(factory),
        new PosterProvider(posterPath),
        new MetadataMergePolicy(),
        Spanish,
        new FixedClock(Now),
        new CacheTitleArtwork(cache));

    private sealed class PosterProvider(string? posterPath) : IMetadataProvider
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
            Task.FromResult<MetadataDetails?>(new MetadataDetails(
                reference,
                "La llegada",
                "Arrival",
                "Una lingüista traduce a los visitantes.",
                2016,
                ["Ciencia ficción"],
                posterPath,
                BackdropPath: null,
                TrailerKey: null));
    }

    private sealed class ArtworkHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        public Uri? LastAddress { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            LastAddress = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("a picture", Encoding.UTF8, "image/jpeg"),
            });
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
