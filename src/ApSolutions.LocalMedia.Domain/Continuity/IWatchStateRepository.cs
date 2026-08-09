namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>
/// Stores one row per piece of content. Reading content that was never played is not a failure: an
/// absent row simply means there is nothing to resume.
/// </summary>
public interface IWatchStateRepository
{
    Task<WatchState?> GetAsync(ContentKey content, CancellationToken cancellationToken = default);

    /// <summary>Every stored state, which is what a threshold change has to reconsider.</summary>
    Task<IReadOnlyList<WatchState>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the state atomically, replacing any earlier row for the same content.</summary>
    Task SaveAsync(WatchState state, CancellationToken cancellationToken = default);
}
