using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Application.Continuity;

/// <summary>
/// Records a decision the person made by hand. The override stays in force until the same person
/// undoes it, so nothing the player computes afterwards can quietly change the state back.
/// </summary>
public sealed class SetWatchStatus
{
    private readonly IWatchStateRepository _repository;
    private readonly IClock _clock;

    public SetWatchStatus(IWatchStateRepository repository, IClock clock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Marks content watched or not started. The stored position is kept either way: marking something
    /// unwatched is a statement about the state, not an instruction to forget where it was left.
    /// </summary>
    public async Task<WatchState> MarkAsync(
        ContentKey content,
        MediaFileId sourceMediaFileId,
        WatchStatus status,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetAsync(content, cancellationToken).ConfigureAwait(false);
        var now = _clock.UtcNow;
        var state = new WatchState
        {
            Content = content,
            Position = existing?.Position ?? TimeSpan.Zero,
            ObservedDuration = existing?.ObservedDuration,
            SourceMediaFileId = existing?.SourceMediaFileId ?? sourceMediaFileId,
            Status = status,
            IsManualOverride = true,
            StartedUtc = existing?.StartedUtc ?? now,
            UpdatedUtc = now,
        };
        await _repository.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        return state;
    }

    /// <summary>
    /// Hands the state back to the automatic rules and recomputes it from the stored position. Content
    /// that was never played has nothing to undo.
    /// </summary>
    public async Task<WatchState?> ClearOverrideAsync(
        ContentKey content,
        double watchedThreshold,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetAsync(content, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        var state = existing with
        {
            Status = WatchStatePolicy.Evaluate(existing.Position, existing.ObservedDuration, watchedThreshold),
            IsManualOverride = false,
            UpdatedUtc = _clock.UtcNow,
        };
        await _repository.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        return state;
    }
}
