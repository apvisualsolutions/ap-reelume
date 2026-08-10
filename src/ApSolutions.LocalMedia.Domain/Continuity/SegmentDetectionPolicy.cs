// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>
/// What becomes of a detection: which results are worth storing, which stored rows survive a
/// re-detection, and which ranges the player actually sees for a file. Manual markers always win
/// over detections of the same kind, and a person's correction survives every later run.
/// </summary>
public static class SegmentDetectionPolicy
{
    /// <summary>Detections below this confidence are never stored and never shown.</summary>
    public static double MinimumConfidence => 0.6;

    /// <summary>
    /// The rows a series holds after a detection run: corrected rows survive untouched, uncorrected
    /// rows are replaced by the run's valid, confident results, and one row exists per file and kind.
    /// </summary>
    public static IReadOnlyList<DetectedMarker> MergeDetections(
        SeriesSegmentDetection detection,
        IReadOnlyList<DetectedMarker> existing,
        IReadOnlyDictionary<Catalog.MediaFileId, TimeSpan?>? durations = null)
    {
        ArgumentNullException.ThrowIfNull(detection);
        ArgumentNullException.ThrowIfNull(existing);

        var corrected = existing.Where(row => row.UserCorrected).ToList();
        var locked = corrected.Select(row => (row.FileId, row.Kind)).ToHashSet();
        var merged = new List<DetectedMarker>(corrected);

        foreach (var group in detection.Segments
            .Where(segment => IsStorable(
                segment,
                durations?.GetValueOrDefault(segment.FileId)))
            .GroupBy(segment => (segment.FileId, segment.Kind)))
        {
            if (locked.Contains(group.Key))
            {
                continue;
            }

            var best = group.MaxBy(segment => segment.Confidence)!;
            merged.Add(new DetectedMarker(
                Guid.NewGuid(),
                detection.SeriesId,
                best.FileId,
                best.Kind,
                best.Start,
                best.End,
                best.Confidence,
                detection.DetectorVersion,
                UserCorrected: false));
        }

        return merged;
    }

    /// <summary>
    /// The ranges the player uses for one file: every manual marker of the series, plus the file's
    /// detected rows of the kinds no manual marker covers, projected into the shared marker shape.
    /// </summary>
    public static IReadOnlyList<IntroMarker> ComposeForFile(
        IReadOnlyList<IntroMarker> manualMarkers,
        IReadOnlyList<DetectedMarker> detectedForFile)
    {
        ArgumentNullException.ThrowIfNull(manualMarkers);
        ArgumentNullException.ThrowIfNull(detectedForFile);

        var coveredKinds = manualMarkers.Select(marker => marker.Kind).ToHashSet();
        var composed = new List<IntroMarker>(manualMarkers);
        composed.AddRange(detectedForFile
            .Where(row => !coveredKinds.Contains(row.Kind))
            .Select(row => new IntroMarker(
                row.Id,
                row.SeriesId,
                row.Kind,
                row.Start,
                row.End,
                MarkerOrigin.Detected,
                row.Confidence,
                row.UserCorrected)));
        return composed;
    }

    /// <summary>A person confirmed the range as it stands; the row now survives re-detection.</summary>
    public static DetectedMarker Accept(DetectedMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        return marker with { UserCorrected = true };
    }

    /// <summary>
    /// A person adjusted the range. Returns the corrected row, or null when the range is invalid for
    /// the episode; a correction is a human act, so it also survives re-detection.
    /// </summary>
    public static DetectedMarker? Correct(
        DetectedMarker marker,
        TimeSpan start,
        TimeSpan end,
        TimeSpan? duration)
    {
        ArgumentNullException.ThrowIfNull(marker);
        if (!MarkerPolicy.IsValidRange(start, end, duration))
        {
            return null;
        }

        return marker with { Start = start, End = end, UserCorrected = true };
    }

    /// <summary>
    /// A result is storable when its range makes sense and its confidence carries weight. Manual
    /// markers always validated against the episode's duration; when a caller knows it, a detection
    /// past the end is refused by the same rule instead of being judged blind (BUG-009).
    /// </summary>
    private static bool IsStorable(DetectedSegment segment, TimeSpan? duration) =>
        segment.Confidence >= MinimumConfidence
        && segment.Confidence <= 1.0
        && MarkerPolicy.IsValidRange(segment.Start, segment.End, duration);
}
