using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;

namespace ApSolutions.LocalMedia.Application.Personalization;

/// <summary>
/// Records what a person marks. Every operation reads the stored row first, so two marks made from
/// different surfaces never overwrite each other's other facts, and a row with nothing left marked is
/// removed rather than kept as an empty one.
/// </summary>
public sealed class SetPersonalState
{
    private readonly IPersonalStateRepository _repository;
    private readonly IClock _clock;

    public SetPersonalState(IPersonalStateRepository repository, IClock clock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<PersonalState> SetFavoriteAsync(
        ContentKey content,
        bool isFavorite,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(content, state => state.WithFavorite(isFavorite), cancellationToken);

    public Task<PersonalState> SetWatchLaterAsync(
        ContentKey content,
        bool isWatchLater,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(content, state => state.WithWatchLater(isWatchLater), cancellationToken);

    /// <summary>Records a rating from one to ten, or clears it with null.</summary>
    public Task<PersonalState> SetRatingAsync(
        ContentKey content,
        int? rating,
        CancellationToken cancellationToken = default)
    {
        if (!PersonalStatePolicy.IsValidRating(rating))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rating),
                rating,
                $"A rating must be between {PersonalStatePolicy.MinimumRating} and {PersonalStatePolicy.MaximumRating}, or absent.");
        }

        return ApplyAsync(content, state => state.WithRating(rating), cancellationToken);
    }

    public Task<PersonalState> ToggleFavoriteAsync(
        ContentKey content,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(content, state => state.ToggleFavorite(), cancellationToken);

    public Task<PersonalState> ToggleWatchLaterAsync(
        ContentKey content,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(content, state => state.ToggleWatchLater(), cancellationToken);

    private async Task<PersonalState> ApplyAsync(
        ContentKey content,
        Func<PersonalState, PersonalState> change,
        CancellationToken cancellationToken)
    {
        // The stored row is re-read inside every write rather than cached, so a mark made elsewhere is
        // never silently reverted. This is the same rule progress follows.
        var stored = await _repository.GetAsync(content, cancellationToken).ConfigureAwait(false);
        var next = change(stored ?? PersonalState.Empty(content));
        if (next.IsEmpty)
        {
            await _repository.DeleteAsync(content, cancellationToken).ConfigureAwait(false);
            return next;
        }

        await _repository.SaveAsync(next, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
        return next;
    }
}
