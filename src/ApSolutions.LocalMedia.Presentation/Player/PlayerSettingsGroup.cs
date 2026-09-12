// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Which group of options the gear is showing, if any (ADR-0012).
/// </summary>
/// <remarks>
/// <see cref="None"/> is the list itself rather than the absence of a group, the same way
/// <c>PlayerPanel.None</c> is the closed column: the first level is a state the gear can be in, and
/// a nullable around this would make «showing the list» and «showing nothing» the same value.
/// </remarks>
public enum PlayerSettingsGroup
{
    /// <summary>The first level: the list of groups.</summary>
    None = 0,

    /// <summary>Brightness, contrast and gamma (PLY-018).</summary>
    Picture,
}
