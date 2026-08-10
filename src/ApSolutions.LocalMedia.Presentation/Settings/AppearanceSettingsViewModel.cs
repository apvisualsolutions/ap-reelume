// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Presentation.Language;
using ApSolutions.LocalMedia.Presentation.Theme;

namespace ApSolutions.LocalMedia.Presentation.Settings;

public sealed class AppearanceSettingsViewModel : INotifyPropertyChanged
{
    private readonly IThemeService _themeService;
    private readonly ILanguageService? _languageService;

    public AppearanceSettingsViewModel(IThemeService themeService, ILanguageService? languageService = null)
    {
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _languageService = languageService;
        ApplyThemeCommand = new ApplyPreferenceCommand(ApplyTheme);
        ApplyLanguageCommand = new ApplyLanguageCommandImplementation(ApplyLanguage);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ThemePreference CurrentPreference => _themeService.CurrentPreference;

    public string SystemStateCue => GetStateCue(ThemePreference.System);

    public string LightStateCue => GetStateCue(ThemePreference.Light);

    public string DarkStateCue => GetStateCue(ThemePreference.Dark);

    public string CurrentLanguage => _languageService?.Current ?? "es";

    public string SpanishStateCue => CurrentLanguage == "es" ? "●" : "○";

    public string EnglishStateCue => CurrentLanguage == "en" ? "●" : "○";

    public ICommand ApplyThemeCommand { get; }

    /// <summary>Takes "es" or "en" and makes the whole application speak it (BUG-011).</summary>
    public ICommand ApplyLanguageCommand { get; }

    private void ApplyTheme(ThemePreference preference)
    {
        _themeService.Apply(preference);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPreference)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SystemStateCue)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LightStateCue)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DarkStateCue)));
    }

    private void ApplyLanguage(string language)
    {
        if (_languageService is not { } service)
        {
            return;
        }

        service.Apply(language);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpanishStateCue)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnglishStateCue)));
    }

    private string GetStateCue(ThemePreference preference) =>
        CurrentPreference == preference ? "●" : "○";

    private sealed class ApplyLanguageCommandImplementation(Action<string> apply) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => parameter is "es" or "en";

        public void Execute(object? parameter)
        {
            if (parameter is string language && CanExecute(language))
            {
                apply(language);
            }
        }
    }

    private sealed class ApplyPreferenceCommand(Action<ThemePreference> apply) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => parameter is ThemePreference;

        public void Execute(object? parameter)
        {
            if (parameter is ThemePreference preference)
            {
                apply(preference);
            }
        }
    }
}
