using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>Stores the detected ranges of a series, one row per episode file and kind.</summary>
public interface IDetectedMarkerRepository
{
    Task<IReadOnlyList<DetectedMarker>> GetForSeriesAsync(
        SeriesId seriesId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DetectedMarker>> GetForFileAsync(
        MediaFileId fileId,
        CancellationToken cancellationToken = default);

    Task<DetectedMarker?> GetAsync(Guid markerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces the series' rows with the given set. The policy decides that set; the
    /// repository never filters, so a caller that forgets the policy would visibly lose corrections.
    /// </summary>
    Task ReplaceForSeriesAsync(
        SeriesId seriesId,
        IReadOnlyList<DetectedMarker> markers,
        CancellationToken cancellationToken = default);

    Task SaveAsync(DetectedMarker marker, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid markerId, CancellationToken cancellationToken = default);
}
