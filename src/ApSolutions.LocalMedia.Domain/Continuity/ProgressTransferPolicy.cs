namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>What to do with stored progress when another version of the same content is opened.</summary>
public enum ProgressTransferKind
{
    /// <summary>The very same second is still correct.</summary>
    Exact,

    /// <summary>The second is scaled by the ratio between the two durations.</summary>
    Proportional,

    /// <summary>The move needs a person to agree first.</summary>
    Confirm,

    /// <summary>There is nothing worth carrying across.</summary>
    Restart,
}

/// <summary>Why a confirmation is being asked for, so the interface can say it in words.</summary>
public enum ProgressTransferReason
{
    None,

    /// <summary>The two versions differ by more than the safe proportion.</summary>
    LargeDifference,

    /// <summary>One of the two lengths is not known, so no ratio can be trusted.</summary>
    UnknownDuration,

    /// <summary>The versions are different edits rather than the same edit re-encoded.</summary>
    IncompatibleStructure,
}

/// <summary>
/// The decision, with the second it lands on. For a confirmation the second is the suggestion the
/// person is being asked about, not a value already applied.
/// </summary>
public sealed record ProgressTransferDecision(
    ProgressTransferKind Kind,
    TimeSpan Position,
    ProgressTransferReason Reason)
{
    public static ProgressTransferDecision Exact(TimeSpan position) =>
        new(ProgressTransferKind.Exact, position, ProgressTransferReason.None);

    public static ProgressTransferDecision Proportional(TimeSpan position) =>
        new(ProgressTransferKind.Proportional, position, ProgressTransferReason.None);

    public static ProgressTransferDecision Confirm(TimeSpan suggested, ProgressTransferReason reason) =>
        new(ProgressTransferKind.Confirm, suggested, reason);

    public static ProgressTransferDecision Restart() =>
        new(ProgressTransferKind.Restart, TimeSpan.Zero, ProgressTransferReason.None);
}

/// <summary>
/// Decides how progress moves between two versions of the same content. Progress belongs to the
/// content, so the rules are about the two durations, never about the files.
/// </summary>
public static class ProgressTransferPolicy
{
    /// <summary>Beyond this proportion of difference the move always needs agreement.</summary>
    public const double ProportionalLimit = 0.10;

    private const double ExactToleranceFraction = 0.01;

    private static readonly TimeSpan ExactToleranceFloor = TimeSpan.FromSeconds(5);

    /// <summary>How much two versions may differ and still hold the same second: five seconds or one per cent.</summary>
    public static TimeSpan ExactTolerance(TimeSpan? duration)
    {
        if (duration is not { } observed || observed <= TimeSpan.Zero)
        {
            return ExactToleranceFloor;
        }

        var fraction = TimeSpan.FromTicks((long)(observed.Ticks * ExactToleranceFraction));
        return fraction > ExactToleranceFloor ? fraction : ExactToleranceFloor;
    }

    public static ProgressTransferDecision Decide(
        TimeSpan position,
        TimeSpan? sourceDuration,
        TimeSpan? targetDuration,
        bool structureIsCompatible)
    {
        if (!ProgressPolicy.ShouldOfferResume(position, sourceDuration))
        {
            return ProgressTransferDecision.Restart();
        }

        if (sourceDuration is not { } source
            || targetDuration is not { } target
            || source <= TimeSpan.Zero
            || target <= TimeSpan.Zero)
        {
            return ProgressTransferDecision.Confirm(position, ProgressTransferReason.UnknownDuration);
        }

        var difference = (target - source).Duration();
        if (difference <= ExactTolerance(source))
        {
            return ProgressTransferDecision.Exact(ProgressPolicy.ClampPosition(position, target));
        }

        var scaled = ProgressPolicy.ClampPosition(
            TimeSpan.FromTicks((long)(position.Ticks * (target.TotalSeconds / source.TotalSeconds))),
            target);
        if (difference > TimeSpan.FromTicks((long)(source.Ticks * ProportionalLimit)))
        {
            return ProgressTransferDecision.Confirm(scaled, ProgressTransferReason.LargeDifference);
        }

        return structureIsCompatible
            ? ProgressTransferDecision.Proportional(scaled)
            : ProgressTransferDecision.Confirm(scaled, ProgressTransferReason.IncompatibleStructure);
    }
}
