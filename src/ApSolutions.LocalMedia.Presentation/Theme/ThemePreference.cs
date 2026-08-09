using Avalonia.Controls;
using Avalonia.Styling;

namespace ApSolutions.LocalMedia.Presentation.Theme;

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

public static class AppThemeVariants
{
    public static ThemeVariant HighContrast { get; } = new("HighContrast", ThemeVariant.Light);
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
