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
}

/// <summary>
/// The two variants Windows' own high contrast setting maps onto. They are a state, not a fourth
/// choice: the three pills in Appearance stay as they are, and which of these is applied is read
/// from the system rather than picked.
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
