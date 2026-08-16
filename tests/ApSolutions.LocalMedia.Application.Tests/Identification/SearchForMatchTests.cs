// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Domain.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Identification;

/// <summary>
/// The search a person asks for from the review inbox, in their own words.
/// </summary>
/// <remarks>
/// It behaves like the automatic pass on purpose: the words are read the way a file name is read,
/// what comes back replaces the candidates the file had, and an answer the scorer trusts on its own
/// is applied rather than queued. Somebody who searched and got the obvious answer should not have
/// to confirm it; somebody who got a doubtful one should find it waiting.
/// </remarks>
public sealed class SearchForMatchTests
{
    private static readonly MediaFileId File = new(new Guid("00000000-0000-0000-0000-00000000f11e"));

    [Fact]
    public async Task The_words_are_read_the_way_a_file_name_is_read()
    {
        var source = new SearchCandidateSource(0.55);
        var repository = new SearchCandidateRepository();

        _ = await CreateSearch(source, repository, out _).ExecuteAsync(
            File,
            "  La llegada 2016  ",
            TestContext.Current.CancellationToken);

        Assert.Equal("La llegada", source.LastTitle);
        Assert.Equal(2016, source.LastYear);
        Assert.Empty(source.LastFolders!);
    }

    /// <summary>
    /// A doubtful answer waits for a person: it replaces the candidates and nothing is written to the
    /// catalogue.
    /// </summary>
    [Fact]
    public async Task A_doubtful_answer_replaces_the_candidates_and_applies_nothing()
    {
        var source = new SearchCandidateSource(0.55);
        var repository = new SearchCandidateRepository();
        var search = CreateSearch(source, repository, out var provider);

        var candidates = await search.ExecuteAsync(
            File,
            "La llegada 2016",
            TestContext.Current.CancellationToken);

        var candidate = Assert.Single(candidates);
        Assert.Equal("movie:329865", candidate.StableKey);
        Assert.NotEqual(ReviewState.Automatic, candidate.ReviewState);
        Assert.Equal([candidate], repository.Stored);
        Assert.Empty(provider.Requested);
    }

    /// <summary>
    /// An answer beyond doubt finishes the identification, exactly as the scan's automatic pass does.
    /// </summary>
    [Fact]
    public async Task An_answer_beyond_doubt_is_applied_without_asking()
    {
        var source = new SearchCandidateSource(1.0);
        var repository = new SearchCandidateRepository();
        var search = CreateSearch(source, repository, out var provider);

        var candidates = await search.ExecuteAsync(
            File,
            "La llegada 2016",
            TestContext.Current.CancellationToken);

        Assert.Equal(ReviewState.Automatic, Assert.Single(candidates).ReviewState);
        Assert.Equal("movie:329865", Assert.Single(provider.Requested).Key);
        Assert.Equal(MetadataContentKind.Movie, Assert.Single(provider.Requested).Kind);
    }

    [Fact]
    public async Task A_search_for_nothing_is_refused_before_anything_is_replaced()
    {
        var repository = new SearchCandidateRepository();
        var search = CreateSearch(new SearchCandidateSource(1.0), repository, out _);

        _ = await Assert.ThrowsAsync<ArgumentException>(
            () => search.ExecuteAsync(File, "   ", TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => search.ExecuteAsync(File, null!, TestContext.Current.CancellationToken));
        Assert.Null(repository.Stored);
    }

    [Fact]
    public void A_search_without_its_halves_refuses_to_exist()
    {
        var identify = new IdentifyMediaFile(
            new MediaNameParser(),
            new CandidateScorer(),
            new SearchCandidateSource(1.0),
            new SearchCandidateRepository());

        _ = Assert.Throws<ArgumentNullException>(() => new SearchForMatch(null!, TestIdentification.Silent()));
        _ = Assert.Throws<ArgumentNullException>(() => new SearchForMatch(identify, null!));
    }

    private static SearchForMatch CreateSearch(
        SearchCandidateSource source,
        SearchCandidateRepository repository,
        out StubMetadataProvider provider)
    {
        provider = new StubMetadataProvider();
        return new SearchForMatch(
            new IdentifyMediaFile(new MediaNameParser(), new CandidateScorer(), source, repository),
            TestIdentification.Apply(new MemoryCatalogMetadataRepository(), provider));
    }

    /// <summary>One answer, as close to what was asked as the test wants it to be.</summary>
    private sealed class SearchCandidateSource(double similarity) : IIdentificationCandidateSource
    {
        public string? LastTitle { get; private set; }

        public int? LastYear { get; private set; }

        public IReadOnlyList<string>? LastFolders { get; private set; }

        public Task<IReadOnlyList<CandidateFacts>> GetLocalAsync(
            ParsedMediaName parsed,
            CancellationToken cancellationToken = default)
        {
            LastTitle = parsed.CleanTitle;
            LastYear = parsed.Year;
            LastFolders = [];
            return Task.FromResult<IReadOnlyList<CandidateFacts>>(
            [
                new CandidateFacts(
                    CandidateId.FromStableKey("movie:329865"),
                    "movie:329865",
                    CandidateContentKind.Movie,
                    similarity,
                    SeasonMatch: null,
                    EpisodeMatch: null,
                    YearMatch: similarity,
                    DurationMatch: null),
            ]);
        }

        public Task<IReadOnlyList<CandidateFacts>> GetRemoteAsync(
            ParsedMediaName parsed,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CandidateFacts>>([]);
    }

    private sealed class SearchCandidateRepository : IMatchCandidateRepository
    {
        public IReadOnlyList<MatchCandidate>? Stored { get; private set; }

        public Task ReplaceForMediaFileAsync(
            MediaFileId mediaFileId,
            IReadOnlyList<MatchCandidate> candidates,
            CancellationToken cancellationToken = default)
        {
            Stored = candidates;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MatchCandidate>> GetForMediaFileAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored ?? []);

        public Task<IReadOnlyList<MatchCandidate>> ListForReviewAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored ?? []);

        public Task<MatchDecisionWriteResult> TrySetReviewStateAsync(
            MediaFileId mediaFileId,
            CandidateId candidateId,
            int expectedRevision,
            ReviewState reviewState,
            bool lockDecision,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
