// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Domain.Metadata;

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
    private readonly ApplyIdentification _applyIdentification;

    public ResolveMatch(
        IMatchCandidateRepository repository,
        IApplicationEventPublisher eventPublisher,
        ApplyIdentification applyIdentification)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _applyIdentification = applyIdentification ?? throw new ArgumentNullException(nameof(applyIdentification));
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
            // Accepting is what the person meant, so it is what the catalogue must show. A provider
            // failure is not swallowed here: this is an explicit action, and somebody who pressed
            // accept is owed the error rather than a row that silently stayed as the parser left it.
            // The decision above is already stored, so a later refresh finishes what this could not.
            await _applyIdentification.ExecuteAsync(
                new ApplyIdentificationCommand(
                    mediaFileId,
                    write.Candidate.StableKey,
                    ToMetadataKind(write.Candidate.Kind)),
                cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// A candidate's kind in the provider's terms. An episode belongs to a show, which is the entry
    /// the provider holds details for.
    /// </summary>
    internal static MetadataContentKind ToMetadataKind(CandidateContentKind kind) =>
        kind == CandidateContentKind.Movie ? MetadataContentKind.Movie : MetadataContentKind.Show;

    internal static ReviewDecisionOutcome Map(MatchDecisionWriteOutcome outcome) => outcome switch
    {
        MatchDecisionWriteOutcome.Applied => ReviewDecisionOutcome.Applied,
        MatchDecisionWriteOutcome.Conflict => ReviewDecisionOutcome.Conflict,
        MatchDecisionWriteOutcome.NotFound => ReviewDecisionOutcome.NotFound,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}
