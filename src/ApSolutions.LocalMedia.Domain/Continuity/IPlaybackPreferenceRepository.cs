// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>
/// Stores one preference per scope key. Reads never fail when nothing is stored: an absent row means
/// the next scope answers.
/// </summary>
public interface IPlaybackPreferenceRepository
{
    Task<PlaybackPreference?> GetAsync(
        PreferenceScope scope,
        string scopeKey,
        CancellationToken cancellationToken = default);

    Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default);

    Task RemoveAsync(
        PreferenceScope scope,
        string scopeKey,
        CancellationToken cancellationToken = default);
}
