namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>Why a position is being written. Everything except the periodic tick is critical.</summary>
public enum PersistenceTrigger
{
    Tick,
    Pause,
    Seek,
    ModeChange,
    FileChange,
    Close,
    EngineFailure,
}

/// <summary>
/// When a position is worth writing and when it is worth offering back. The values are the approved
/// policy constants: save every five seconds, and never resume from the first thirty.
/// </summary>
public static class ProgressPolicy
{
    /// <summary>How often the periodic tick writes while playback advances.</summary>
    public static TimeSpan SaveInterval { get; } = TimeSpan.FromSeconds(5);

    /// <summary>Below this point a resume is not worth offering; the person restarts instead.</summary>
    public static TimeSpan MinimumResumePosition { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How far the position must have moved for a tick to be worth a write. A paused or stalled
    /// session repeats the same reading, and rewriting it would be pure disk traffic.
    /// </summary>
    public static TimeSpan TickWriteEpsilon { get; } = TimeSpan.FromSeconds(1);

    /// <summary>A critical trigger always writes, because the session may not get another chance.</summary>
    public static bool IsCritical(PersistenceTrigger trigger) => trigger is not PersistenceTrigger.Tick;

    /// <summary>Keeps a position inside the observed media; an unknown duration only clamps the floor.</summary>
    public static TimeSpan ClampPosition(TimeSpan position, TimeSpan? duration)
    {
        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        // A zero or negative length is an unobserved media rather than one that ends immediately.
        return duration is { } observed && observed > TimeSpan.Zero && position > observed
            ? observed
            : position;
    }

    /// <summary>True when stored progress is worth offering as a resume point.</summary>
    public static bool ShouldOfferResume(TimeSpan position, TimeSpan? duration)
    {
        if (position < MinimumResumePosition)
        {
            return false;
        }

        return duration is not { } observed || observed <= TimeSpan.Zero || position < observed;
    }

    /// <summary>True when this candidate must reach storage now.</summary>
    public static bool ShouldPersist(TimeSpan? lastPersisted, TimeSpan candidate, PersistenceTrigger trigger)
    {
        if (IsCritical(trigger))
        {
            return true;
        }

        return lastPersisted is not { } last || (candidate - last).Duration() >= TickWriteEpsilon;
    }
}
