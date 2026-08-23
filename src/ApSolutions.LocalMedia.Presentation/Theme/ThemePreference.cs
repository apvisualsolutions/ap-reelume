// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Styling;

namespace ApSolutions.LocalMedia.Presentation.Theme;

public enum ThemePreference
{
    System,
    Light,
    Dark,

    /// <summary>Chosen, not only inherited: §4's fourth pill, revoked into being on 2026-08-23.</summary>
    HighContrastLight,

    /// <summary>And the fifth. Windows' own high contrast still overrides whatever is picked.</summary>
    HighContrastDark,
}

/// <summary>
/// The two variants both the system's high contrast and the two chosen pills map onto. The system
/// setting still wins: when Windows says high contrast, which of these applies is read from the
/// system, whatever the stored preference says.
/// </summary>
public static class AppThemeVariants
{
    public static ThemeVariant HighContrastLight { get; } = new("HighContrastLight", ThemeVariant.Light);

    public static ThemeVariant HighContrastDark { get; } = new("HighContrastDark", ThemeVariant.Dark);
}

public interface IThemeService
{
    ThemePreference CurrentPreference { get; }

    ThemeVariant PlayerThemeVariant { get; }

    bool AnimationsEnabled { get; }

    TimeSpan MotionDuration { get; }

    void Apply(ThemePreference preference);

    bool TryApplyBackdrop(Window window);
}
