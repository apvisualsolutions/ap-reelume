using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;

namespace ApSolutions.LocalMedia.Application.Identification;

public enum ReviewDecisionOutcome
{
    Applied,
    Conflict,
    NotFound,
}

public sealed record ResolveMatchCommand(
    MediaFileId MediaFileId,
    CandidateId CandidateId,
    int ExpectedRevision);

public sealed record ReviewDecisionResult(
    ReviewDecisionOutcome Outcome,
    MatchCandidate? Candidate);

public sealed class ResolveMatch
{
    private readonly IMatchCandidateRepository _repository;
    private readonly IApplicationEventPublisher _eventPublisher;

    public ResolveMatch(
        IMatchCandidateRepository repository,
        IApplicationEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task<ReviewDecisionResult> ExecuteAsync(
        ResolveMatchCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await ApplyAsync(
            command.MediaFileId,
            command.CandidateId,
            command.ExpectedRevision,
            ReviewState.Accepted,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ReviewDecisionResult> ApplyAsync(
        MediaFileId mediaFileId,
        CandidateId candidateId,
        int expectedRevision,
        ReviewState reviewState,
        CancellationToken cancellationToken)
    {
        var write = await _repository.TrySetReviewStateAsync(
            mediaFileId,
            candidateId,
            expectedRevision,
            reviewState,
            lockDecision: true,
            cancellationToken).ConfigureAwait(false);
        var result = new ReviewDecisionResult(Map(write.Outcome), write.Candidate);
        if (write is { Outcome: MatchDecisionWriteOutcome.Applied, Candidate: not null })
        {
            await _eventPublisher.PublishAsync(
                new ReviewInboxChanged(
                    mediaFileId,
                    candidateId,
                    reviewState,
                    write.Candidate.Revision),
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    internal static ReviewDecisionOutcome Map(MatchDecisionWriteOutcome outcome) => outcome switch
    {
        MatchDecisionWriteOutcome.Applied => ReviewDecisionOutcome.Applied,
        MatchDecisionWriteOutcome.Conflict => ReviewDecisionOutcome.Conflict,
        MatchDecisionWriteOutcome.NotFound => ReviewDecisionOutcome.NotFound,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}
