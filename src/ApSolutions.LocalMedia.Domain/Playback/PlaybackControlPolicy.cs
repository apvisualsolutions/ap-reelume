// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// The approved ranges for speed, seeking, and skipping. Every request the person can make passes
/// through here, so an adapter never has to decide what a legal value is.
/// </summary>
public static class PlaybackControlPolicy
{
    public const double MinimumSpeed = 0.25;
    public const double MaximumSpeed = 4.0;

    /// <summary>Initial backward skip; configurable, always positive.</summary>
    public static readonly TimeSpan DefaultBackwardSkip = TimeSpan.FromSeconds(10);

    /// <summary>Initial forward skip; configurable, always positive.</summary>
    public static readonly TimeSpan DefaultForwardSkip = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan MinimumSkipInterval = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan MaximumSkipInterval = TimeSpan.FromMinutes(10);

    /// <summary>The steps the interface offers; every one is inside the allowed range.</summary>
    /// <remarks>
    /// <para>
    /// Nine, and it was ten until 2026-08-28: the prototype's own list is
    /// <c>[0.25, 0.5, 0.75, 1, 1.25, 1.5, 2, 3, 4]</c> and this one carried a <c>1.75</c> besides.
    /// The prototype outranks the document and the document outranks the markup, so the step nobody
    /// drew came out rather than being drawn.
    /// </para>
    /// <para>
    /// It is read by the transport, which builds its menu from it. That is worth saying because it
    /// was not true until the same day: the menu wrote the ten numbers into its own markup and a test
    /// read that markup back <em>as text</em> to compare the two, so this list decided nothing and
    /// the comment above it claimed the keyboard walked it. Nothing walked it.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<double> SpeedSteps { get; } =
        [0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0];

    public static double ClampSpeed(double requested) => Math.Clamp(requested, MinimumSpeed, MaximumSpeed);

    /// <summary>Keeps a position inside the observed duration; an unknown duration bounds only at zero.</summary>
    public static TimeSpan ClampPosition(TimeSpan position, TimeSpan? duration)
    {
        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return duration is { } observed && position > observed ? observed : position;
    }

    /// <summary>Moves by a signed amount and lands exactly on the boundary instead of overshooting.</summary>
    public static TimeSpan Skip(TimeSpan current, TimeSpan delta, TimeSpan? duration) =>
        ClampPosition(current + delta, duration);

    /// <summary>A configured interval is always a positive, sensible duration.</summary>
    public static TimeSpan ClampSkipInterval(TimeSpan requested)
    {
        if (requested < MinimumSkipInterval)
        {
            return MinimumSkipInterval;
        }

        return requested > MaximumSkipInterval ? MaximumSkipInterval : requested;
    }
}
