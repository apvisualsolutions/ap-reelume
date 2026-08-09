using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Identification;

public interface IMatchCandidateRepository
{
    Task ReplaceForMediaFileAsync(
        MediaFileId mediaFileId,
        IReadOnlyList<MatchCandidate> candidates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MatchCandidate>> GetForMediaFileAsync(
        MediaFileId mediaFileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MatchCandidate>> ListForReviewAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    Task<MatchDecisionWriteResult> TrySetReviewStateAsync(
        MediaFileId mediaFileId,
        CandidateId candidateId,
        int expectedRevision,
        ReviewState reviewState,
        bool lockDecision,
        CancellationToken cancellationToken = default);
}
