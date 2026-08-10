// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// What the engine must do for a requested level: the gain to apply, whether this is a boost, and
/// whether the limiter and its warning are in force. Boost and limiter are never separable.
/// </summary>
public sealed record VolumeDecision(
    int Percent,
    bool IsMuted,
    bool IsBoosted,
    bool LimiterEngaged,
    double LinearGain)
{
    /// <summary>The interface must show a visual and a textual warning whenever this is true.</summary>
    public bool RequiresWarning => IsBoosted;
}

/// <summary>
/// Volume from silence to twice the normal level. Anything above one hundred percent always engages
/// the peak limiter and always raises a warning, so amplified audio can never clip unannounced.
/// </summary>
public static class VolumeBoostPolicy
{
    public const int MinimumPercent = 0;
    public const int MaximumNormalPercent = 100;
    public const int MaximumBoostPercent = 200;

    /// <summary>Normalised sample level the limiter never lets the output exceed.</summary>
    public const double LimiterThreshold = 0.98;

    public static VolumeDecision Decide(int requestedPercent, bool muted)
    {
        var percent = Math.Clamp(requestedPercent, MinimumPercent, MaximumBoostPercent);
        var boosted = percent > MaximumNormalPercent;
        return new VolumeDecision(
            percent,
            muted,
            boosted,
            LimiterEngaged: boosted,
            LinearGain: muted ? 0 : percent / 100.0);
    }
}
