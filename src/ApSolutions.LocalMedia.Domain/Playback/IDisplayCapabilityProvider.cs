// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// Reports what the display the player is on can do right now. Implemented by the host, because the
/// answer is a Windows display-configuration question, and asked again on every session so turning
/// HDR on or off in Windows is picked up without a restart.
/// </summary>
public interface IDisplayCapabilityProvider
{
    /// <summary>
    /// Capabilities of the display currently showing the player. An implementation that cannot ask
    /// the system reports no HDR rather than guessing.
    /// </summary>
    DisplayCapabilities GetCurrentDisplay();
}
