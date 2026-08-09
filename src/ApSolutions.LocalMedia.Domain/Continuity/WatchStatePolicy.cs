namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>
/// Turns a position and an observed duration into one of the three watch states. The rules are the
/// approved policy constants: in progress at the lesser of one minute and two per cent, watched at
/// ninety per cent by default, and a threshold the person can move between fifty and one hundred.
/// </summary>
public static class WatchStatePolicy
{
    public const double DefaultWatchedThreshold = 0.90;

    public const double MinimumWatchedThreshold = 0.50;

    public const double MaximumWatchedThreshold = 1.00;

    private const double SignificantProgressFraction = 0.02;

    private static readonly TimeSpan SignificantProgressCeiling = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How far in the content must be before it counts as started. A short episode reaches it sooner
    /// than a film, which is why the rule is the lesser of the two values rather than a flat minute.
    /// </summary>
    public static TimeSpan SignificantProgress(TimeSpan? duration)
    {
        if (duration is not { } observed || observed <= TimeSpan.Zero)
        {
            return SignificantProgressCeiling;
        }

        var fraction = TimeSpan.FromTicks((long)(observed.Ticks * SignificantProgressFraction));
        return fraction < SignificantProgressCeiling ? fraction : SignificantProgressCeiling;
    }

    /// <summary>Keeps a requested threshold inside the accepted range; nonsense falls back to the default.</summary>
    public static double ClampThreshold(double requested) =>
        double.IsNaN(requested)
            ? DefaultWatchedThreshold
            : Math.Clamp(requested, MinimumWatchedThreshold, MaximumWatchedThreshold);

    /// <summary>
    /// The state the position implies. Without a usable duration there is no percentage to compare,
    /// so content can be in progress but never automatically watched.
    /// </summary>
    public static WatchStatus Evaluate(TimeSpan position, TimeSpan? duration, double watchedThreshold)
    {
        if (position < SignificantProgress(duration))
        {
            return WatchStatus.NotStarted;
        }

        if (duration is not { } observed || observed <= TimeSpan.Zero)
        {
            return WatchStatus.InProgress;
        }

        return position.TotalSeconds / observed.TotalSeconds >= ClampThreshold(watchedThreshold)
            ? WatchStatus.Watched
            : WatchStatus.InProgress;
    }
}
