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

    /// <summary>The resource the theme declares the standard duration in, and the one this writes.</summary>
    private const string MotionDurationKey = "MotionDuration";

    /// <summary>
    /// What the token says when nothing has overridden it, for a host with no theme merged in.
    /// </summary>
    private static readonly TimeSpan FallbackMotionDuration = TimeSpan.FromMilliseconds(160);
    private readonly Avalonia.Application _application;
    private readonly ISettingsStore _settingsStore;
    private readonly IBackdropService _backdropService;
    private readonly IReducedMotionService _reducedMotionService;
    private readonly IHighContrastService _highContrastService;
    private readonly TimeSpan _standardMotionDuration;

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
        _standardMotionDuration = ReadDeclaredMotionDuration();
        ApplyToApplication(CurrentPreference);
        ApplyMotion();
    }

    public ThemePreference CurrentPreference { get; private set; }

    public ThemeVariant PlayerThemeVariant => ThemeVariant.Dark;

    public bool AnimationsEnabled => !_reducedMotionService.IsEnabled;

    public TimeSpan MotionDuration => AnimationsEnabled ? _standardMotionDuration : TimeSpan.Zero;

    public void Apply(ThemePreference preference)
    {
        if (!Enum.IsDefined(preference))
        {
            throw new ArgumentOutOfRangeException(nameof(preference));
        }

        ApplyToApplication(preference);
        ApplyMotion();
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
    /// <para>
    /// Read when the theme is applied — at startup and on every change of preference. Turning high
    /// contrast on in Windows while the application is already open therefore reaches it on the next
    /// launch; following it live needs a settings-change message, which is not this.
    /// </para>
    /// <para>
    /// It validated the preference again until 2026-08-22, and <b>nothing could take that branch</b>:
    /// its two callers are <see cref="Apply"/>, which throws on an undefined value before getting
    /// here, and the constructor, which passes a value it just normalised. A guard no caller can
    /// reach is not caution, it is two branches no test can cover — the same shape
    /// <c>RouteStateConverter</c> lost three of, and the reason this file's branch coverage went
    /// backwards while its code stood still. The <c>default</c> arm of the switch below is what
    /// actually answers an impossible value, and it answers it with the system theme.
    /// </para>
    /// </remarks>
    private void ApplyToApplication(ThemePreference preference)
    {
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
            ThemePreference.HighContrastLight => AppThemeVariants.HighContrastLight,
            ThemePreference.HighContrastDark => AppThemeVariants.HighContrastDark,
            _ => ThemeVariant.Default,
        };
    }

    /// <summary>
    /// The duration the theme declares, read once, so this service holds no copy of the number.
    /// </summary>
    /// <remarks>
    /// A 160 written here beside a 160 in the token file is the shape the scalars gate deleted the
    /// last pair for: two numbers that will disagree the first time one of them moves. The fallback
    /// is for a host with no theme merged in, which is how several suites mount a single view.
    /// </remarks>
    private TimeSpan ReadDeclaredMotionDuration() =>
        _application.TryFindResource(MotionDurationKey, null, out var declared) && declared is TimeSpan duration
            ? duration
            : FallbackMotionDuration;

    /// <summary>
    /// Writes the duration every animation reads, which is what makes reduced motion reach them.
    /// </summary>
    /// <remarks>
    /// Reduced motion takes animations to <b>zero</b> rather than shortening them, and an animation
    /// cannot ask a service anything: it reads a resource. So the service writes the resource. The
    /// application's own dictionary wins over the merged theme, which is why setting it here
    /// overrides the declared 160 without editing the token.
    /// </remarks>
    private void ApplyMotion() =>
        _application.Resources[MotionDurationKey] = MotionDuration;
}
