// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Domain.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Identification;

/// <summary>
/// What an identification is for: the catalogue showing what the provider knows. Every test here
/// asserts on the stored row rather than on a return value, because the defect it covers was a chain
/// whose every link returned success and whose last link wrote nothing.
/// </summary>
public sealed class ApplyIdentificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Accepting_a_candidate_writes_the_identified_metadata_to_the_catalogue()
    {
        var candidate = Candidate("movie:6289", 0.75, ReviewState.Suggested);
        var catalog = new MemoryCatalogMetadataRepository();
        var provider = new StubMetadataProvider(Details("movie:6289"));
        var resolver = new ResolveMatch(
            new ReviewRepository([candidate]),
            new SilentPublisher(),
            TestIdentification.Apply(catalog, provider, new FixedTime(Now)));

        var decision = await resolver.ExecuteAsync(
            new ResolveMatchCommand(candidate.MediaFileId, candidate.Id, ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        Assert.Equal(ReviewDecisionOutcome.Applied, decision.Outcome);
        var stored = Assert.Single(catalog.Rows.Values);
        Assert.Equal("La llegada", stored.Metadata.Title);
        Assert.Equal("Una lingüista traduce a los visitantes.", stored.Metadata.Overview);
        Assert.Equal(2016, stored.Metadata.ReleaseYear);
        Assert.Equal("tmdb", stored.Provider);
        Assert.Equal("movie:6289", stored.ProviderKey);
        Assert.Equal(Now, stored.RefreshedUtc);
    }

    /// <summary>
    /// The title the accepted candidate points at, not whichever one the file happens to sit beside:
    /// the row is keyed by the media file the candidate belongs to.
    /// </summary>
    [Fact]
    public async Task The_row_it_writes_belongs_to_the_media_file_the_candidate_identifies()
    {
        var candidate = Candidate("movie:6289", 0.75, ReviewState.Suggested);
        var catalog = new MemoryCatalogMetadataRepository();
        var resolver = new ResolveMatch(
            new ReviewRepository([candidate]),
            new SilentPublisher(),
            TestIdentification.Apply(catalog, new StubMetadataProvider(Details("movie:6289"))));

        _ = await resolver.ExecuteAsync(
            new ResolveMatchCommand(candidate.MediaFileId, candidate.Id, ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        Assert.Equal([new TitleId(candidate.MediaFileId.Value)], catalog.Rows.Keys);
    }

    /// <summary>
    /// The whole point of the merge: an identification is not allowed to undo somebody's correction.
    /// </summary>
    [Fact]
    public async Task What_a_person_locked_survives_the_identification()
    {
        var candidate = Candidate("movie:6289", 0.75, ReviewState.Suggested);
        var titleId = new TitleId(candidate.MediaFileId.Value);
        var catalog = new MemoryCatalogMetadataRepository
        {
            Rows =
            {
                [titleId] = new CatalogMetadata(
                    titleId,
                    new EditableMetadata(
                        "Mi título",
                        OriginalTitle: null,
                        Overview: null,
                        ReleaseYear: null,
                        Genres: [],
                        PosterPath: null,
                        BackdropPath: null,
                        TrailerKey: null,
                        LockedFields: new HashSet<MetadataField> { MetadataField.Title }),
                    Revision: 3),
            },
        };
        var resolver = new ResolveMatch(
            new ReviewRepository([candidate]),
            new SilentPublisher(),
            TestIdentification.Apply(catalog, new StubMetadataProvider(Details("movie:6289"))));

        _ = await resolver.ExecuteAsync(
            new ResolveMatchCommand(candidate.MediaFileId, candidate.Id, ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        var stored = catalog.Rows[titleId];
        Assert.Equal("Mi título", stored.Metadata.Title);
        Assert.Equal("Una lingüista traduce a los visitantes.", stored.Metadata.Overview);
        Assert.Equal(4, stored.Revision);
    }

    /// <summary>
    /// The shipped default. Without a token the provider serves only its cache, so a library nobody
    /// has looked up stays exactly as the file name parser left it — and that is not a failure.
    /// </summary>
    [Fact]
    public async Task An_answer_the_provider_does_not_have_leaves_the_catalogue_untouched()
    {
        var candidate = Candidate("movie:6289", 0.75, ReviewState.Suggested);
        var catalog = new MemoryCatalogMetadataRepository();
        var resolver = new ResolveMatch(
            new ReviewRepository([candidate]),
            new SilentPublisher(),
            TestIdentification.Apply(catalog, new StubMetadataProvider()));

        var decision = await resolver.ExecuteAsync(
            new ResolveMatchCommand(candidate.MediaFileId, candidate.Id, ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        Assert.Equal(ReviewDecisionOutcome.Applied, decision.Outcome);
        Assert.Empty(catalog.Rows);
    }

    /// <summary>
    /// A rejection is a person saying "not this one", so it must not write the metadata an
    /// acceptance writes.
    /// </summary>
    [Fact]
    public async Task Rejecting_a_candidate_writes_nothing_to_the_catalogue()
    {
        var candidate = Candidate("movie:6289", 0.40, ReviewState.Pending);
        var catalog = new MemoryCatalogMetadataRepository();
        var rejecter = new RejectMatch(new ReviewRepository([candidate]), new SilentPublisher());

        var decision = await rejecter.ExecuteAsync(
            new RejectMatchCommand(candidate.MediaFileId, candidate.Id, ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        Assert.Equal(ReviewDecisionOutcome.Applied, decision.Outcome);
        Assert.Empty(catalog.Rows);
    }

    /// <summary>
    /// An episode is looked up as the show it belongs to, which is the entry the provider holds
    /// details for. Asking for it as a film would be asking for an entry that does not exist.
    /// </summary>
    [Fact]
    public async Task An_episode_is_looked_up_as_the_show_it_belongs_to()
    {
        var candidate = Candidate("tv:1396", 0.75, ReviewState.Suggested) with
        {
            Kind = CandidateContentKind.Episode,
        };
        var provider = new StubMetadataProvider();
        var resolver = new ResolveMatch(
            new ReviewRepository([candidate]),
            new SilentPublisher(),
            TestIdentification.Apply(new MemoryCatalogMetadataRepository(), provider));

        _ = await resolver.ExecuteAsync(
            new ResolveMatchCommand(candidate.MediaFileId, candidate.Id, ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        var requested = Assert.Single(provider.Requested);
        Assert.Equal(MetadataContentKind.Show, requested.Kind);
        Assert.Equal("tv:1396", requested.Key);
        Assert.Equal("tmdb", requested.Provider);
    }

    /// <summary>
    /// Somebody editing the same title while the identification runs keeps their revision: the
    /// write is refused rather than applied on top, and the outcome says which of the two happened.
    /// </summary>
    [Fact]
    public async Task An_edit_that_landed_first_is_not_overwritten()
    {
        var candidate = Candidate("movie:6289", 0.75, ReviewState.Suggested);
        var titleId = new TitleId(candidate.MediaFileId.Value);
        var catalog = new MemoryCatalogMetadataRepository();
        var apply = TestIdentification.Apply(catalog, new StubMetadataProvider(Details("movie:6289")));

        // The row moves on between the read and the write, which is the race the revision exists for.
        catalog.OnRead = () => catalog.Rows[titleId] = new CatalogMetadata(
            titleId,
            new EditableMetadata(
                "Escrito por otra ventana",
                null,
                null,
                null,
                [],
                null,
                null,
                null,
                new HashSet<MetadataField>()),
            Revision: 9);

        var result = await apply.ExecuteAsync(
            new ApplyIdentificationCommand(candidate.MediaFileId, candidate.StableKey, MetadataContentKind.Movie),
            TestContext.Current.CancellationToken);

        Assert.Equal(ApplyIdentificationOutcome.Conflict, result.Outcome);
        Assert.Equal("Escrito por otra ventana", catalog.Rows[titleId].Metadata.Title);
        Assert.Equal(9, catalog.Rows[titleId].Revision);
    }

    /// <summary>A reference with no key cannot identify anything, and says so where it happens.</summary>
    [Fact]
    public async Task A_command_without_a_provider_key_is_refused()
    {
        var apply = TestIdentification.Apply(
            new MemoryCatalogMetadataRepository(),
            new StubMetadataProvider());

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            apply.ExecuteAsync(null!, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentException>(() =>
            apply.ExecuteAsync(
                new ApplyIdentificationCommand(
                    new MediaFileId(Guid.Empty),
                    "  ",
                    MetadataContentKind.Movie),
                TestContext.Current.CancellationToken));
    }

    private static MetadataDetails Details(string key) => new(
        new MetadataReference("tmdb", key, MetadataContentKind.Movie),
        "es-ES",
        "La llegada",
        "Arrival",
        "Una lingüista traduce a los visitantes.",
        2016,
        ["Ciencia ficción"],
        "/poster.jpg",
        "/backdrop.jpg",
        TrailerKey: null);

    private static MatchCandidate Candidate(string stableKey, double score, ReviewState state)
    {
        var mediaFileId = new MediaFileId(CandidateId.FromStableKey($"file:{stableKey}").Value);
        return new MatchCandidate(
            CandidateId.FromStableKey(stableKey),
            mediaFileId,
            stableKey,
            CandidateContentKind.Movie,
            score,
            CandidateScorer.ScoringModelVersion,
            state,
            [new MatchSignal("Identification.Signal.Title", score, 0.5)],
            ["Identification.Signal.Title"]);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ReviewRepository(IEnumerable<MatchCandidate> candidates) : IMatchCandidateRepository
    {
        public List<MatchCandidate> Candidates { get; } = [.. candidates];

        public Task ReplaceForMediaFileAsync(
            MediaFileId mediaFileId,
            IReadOnlyList<MatchCandidate> candidates,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<MatchCandidate>> GetForMediaFileAsync(
            MediaFileId mediaFileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MatchCandidate>>(
                Candidates.Where(candidate => candidate.MediaFileId == mediaFileId).ToArray());

        public Task<IReadOnlyList<MatchCandidate>> ListForReviewAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MatchCandidate>>([.. Candidates.Skip(offset).Take(limit)]);

        public Task<MatchDecisionWriteResult> TrySetReviewStateAsync(
            MediaFileId mediaFileId,
            CandidateId candidateId,
            int expectedRevision,
            ReviewState reviewState,
            bool lockDecision,
            CancellationToken cancellationToken = default)
        {
            var index = Candidates.FindIndex(candidate =>
                candidate.MediaFileId == mediaFileId && candidate.Id == candidateId);
            if (index < 0)
            {
                return Task.FromResult(new MatchDecisionWriteResult(MatchDecisionWriteOutcome.NotFound, null));
            }

            var existing = Candidates[index];
            if (existing.Revision != expectedRevision || existing.IsDecisionLocked)
            {
                return Task.FromResult(new MatchDecisionWriteResult(MatchDecisionWriteOutcome.Conflict, existing));
            }

            var updated = existing with
            {
                ReviewState = reviewState,
                Revision = existing.Revision + 1,
                IsDecisionLocked = lockDecision,
            };
            Candidates[index] = updated;
            return Task.FromResult(new MatchDecisionWriteResult(MatchDecisionWriteOutcome.Applied, updated));
        }
    }

    private sealed class SilentPublisher : IApplicationEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
            where TEvent : notnull => Task.CompletedTask;
    }
}
