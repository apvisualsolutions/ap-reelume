using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>Which part of an episode a range covers.</summary>
public enum MarkerKind
{
    Intro,
    Recap,
    Credits,
}

/// <summary>
/// Where a range came from. This release only ever creates <see cref="Manual"/>; the detected value
/// exists so a later release can add detection without migrating the stored markers.
/// </summary>
public enum MarkerOrigin
{
    Manual,
    Detected,
}

/// <summary>
/// A range a person can skip, stored per series rather than per episode because an intro is the same
/// in every episode of a show.
/// </summary>
public sealed record IntroMarker(
    Guid Id,
    SeriesId SeriesId,
    MarkerKind Kind,
    TimeSpan Start,
    TimeSpan End,
    MarkerOrigin Origin,
    double? Confidence,
    bool UserCorrected);

/// <summary>
/// What makes a range valid, when two ranges collide, and when the skip button exists. Two ranges of
/// different kinds may cover the same seconds — a recap can sit inside an intro — so overlap is only
/// ever checked against ranges of the same kind.
/// </summary>
public static class MarkerPolicy
{
    /// <summary>True when the range runs forward and stays inside the episode.</summary>
    public static bool IsValidRange(TimeSpan start, TimeSpan end, TimeSpan? duration)
    {
        if (start < TimeSpan.Zero || end <= start)
        {
            return false;
        }

        return duration is not { } observed || observed <= TimeSpan.Zero || end <= observed;
    }

    /// <summary>
    /// The stored range of the same kind this one would overlap, or null when there is none. Ranges
    /// that merely touch at an edge do not overlap.
    /// </summary>
    public static IntroMarker? FindConflict(
        IEnumerable<IntroMarker> existing,
        Guid? ignoreId,
        MarkerKind kind,
        TimeSpan start,
        TimeSpan end)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return existing.FirstOrDefault(marker =>
            marker.Kind == kind
            && marker.Id != ignoreId
            && start < marker.End
            && marker.Start < end);
    }

    /// <summary>The button exists from the first second of the range until the last, never after it.</summary>
    public static bool IsButtonVisible(IntroMarker marker, TimeSpan position)
    {
        ArgumentNullException.ThrowIfNull(marker);
        return position >= marker.Start && position < marker.End;
    }

    /// <summary>Skipping lands on the end of the range, not one frame before or after.</summary>
    public static TimeSpan SkipTarget(IntroMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        return marker.End;
    }

    /// <summary>The range the position currently sits inside, or null when it sits in none.</summary>
    public static IntroMarker? FindActive(IEnumerable<IntroMarker> markers, TimeSpan position)
    {
        ArgumentNullException.ThrowIfNull(markers);
        return markers.FirstOrDefault(marker => IsButtonVisible(marker, position));
    }
}
