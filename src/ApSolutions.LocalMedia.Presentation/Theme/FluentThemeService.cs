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

    public FluentThemeService(
        Avalonia.Application application,
        ISettingsStore settingsStore,
        IBackdropService backdropService,
        IReducedMotionService reducedMotionService)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _backdropService = backdropService ?? throw new ArgumentNullException(nameof(backdropService));
        _reducedMotionService = reducedMotionService ?? throw new ArgumentNullException(nameof(reducedMotionService));

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

    private void ApplyToApplication(ThemePreference preference)
    {
        _application.RequestedThemeVariant = preference switch
        {
            ThemePreference.System => ThemeVariant.Default,
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(preference)),
        };
    }
}
