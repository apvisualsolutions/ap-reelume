using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Identification;

public sealed class ReviewWorkflowTests
{
    [Fact]
    public async Task Inbox_contains_only_uncertain_candidates_in_priority_order_with_pagination()
    {
        var repository = new ReviewRepository([
            Candidate("automatic", 0.90, ReviewState.Automatic),
            Candidate("suggested-high", 0.8999, ReviewState.Suggested),
            Candidate("pending", 0.5999, ReviewState.Pending),
            Candidate("suggested-low", 0.60, ReviewState.Suggested),
        ]);
        var query = new GetReviewInbox(repository);

        var first = await query.ExecuteAsync(
            new GetReviewInboxQuery(PageSize: 2, Offset: 0),
            TestContext.Current.CancellationToken);
        var second = await query.ExecuteAsync(
            new GetReviewInboxQuery(PageSize: 2, Offset: first.NextOffset!.Value),
            TestContext.Current.CancellationToken);

        Assert.Equal(["pending", "suggested-low"], first.Items.Select(item => item.StableKey));
        Assert.Equal(2, first.NextOffset);
        Assert.Equal(["suggested-high"], second.Items.Select(item => item.StableKey));
        Assert.Null(second.NextOffset);
        Assert.DoesNotContain(
            first.Items.Concat(second.Items),
            candidate => candidate.ReviewState == ReviewState.Automatic);
    }

    [Fact]
    public async Task Resolve_and_reject_lock_the_decision_and_publish_a_typed_event()
    {
        var accepted = Candidate("accept", 0.75, ReviewState.Suggested);
        var rejected = Candidate("reject", 0.40, ReviewState.Pending);
        var repository = new ReviewRepository([accepted, rejected]);
        var publisher = new RecordingPublisher();

        var resolved = await new ResolveMatch(repository, publisher).ExecuteAsync(
            new ResolveMatchCommand(accepted.MediaFileId, accepted.Id, ExpectedRevision: 0),
            TestContext.Current.CancellationToken);
        var rejectedResult = await new RejectMatch(repository, publisher).ExecuteAsync(
            new RejectMatchCommand(rejected.MediaFileId, rejected.Id, ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        Assert.Equal(ReviewDecisionOutcome.Applied, resolved.Outcome);
        Assert.Equal(ReviewState.Accepted, resolved.Candidate?.ReviewState);
        Assert.True(resolved.Candidate?.IsDecisionLocked);
        Assert.Equal(ReviewDecisionOutcome.Applied, rejectedResult.Outcome);
        Assert.Equal(ReviewState.Rejected, rejectedResult.Candidate?.ReviewState);
        Assert.True(rejectedResult.Candidate?.IsDecisionLocked);
        Assert.Equal([ReviewState.Accepted, ReviewState.Rejected], publisher.Events.Select(item => item.ReviewState));
    }

    [Fact]
    public async Task Concurrent_review_reports_conflict_without_losing_the_existing_choice()
    {
        var candidate = Candidate("concurrent", 0.75, ReviewState.Suggested);
        var repository = new ReviewRepository([candidate]);
        var publisher = new RecordingPublisher();
        var resolver = new ResolveMatch(repository, publisher);

        var first = await resolver.ExecuteAsync(
            new ResolveMatchCommand(candidate.MediaFileId, candidate.Id, ExpectedRevision: 0),
            TestContext.Current.CancellationToken);
        var stale = await new RejectMatch(repository, publisher).ExecuteAsync(
            new RejectMatchCommand(candidate.MediaFileId, candidate.Id, ExpectedRevision: 0),
            TestContext.Current.CancellationToken);

        Assert.Equal(ReviewDecisionOutcome.Applied, first.Outcome);
        Assert.Equal(ReviewDecisionOutcome.Conflict, stale.Outcome);
        var stored = Assert.Single(repository.Candidates);
        Assert.Equal(ReviewState.Accepted, stored.ReviewState);
        Assert.True(stored.IsDecisionLocked);
        Assert.Single(publisher.Events);
    }

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
            CancellationToken cancellationToken = default)
        {
            var page = Candidates
                .Where(candidate => candidate.ReviewState is ReviewState.Pending or ReviewState.Suggested)
                .OrderBy(candidate => candidate.ReviewState == ReviewState.Pending ? 0 : 1)
                .ThenBy(candidate => candidate.Score)
                .ThenBy(candidate => candidate.StableKey, StringComparer.Ordinal)
                .Skip(offset)
                .Take(limit)
                .ToArray();
            return Task.FromResult<IReadOnlyList<MatchCandidate>>(page);
        }

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

    private sealed class RecordingPublisher : IApplicationEventPublisher
    {
        public List<ReviewInboxChanged> Events { get; } = [];

        public Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
            where TEvent : notnull
        {
            if (applicationEvent is ReviewInboxChanged changed)
            {
                Events.Add(changed);
            }

            return Task.CompletedTask;
        }
    }
}
