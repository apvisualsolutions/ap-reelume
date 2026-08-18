// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace ApSolutions.LocalMedia.Presentation.Theme;

public sealed class FluentThemeService : IThemeService
{
    private const string PreferenceKey = "theme.preference";
    private static readonly TimeSpan StandardMotionDuration = TimeSpan.FromMilliseconds(160);
    private readonly Avalonia.Application _application;
    private readonly ISettingsStore _settingsStore;
    private readonly IBackdropService _backdropService;
    private readonly IReducedMotionService _reducedMotionService;
    private readonly IHighContrastService _highContrastService;

    public FluentThemeService(
        Avalonia.Application application,
        ISettingsStore settingsStore,
        IBackdropService backdropService,
        IReducedMotionService reducedMotionService,
        IHighContrastService highContrastService)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _backdropService = backdropService ?? throw new ArgumentNullException(nameof(backdropService));
        _reducedMotionService = reducedMotionService ?? throw new ArgumentNullException(nameof(reducedMotionService));
        _highContrastService = highContrastService ?? throw new ArgumentNullException(nameof(highContrastService));

        var storedPreference = _settingsStore.Read<ThemePreference>(PreferenceKey);
        CurrentPreference = Enum.IsDefined(storedPreference)
            ? storedPreference
            : ThemePreference.System;
        ApplyToApplication(CurrentPreference);
    }

    public ThemePreference CurrentPreference { get; private set; }

    public ThemeVariant PlayerThemeVariant => ThemeVariant.Dark;

    public bool AnimationsEnabled => !_reducedMotionService.IsEnabled;

    public TimeSpan MotionDuration => AnimationsEnabled ? StandardMotionDuration : TimeSpan.Zero;

    public void Apply(ThemePreference preference)
    {
        if (!Enum.IsDefined(preference))
        {
            throw new ArgumentOutOfRangeException(nameof(preference));
        }

        ApplyToApplication(preference);
        _settingsStore.Write(PreferenceKey, preference);
        CurrentPreference = preference;
    }

    public bool TryApplyBackdrop(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return _backdropService.TryApply(window);
    }

    /// <summary>
    /// The three pills decide the theme, except when the system asks for high contrast: that is a
    /// need rather than a taste, so it overrides the choice instead of appearing beside it. Which
    /// side of it applies is read from the system too, so the preference needs no fourth value and
    /// no stored setting has to migrate.
    /// </summary>
    /// <remarks>
    /// Read when the theme is applied — at startup and on every change of preference. Turning high
    /// contrast on in Windows while the application is already open therefore reaches it on the next
    /// launch; following it live needs a settings-change message, which is not this.
    /// </remarks>
    private void ApplyToApplication(ThemePreference preference)
    {
        if (!Enum.IsDefined(preference))
        {
            throw new ArgumentOutOfRangeException(nameof(preference));
        }

        if (_highContrastService.IsEnabled)
        {
            _application.RequestedThemeVariant = _highContrastService.IsLight
                ? AppThemeVariants.HighContrastLight
                : AppThemeVariants.HighContrastDark;
            return;
        }

        _application.RequestedThemeVariant = preference switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
