// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// What the player carries from frame to frame to choose the noise reducer's step: it averages what
/// the reducer cost and how often frames arrived, and asks <see cref="DenoiseStepPolicy"/> every
/// <see cref="DecisionFrames"/> frames, so one slow frame does not change the picture.
/// </summary>
/// <remarks>
/// Not safe to share between threads, and it does not need to be: the player feeds it from the one
/// thread LibVLC hands frames over on.
/// </remarks>
public sealed class DenoiseCadence
{
    /// <summary>
    /// Frames measured before each decision: half a second of a film at 24 frames a second, long
    /// enough that a single slow frame is an outlier and short enough that a stutter is brief.
    /// </summary>
    public const int DecisionFrames = 12;

    /// <summary>
    /// The longest gap between two frames that counts as the film's pace. No frame arrives while the
    /// film is paused, and the first one after a pause would otherwise make every cost look small.
    /// </summary>
    private const double LongestInterval = 250d;

    private double _cost;
    private double _interval;
    private int _frames;

    /// <summary>The step the reducer should use now.</summary>
    public int Step { get; private set; } = 1;

    /// <summary>Takes one frame's measurements into account.</summary>
    public void Record(double costMilliseconds, double intervalMilliseconds)
    {
        _cost += costMilliseconds;
        _interval += Math.Min(intervalMilliseconds, LongestInterval);
        if (++_frames < DecisionFrames)
        {
            return;
        }

        // Whatever was decided, the costs measured so far belong to the step they were measured at.
        Step = DenoiseStepPolicy.Next(Step, _cost / _frames, _interval / _frames);
        Clear();
    }

    /// <summary>Starts again at the finest step, for a new film or a new size.</summary>
    public void Reset()
    {
        Step = 1;
        Clear();
    }

    private void Clear()
    {
        _cost = 0d;
        _interval = 0d;
        _frames = 0;
    }
}
