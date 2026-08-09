using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>One episode the detector may read, already resolved to a real local file.</summary>
public sealed record SegmentDetectionEpisode(MediaFileId FileId, string Path, TimeSpan? Duration);

/// <summary>What the detector found in one episode, before any policy or storage decision.</summary>
public sealed record DetectedSegment(
    MediaFileId FileId,
    MarkerKind Kind,
    TimeSpan Start,
    TimeSpan End,
    double Confidence);

/// <summary>Every segment one detection run found, tagged with the version that produced it.</summary>
public sealed record SeriesSegmentDetection(
    SeriesId SeriesId,
    int DetectorVersion,
    IReadOnlyList<DetectedSegment> Segments);

/// <summary>How far a detection run has come, in episodes whose features have been read.</summary>
public sealed record SegmentDetectionProgress(int EpisodesProcessed, int TotalEpisodes);

/// <summary>
/// Finds recurring segments across the episodes of one series by comparing them with each other,
/// locally and without ever opening a connection. Implementations honour cancellation promptly and
/// never touch storage: what to keep is the policy's decision, not the detector's.
/// </summary>
public interface IAutomaticSegmentDetector
{
    /// <summary>Version stamped on every result so a better detector can supersede stored rows.</summary>
    int Version { get; }

    Task<SeriesSegmentDetection> DetectAsync(
        SeriesId seriesId,
        IReadOnlyList<SegmentDetectionEpisode> episodes,
        IProgress<SegmentDetectionProgress>? progress,
        CancellationToken cancellationToken);
}
