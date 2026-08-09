using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>Stores the ranges of one series. A series with no ranges simply reads as empty.</summary>
public interface IIntroMarkerRepository
{
    Task<IReadOnlyList<IntroMarker>> GetForSeriesAsync(
        SeriesId seriesId,
        CancellationToken cancellationToken = default);

    Task<IntroMarker?> GetAsync(Guid markerId, CancellationToken cancellationToken = default);

    Task SaveAsync(IntroMarker marker, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid markerId, CancellationToken cancellationToken = default);
}
