// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Metadata;

/// <summary>
/// A poster is fetched once, from an address the policy agreed to, or not at all.
/// </summary>
/// <remarks>
/// The three that matter are the three this use case exists to keep out of
/// <c>ApplyIdentification</c>: a title with no poster path, a path that composes into no address,
/// and one already on the disk. Each of them ends in no connection, which is the promise.
/// </remarks>
public sealed class CacheTitleArtworkTests
{
    private static readonly TitleId Title = new(new Guid("77777777-7777-7777-7777-777777777777"));

    [Fact]
    public async Task A_title_with_a_poster_and_nothing_cached_fetches_it_once()
    {
        var store = new RecordingStore();

        var path = await CacheTitleArtwork(store).ExecuteAsync(
            Title,
            "/wXsQvli6tWqja51pYxXNG1LFIGV.jpg",
            "El Faro de Piedra",
            TestContext.Current.CancellationToken);

        Assert.Equal(store.Fetched, path);
        Assert.Equal(1, store.Fetches);
        Assert.Equal(
            "https://image.tmdb.org/t/p/w780/wXsQvli6tWqja51pYxXNG1LFIGV.jpg",
            store.LastSource?.AbsoluteUri);

        // The alternative text is the title, because that is the only description this application
        // has of a picture it did not choose.
        Assert.Equal("El Faro de Piedra", store.LastAlternativeText);
    }

    [Fact]
    public async Task A_poster_already_on_the_disk_is_answered_without_a_connection()
    {
        var store = new RecordingStore { Found = @"C:\cache\artwork\poster.jpg" };

        var path = await CacheTitleArtwork(store).ExecuteAsync(
            Title,
            "/wXsQvli6tWqja51pYxXNG1LFIGV.jpg",
            "El Faro de Piedra",
            TestContext.Current.CancellationToken);

        Assert.Equal(@"C:\cache\artwork\poster.jpg", path);
        Assert.Equal(0, store.Fetches);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/../../etc/passwd.jpg")]
    [InlineData("https://evil.example/x.jpg")]
    public async Task A_path_that_composes_into_no_address_never_reaches_the_store(string? posterPath)
    {
        var store = new RecordingStore();

        var path = await CacheTitleArtwork(store).ExecuteAsync(
            Title,
            posterPath,
            "El Faro de Piedra",
            TestContext.Current.CancellationToken);

        Assert.Null(path);
        Assert.Equal(0, store.Fetches);

        // Not even looked for: a refused path has no address, so there is nothing to look up either.
        Assert.Equal(0, store.Lookups);
    }

    [Fact]
    public async Task A_fetch_that_answers_nothing_is_an_ordinary_state_and_not_a_failure()
    {
        var store = new RecordingStore { FetchAnswersNothing = true };

        var path = await CacheTitleArtwork(store).ExecuteAsync(
            Title,
            "/wXsQvli6tWqja51pYxXNG1LFIGV.jpg",
            "El Faro de Piedra",
            TestContext.Current.CancellationToken);

        Assert.Null(path);
        Assert.Equal(1, store.Fetches);
    }

    [Fact]
    public void The_use_case_needs_somewhere_to_put_what_it_fetches()
    {
        Assert.Throws<ArgumentNullException>(() => new CacheTitleArtwork(null!));
    }

    private static CacheTitleArtwork CacheTitleArtwork(IArtworkStore store) => new(store);

    private sealed class RecordingStore : IArtworkStore
    {
        public string? Found { get; init; }

        public bool FetchAnswersNothing { get; init; }

        public string? Fetched { get; private set; }

        public int Fetches { get; private set; }

        public int Lookups { get; private set; }

        public Uri? LastSource { get; private set; }

        public string? LastAlternativeText { get; private set; }

        public string? Find(TitleId titleId, Uri source)
        {
            Lookups++;
            LastSource = source;
            return Found;
        }

        public Task<string?> FetchAsync(
            TitleId titleId,
            Uri source,
            string alternativeText,
            CancellationToken cancellationToken = default)
        {
            Fetches++;
            LastSource = source;
            LastAlternativeText = alternativeText;
            Fetched = FetchAnswersNothing ? null : @"C:\cache\artwork\fetched.jpg";
            return Task.FromResult(Fetched);
        }

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
}
