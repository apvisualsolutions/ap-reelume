// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Identification;

/// <summary>
/// Where the candidates for one file come from, and what the source refuses to do on its own.
/// <para>
/// The local pass answers from what is already cached, so an identified library keeps working with
/// the network unplugged. The remote pass is the only one that can open a connection, and it asks
/// first: a scan that finds a thousand files must never become a thousand requests nobody asked for.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class MetadataCandidateSourceTests
{
    private static readonly MetadataLanguage Spanish = new("es-ES", "en-US");

    [Fact]
    public async Task Without_consent_the_remote_pass_asks_nothing_and_answers_nothing()
    {
        var provider = new CountingProvider();
        var source = new MetadataCandidateSource(provider, new EmptyCache(), Spanish, () => false);

        var candidates = await source.GetRemoteAsync(Movie("Arrival", 2016), TestContext.Current.CancellationToken);

        Assert.Empty(candidates);
        Assert.Equal(0, provider.Searches);
    }

    [Fact]
    public async Task With_consent_the_remote_pass_asks_and_describes_what_came_back()
    {
        var provider = new CountingProvider(Result("movie:1", "Arrival", null, 2016));
        var source = new MetadataCandidateSource(provider, new EmptyCache(), Spanish, () => true);

        var candidates = await source.GetRemoteAsync(Movie("Arrival", 2016), TestContext.Current.CancellationToken);

        var only = Assert.Single(candidates);
        Assert.Equal("movie:1", only.StableKey);
        Assert.Equal(CandidateContentKind.Movie, only.Kind);
        Assert.Equal(1.0, only.TitleSimilarity);
        Assert.Equal(1.0, only.YearMatch);
        Assert.Null(only.SeasonMatch);
        Assert.Null(only.EpisodeMatch);
        Assert.Null(only.DurationMatch);
        Assert.Equal(1, provider.Searches);
    }

    /// <summary>
    /// The local pass is a cache read with a provider in front of it. Nothing cached means nothing
    /// answered, and the provider is never asked, so an offline library degrades instead of stalling.
    /// </summary>
    [Fact]
    public async Task The_local_pass_answers_nothing_when_nothing_was_ever_cached()
    {
        var provider = new CountingProvider(Result("movie:1", "Arrival", null, 2016));
        var source = new MetadataCandidateSource(provider, new EmptyCache(), Spanish, () => true);

        var candidates = await source.GetLocalAsync(Movie("Arrival", 2016), TestContext.Current.CancellationToken);

        Assert.Empty(candidates);
        Assert.Equal(0, provider.Searches);
    }

    [Fact]
    public async Task The_local_pass_answers_from_the_cache_without_any_consent_at_all()
    {
        var provider = new CountingProvider(Result("movie:1", "Arrival", null, 2016));
        var cache = new SeededCache("tmdb", "search:movie:arrival:2016", "es-ES", 3);
        var source = new MetadataCandidateSource(provider, cache, Spanish, () => false);

        var candidates = await source.GetLocalAsync(Movie("Arrival", 2016), TestContext.Current.CancellationToken);

        Assert.Single(candidates);
        Assert.Equal(1, provider.Searches);
    }

    [Fact]
    public async Task A_series_is_asked_for_as_a_series_and_scores_its_season_and_episode()
    {
        var provider = new CountingProvider(Result("tv:7", "Crónicas", null, 2011));
        var source = new MetadataCandidateSource(provider, new EmptyCache(), Spanish, () => true);

        var candidates = await source.GetRemoteAsync(
            new ParsedMediaName(ParsedMediaKind.Episode, "Crónicas", 2011, 1, 2, null, false, [], []),
            TestContext.Current.CancellationToken);

        var only = Assert.Single(candidates);
        Assert.Equal(MetadataContentKind.Show, provider.LastQuery!.Kind);
        Assert.Equal(CandidateContentKind.Episode, only.Kind);
        Assert.Equal(1.0, only.SeasonMatch);
        Assert.Equal(1.0, only.EpisodeMatch);
    }

    [Fact]
    public async Task A_name_with_nothing_left_after_parsing_is_never_looked_up()
    {
        var provider = new CountingProvider();
        var source = new MetadataCandidateSource(provider, new EmptyCache(), Spanish, () => true);
        var empty = new ParsedMediaName(ParsedMediaKind.Unknown, "   ", null, null, null, null, false, [], []);

        Assert.Empty(await source.GetLocalAsync(empty, TestContext.Current.CancellationToken));
        Assert.Empty(await source.GetRemoteAsync(empty, TestContext.Current.CancellationToken));
        Assert.Equal(0, provider.Searches);
    }

    /// <summary>
    /// A title the provider answered in another language still scores on its original, and a title
    /// that shares nothing but a few letters scores below one rather than being called a match.
    /// </summary>
    [Fact]
    public async Task Similarity_uses_the_original_title_and_never_claims_more_than_it_can_see()
    {
        var provider = new CountingProvider(
            Result("movie:1", "La llegada", "Arrival", 2016),
            Result("movie:2", "Otra cosa", null, 1999));
        var source = new MetadataCandidateSource(provider, new EmptyCache(), Spanish, () => true);

        var candidates = await source.GetRemoteAsync(Movie("Arrival", 2016), TestContext.Current.CancellationToken);

        Assert.Equal(1.0, candidates[0].TitleSimilarity);
        Assert.InRange(candidates[1].TitleSimilarity, 0.0, 0.6);
        Assert.InRange(candidates[1].YearMatch!.Value, 0.0, 0.999);
    }

    [Fact]
    public async Task A_year_nobody_knows_produces_no_year_signal_at_all()
    {
        var provider = new CountingProvider(Result("movie:1", "Arrival", null, null));
        var source = new MetadataCandidateSource(provider, new EmptyCache(), Spanish, () => true);

        var candidates = await source.GetRemoteAsync(Movie("Arrival", null), TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(candidates).YearMatch);
    }

    [Fact]
    public void A_source_without_a_provider_a_cache_or_a_language_refuses_to_exist()
    {
        _ = Assert.Throws<ArgumentNullException>(
            () => new MetadataCandidateSource(null!, new EmptyCache(), Spanish));
        _ = Assert.Throws<ArgumentNullException>(
            () => new MetadataCandidateSource(new CountingProvider(), null!, Spanish));
        _ = Assert.Throws<ArgumentNullException>(
            () => new MetadataCandidateSource(new CountingProvider(), new EmptyCache(), null!));
    }

    [Fact]
    public async Task A_source_with_no_consent_rule_at_all_stays_offline()
    {
        var provider = new CountingProvider(Result("movie:1", "Arrival", null, 2016));
        var source = new MetadataCandidateSource(provider, new EmptyCache(), Spanish);

        Assert.Empty(await source.GetRemoteAsync(Movie("Arrival", 2016), TestContext.Current.CancellationToken));
        Assert.Equal(0, provider.Searches);
    }

    [Fact]
    public async Task Nothing_is_parsed_from_nothing()
    {
        var source = new MetadataCandidateSource(new CountingProvider(), new EmptyCache(), Spanish);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => source.GetLocalAsync(null!, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => source.GetRemoteAsync(null!, TestContext.Current.CancellationToken));
    }

    private static ParsedMediaName Movie(string title, int? year) =>
        new(ParsedMediaKind.Movie, title, year, null, null, null, false, [], []);

    private static MetadataSearchResult Result(string key, string title, string? originalTitle, int? year) =>
        new(
            new MetadataReference(
                "tmdb",
                key,
                key.StartsWith("tv:", StringComparison.Ordinal)
                    ? MetadataContentKind.Show
                    : MetadataContentKind.Movie),
            "es-ES",
            title,
            originalTitle,
            year);

    private sealed class CountingProvider(params MetadataSearchResult[] results) : IMetadataProvider
    {
        public int Searches { get; private set; }

        public MetadataSearchQuery? LastQuery { get; private set; }

        public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
            MetadataSearchQuery query,
            MetadataLanguage language,
            CancellationToken cancellationToken = default)
        {
            Searches++;
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<MetadataSearchResult>>(results);
        }

        public Task<MetadataDetails?> GetDetailsAsync(
            MetadataReference reference,
            MetadataLanguage language,
            CancellationToken cancellationToken = default) => Task.FromResult<MetadataDetails?>(null);
    }

    private sealed class EmptyCache : IMetadataCache
    {
        public Task<MetadataCacheEntry?> GetAsync(
            MetadataCacheKey key,
            CancellationToken cancellationToken = default) => Task.FromResult<MetadataCacheEntry?>(null);

        public Task StoreAsync(MetadataCacheEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(MetadataCacheKey key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class SeededCache(string provider, string key, string language, int version) : IMetadataCache
    {
        public Task<MetadataCacheEntry?> GetAsync(
            MetadataCacheKey requested,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                requested == new MetadataCacheKey(provider, key, language, version)
                    ? new MetadataCacheEntry(
                        requested,
                        "{}",
                        null,
                        DateTimeOffset.UnixEpoch,
                        DateTimeOffset.UnixEpoch.AddYears(50))
                    : null);

        public Task StoreAsync(MetadataCacheEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(MetadataCacheKey requested, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
