// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// How LibVLC names the track of a kind that is in force, and how it says there is none.
/// </summary>
/// <remarks>
/// One rule in one place, and out of the adapter on purpose. The adapter can only be measured on a
/// machine that has a decoder, so a branch written there is covered where the hardware allows it and
/// uncovered where it does not — which is how a file falls below its floor on a hosted runner while
/// passing on the machine it was written on. This branch is arithmetic, so it is taken by a test
/// anywhere.
/// </remarks>
public static class LibVlcTrackIdentity
{
    /// <summary>The integer LibVLC uses for "this kind is switched off".</summary>
    public const int Disabled = -1;

    /// <summary>
    /// The identifier as the domain names tracks, or null when LibVLC reports the disabled sentinel.
    /// </summary>
    /// <remarks>
    /// Invariant, because the identifier travels to <c>SetSpu</c> and <c>SetAudioTrack</c> as a
    /// number parsed back the same way: a machine whose culture writes a different digit group would
    /// otherwise announce a track it could not then select.
    /// </remarks>
    public static string? Announced(int identifier) =>
        identifier == Disabled ? null : identifier.ToString(CultureInfo.InvariantCulture);
}
