// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Presentation.Theme;

/// <summary>
/// Whether the system asks for high contrast, and which side of it. The host answers both, because
/// the question is a Windows one and this assembly carries no Windows dependency.
/// </summary>
public interface IHighContrastService
{
    /// <summary>True when the system is in high contrast, whatever theme it names.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// True when that high contrast theme is a light one. Decided by the colour the system draws
    /// windows with and never by the theme's name, which is localised and can be redefined.
    /// </summary>
    bool IsLight { get; }
}
