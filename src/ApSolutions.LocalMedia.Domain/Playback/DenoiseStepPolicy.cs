// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// How far apart the noise reducer starts its windows, chosen from what it costs on the machine it is
/// running on rather than from the size of the picture.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the machine decides and not the resolution</b>, measured on 2026-09-27: a 1080p frame at a
/// step of one took 14 ms on 28 threads and 24 ms on four. A table by resolution would be right on
/// one of those and wrong on the other, and a frame that misses its moment on screen is a stutter a
/// person sees at once, where a step of two is a picture a little less clean that nobody notices.
/// </para>
/// <para>
/// <b>Half the time between frames is the budget.</b> The other half is for everything else the frame
/// goes through on the same thread — the conversion, the curve, the hand-over to the screen.
/// </para>
/// <para>
/// <b>Going finer again waits for room to spare.</b> Each finer step costs about three times the one
/// above it, measured at 1080p (14, 5 and 2 ms), and a step that goes finer the moment it looks as if
/// it might fit comes straight back the next time: a picture whose cleanliness pulses every second
/// is worse than either step held.
/// </para>
/// </remarks>
public static class DenoiseStepPolicy
{
    /// <summary>The coarsest step the reducer has: one window every four pixels each way.</summary>
    public const int CoarsestStep = 4;

    /// <summary>What each finer step costs against the one above it, rounded from the measurement.</summary>
    private const double FinerCostFactor = 3d;

    /// <summary>How much of the budget a finer step has to leave free before it is tried.</summary>
    private const double FinerHeadroom = 0.7d;

    /// <summary>
    /// The step for the next frames, given the one in use, what it has been costing and how often the
    /// frames have been arriving.
    /// </summary>
    public static int Next(int currentStep, double averageCostMilliseconds, double frameIntervalMilliseconds)
    {
        if (currentStep is not (1 or 2 or CoarsestStep))
        {
            throw new ArgumentOutOfRangeException(nameof(currentStep), currentStep, "The step is one, two or four pixels.");
        }

        // Nothing measured is nothing to decide on.
        if (averageCostMilliseconds <= 0d || frameIntervalMilliseconds <= 0d)
        {
            return currentStep;
        }

        var budget = frameIntervalMilliseconds / 2d;
        if (averageCostMilliseconds > budget)
        {
            return Math.Min(currentStep * 2, CoarsestStep);
        }

        return currentStep > 1 && averageCostMilliseconds * FinerCostFactor < budget * FinerHeadroom
            ? currentStep / 2
            : currentStep;
    }
}
