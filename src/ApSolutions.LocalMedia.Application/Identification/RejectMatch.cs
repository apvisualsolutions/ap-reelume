// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;

namespace ApSolutions.LocalMedia.Application.Identification;

public sealed record RejectMatchCommand(
    MediaFileId MediaFileId,
    CandidateId CandidateId,
    int ExpectedRevision);

public sealed class RejectMatch
{
    private readonly IMatchCandidateRepository _repository;
    private readonly IApplicationEventPublisher _eventPublisher;

    public RejectMatch(
        IMatchCandidateRepository repository,
        IApplicationEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task<ReviewDecisionResult> ExecuteAsync(
        RejectMatchCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var write = await _repository.TrySetReviewStateAsync(
            command.MediaFileId,
            command.CandidateId,
            command.ExpectedRevision,
            ReviewState.Rejected,
            lockDecision: true,
            cancellationToken).ConfigureAwait(false);
        var result = new ReviewDecisionResult(ResolveMatch.Map(write.Outcome), write.Candidate);
        if (write is { Outcome: MatchDecisionWriteOutcome.Applied, Candidate: not null })
        {
            await _eventPublisher.PublishAsync(
                new ReviewInboxChanged(
                    command.MediaFileId,
                    command.CandidateId,
                    ReviewState.Rejected,
                    write.Candidate.Revision),
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
