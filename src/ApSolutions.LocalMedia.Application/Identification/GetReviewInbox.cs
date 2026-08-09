using ApSolutions.LocalMedia.Domain.Identification;

namespace ApSolutions.LocalMedia.Application.Identification;

public sealed record GetReviewInboxQuery(int PageSize, int Offset = 0);

public sealed record ReviewInboxPage(
    IReadOnlyList<MatchCandidate> Items,
    int? NextOffset);

public sealed class GetReviewInbox
{
    private readonly IMatchCandidateRepository _repository;

    public GetReviewInbox(IMatchCandidateRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ReviewInboxPage> ExecuteAsync(
        GetReviewInboxQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query));
        }

        if (query.Offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query));
        }

        var candidates = await _repository.ListForReviewAsync(
            query.Offset,
            query.PageSize + 1,
            cancellationToken).ConfigureAwait(false);
        var ordered = candidates
            .Where(candidate => candidate.ReviewState is ReviewState.Pending or ReviewState.Suggested)
            .OrderBy(candidate => candidate.ReviewState == ReviewState.Pending ? 0 : 1)
            .ThenBy(candidate => candidate.Score)
            .ThenBy(candidate => candidate.StableKey, StringComparer.Ordinal)
            .ToArray();
        var hasMore = ordered.Length > query.PageSize;
        return new ReviewInboxPage(
            ordered.Take(query.PageSize).ToArray(),
            hasMore ? query.Offset + query.PageSize : null);
    }
}
