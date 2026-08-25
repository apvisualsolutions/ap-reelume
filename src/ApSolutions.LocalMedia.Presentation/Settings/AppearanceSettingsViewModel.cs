// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Domain.Appearance;
using ApSolutions.LocalMedia.Presentation.Language;
using ApSolutions.LocalMedia.Presentation.Theme;
using Avalonia.Styling;

namespace ApSolutions.LocalMedia.Presentation.Settings;

public sealed class AppearanceSettingsViewModel : INotifyPropertyChanged
{
    private readonly IThemeService _themeService;
    private readonly ILanguageService? _languageService;
    private readonly IAppearanceService? _appearance;

    public AppearanceSettingsViewModel(IThemeService themeService, ILanguageService? languageService = null)
        : this(themeService, languageService, appearance: null)
    {
    }

    /// <summary>
    /// The page as the application composes it, with the nine preferences the prototype offers
    /// beside the theme and the language.
    /// </summary>
    /// <remarks>
    /// The appearance service is optional for the same reason the language one is: several suites
    /// mount this page against a theme service alone, and a page that threw without one would make
    /// every one of them about composition rather than about what they measure. Without it the nine
    /// rows read their defaults and writing to them does nothing, which is what a page with nowhere
    /// to store a preference honestly is.
    /// </remarks>
    public AppearanceSettingsViewModel(
        IThemeService themeService,
        ILanguageService? languageService,
        IAppearanceService? appearance)
    {
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _languageService = languageService;
        _appearance = appearance;
        ApplyThemeCommand = new ApplyPreferenceCommand(ApplyTheme);
        ApplyLanguageCommand = new ApplyLanguageCommandImplementation(ApplyLanguage);
        ApplyAccentCommand = new ApplyAccentCommandImplementation(ApplyAccent);
        ApplyDensityCommand = new ApplyEnumCommand<InterfaceDensity>(value => Density = value);
        ApplyRoundingCommand = new ApplyEnumCommand<CornerRounding>(value => Rounding = value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ThemePreference CurrentPreference => _themeService.CurrentPreference;

    public string SystemStateCue => GetStateCue(ThemePreference.System);

    public string HighContrastLightStateCue => GetStateCue(ThemePreference.HighContrastLight);

    public string HighContrastDarkStateCue => GetStateCue(ThemePreference.HighContrastDark);

    public string LightStateCue => GetStateCue(ThemePreference.Light);

    public string DarkStateCue => GetStateCue(ThemePreference.Dark);

    /// <summary>
    /// Which of the two reduced-motion sentences the page shows, as a key rather than as words.
    /// </summary>
    /// <remarks>
    /// A key and not a sentence, the same way the recommendation reasons travel, so the words follow
    /// the chosen language instead of being decided when this property was read. And the answer was
    /// already here: <c>AnimationsEnabled</c> is the negation of the reduced-motion service, so this
    /// page held the thing that knew and said the same sentence whether Windows was animating or not.
    /// </remarks>
    public string ReducedMotionNoticeKey =>
        _themeService.AnimationsEnabled ? "ReducedMotionNotice" : "ReducedMotionActiveNotice";

    public string CurrentLanguage => _languageService?.Current ?? "es";

    public string SpanishStateCue => CurrentLanguage == "es" ? "●" : "○";

    public string EnglishStateCue => CurrentLanguage == "en" ? "●" : "○";

    public ICommand ApplyThemeCommand { get; }

    /// <summary>Takes "es" or "en" and makes the whole application speak it (BUG-011).</summary>
    public ICommand ApplyLanguageCommand { get; }

    /// <summary>Takes a <c>#RRGGBB</c> and derives the whole accent family from it.</summary>
    public ICommand ApplyAccentCommand { get; }

    private AppearanceOptions Options => _appearance?.Current ?? new AppearanceOptions();

    /// <summary>The six the prototype offers, in its own order.</summary>
    public static IReadOnlyList<string> AccentSwatches => AccentPalette.Presets;

    /// <summary>The grid the picker opens with, which is the domain's and not this page's.</summary>
    public static IReadOnlyList<string> ColourGrid => AccentPalette.Grid;

    /// <summary>What is chosen right now, upper-cased, for the monospaced readout beside them.</summary>
    public string AccentHex => Options.Accent.ToUpperInvariant();

    /// <summary>
    /// Which swatch is the one in force, as a flag the style reads rather than a glyph on top of it.
    /// </summary>
    /// <remarks>
    /// It was a glyph — the same ● and ○ every pill row in this tree carries — and the owner was
    /// right that it does not belong here: a circle drawn inside a circle of colour reads as a radio
    /// button somebody dropped on a swatch. The prototype says it with the swatch's own edge, a ring
    /// of the page's ink around the chosen one, and that is geometry too: a border is a shape, so it
    /// survives both high contrast dictionaries exactly as the glyph did.
    /// </remarks>
    public bool IsFirstAccent => IsAccent(0);

    public bool IsSecondAccent => IsAccent(1);

    public bool IsThirdAccent => IsAccent(2);

    public bool IsFourthAccent => IsAccent(3);

    public bool IsFifthAccent => IsAccent(4);

    public bool IsSixthAccent => IsAccent(5);

    /// <summary>True when what is chosen is none of the six, which is what the picker is for.</summary>
    public bool IsCustomAccent => !AccentPalette.Presets.Any(
        preset => string.Equals(preset, Options.Accent, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The three the picker moves. They are the chosen colour taken apart, so opening the picker
    /// starts where the person already is rather than at some default nobody picked.
    /// </summary>
    public double AccentHue
    {
        get => AccentPalette.Split(Options.Accent).Hue;
        set => ApplyAccent(AccentPalette.Join(value, AccentSaturation, AccentLightness));
    }

    public double AccentSaturation
    {
        get => AccentPalette.Split(Options.Accent).Saturation;
        set => ApplyAccent(AccentPalette.Join(AccentHue, value, AccentLightness));
    }

    public double AccentLightness
    {
        get => AccentPalette.Split(Options.Accent).Lightness;
        set => ApplyAccent(AccentPalette.Join(AccentHue, AccentSaturation, value));
    }

    public bool FollowsWindowsTheme
    {
        get => Options.FollowsWindowsTheme;
        set => Update(options => options with { FollowsWindowsTheme = value });
    }

    public bool Mica
    {
        get => Options.Mica;
        set => Update(options => options with { Mica = value });
    }

    public double TintPercent
    {
        get => Options.TintPercent;
        set => Update(options => options with { TintPercent = (int)Math.Round(value) });
    }

    public double CoverWidth
    {
        get => Options.CoverWidth;
        set => Update(options => options with { CoverWidth = (int)Math.Round(value) });
    }

    public bool CoverTitles
    {
        get => Options.CoverTitles;
        set => Update(options => options with { CoverTitles = value });
    }

    public bool Animations
    {
        get => Options.Animations;
        set => Update(options => options with { Animations = value });
    }

    /// <summary>
    /// Pills and not a drop-down, which is where this page departs from the prototype on purpose.
    /// </summary>
    /// <remarks>
    /// The prototype draws density and rounding as <c>select</c>s. This page already spends pills on
    /// the theme and the language — five and two of them — and a drop-down for the third and fourth
    /// choice on the same page would make the control type mean nothing. Three options is what a
    /// pill row is for; the prototype's own accent row is a pill row for exactly that reason.
    /// </remarks>
    public ICommand ApplyDensityCommand { get; }

    public ICommand ApplyRoundingCommand { get; }

    public InterfaceDensity Density
    {
        get => Options.Density;
        set => Update(options => options with { Density = value });
    }

    public CornerRounding Rounding
    {
        get => Options.Rounding;
        set => Update(options => options with { Rounding = value });
    }

    public string CompactCue => Cue(Density == InterfaceDensity.Compact);

    public string ComfortableCue => Cue(Density == InterfaceDensity.Comfortable);

    public string RoomyCue => Cue(Density == InterfaceDensity.Roomy);

    public string SharpCue => Cue(Rounding == CornerRounding.Sharp);

    public string SoftCue => Cue(Rounding == CornerRounding.Soft);

    public string VeryRoundCue => Cue(Rounding == CornerRounding.VeryRound);

    /// <summary>The minimum and maximum of the cover slider, from the record that owns them.</summary>
    public static double MinimumCoverWidth => AppearanceOptions.MinimumCoverWidth;

    public static double MaximumCoverWidth => AppearanceOptions.MaximumCoverWidth;

    /// <summary>
    /// The player's surface, which is the one appearance row that is not a choice.
    /// </summary>
    /// <remarks>
    /// The prototype writes it as a row with a value and no control, and this application means it
    /// the same way: <c>IThemeService.PlayerThemeVariant</c> is Dark in all four themes, so a light
    /// strip under a dark picture is a seam nobody can turn on.
    /// </remarks>
    public bool PlayerSurfaceIsFixed => _themeService.PlayerThemeVariant == ThemeVariant.Dark;

    private void ApplyTheme(ThemePreference preference)
    {
        _themeService.Apply(preference);

        // The accent is derived against the page it will be drawn on, so a change of theme changes
        // it: the same blue that reads on white does not read on near-black, and high contrast
        // takes its own accent back entirely. Nothing else re-derives — this is the one moment the
        // ground under the colour moves.
        _appearance?.Reapply();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FollowsWindowsTheme)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPreference)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SystemStateCue)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HighContrastLightStateCue)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HighContrastDarkStateCue)));
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

    private static string Cue(bool selected) => selected ? "●" : "○";

    private bool IsAccent(int index) =>
        string.Equals(AccentPalette.Presets[index], Options.Accent, StringComparison.OrdinalIgnoreCase);

    private void ApplyAccent(string accent)
    {
        if (AccentPalette.IsAccent(accent))
        {
            Update(options => options with { Accent = accent });
        }
    }

    /// <summary>
    /// Applies one change to the whole set, and tells the page every row again.
    /// </summary>
    /// <remarks>
    /// Every row rather than the one that moved, because they are not independent: turning the
    /// Windows theme off changes which pill is lit, and a change of theme re-derives the accent, so
    /// a page that only refreshed what was touched would show the rest as it was a moment ago.
    /// </remarks>
    private void Update(Func<AppearanceOptions, AppearanceOptions> change)
    {
        if (_appearance is not { } service)
        {
            return;
        }

        service.Apply(change(service.Current));
        foreach (var name in new[]
        {
            nameof(FollowsWindowsTheme),
            nameof(Mica),
            nameof(TintPercent),
            nameof(CoverWidth),
            nameof(CoverTitles),
            nameof(Animations),
            nameof(Density),
            nameof(Rounding),
            nameof(CompactCue),
            nameof(ComfortableCue),
            nameof(RoomyCue),
            nameof(SharpCue),
            nameof(SoftCue),
            nameof(VeryRoundCue),
            nameof(AccentHex),
            nameof(IsFirstAccent),
            nameof(IsSecondAccent),
            nameof(IsThirdAccent),
            nameof(IsFourthAccent),
            nameof(IsFifthAccent),
            nameof(IsSixthAccent),
            nameof(IsCustomAccent),
            nameof(AccentHue),
            nameof(AccentSaturation),
            nameof(AccentLightness),
            nameof(CurrentPreference),
            nameof(SystemStateCue),
            nameof(LightStateCue),
            nameof(DarkStateCue),
            nameof(HighContrastLightStateCue),
            nameof(HighContrastDarkStateCue),
            nameof(ReducedMotionNoticeKey),
        })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

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

    /// <summary>One command per pill row, taking the value the pill stands for.</summary>
    private sealed class ApplyEnumCommand<T>(Action<T> apply) : ICommand
        where T : struct, Enum
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => parameter is T value && Enum.IsDefined(value);

        public void Execute(object? parameter)
        {
            if (parameter is T value && Enum.IsDefined(value))
            {
                apply(value);
            }
        }
    }

    private sealed class ApplyAccentCommandImplementation(Action<string> apply) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => AccentPalette.IsAccent(parameter as string);

        public void Execute(object? parameter)
        {
            if (parameter is string accent && CanExecute(accent))
            {
                apply(accent);
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
