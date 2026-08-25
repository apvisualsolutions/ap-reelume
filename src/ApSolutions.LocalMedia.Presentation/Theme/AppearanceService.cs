// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Appearance;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;

namespace ApSolutions.LocalMedia.Presentation.Theme;

/// <summary>
/// The nine visual preferences the prototype's Appearance page offers besides the theme.
/// </summary>
public interface IAppearanceService
{
    AppearanceOptions Current { get; }

    /// <summary>Stores the whole set and makes the screen match it.</summary>
    void Apply(AppearanceOptions options);

    /// <summary>Re-derives what depends on the theme. Called when the theme itself changes.</summary>
    void Reapply();

    /// <summary>Whether the Mica backdrop may be put on a window at all.</summary>
    bool WantsBackdrop { get; }
}

/// <summary>
/// Writes the preferences into the resources every view already reads.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here invents a mechanism: <c>FluentThemeService</c> has written <c>MotionDuration</c>
/// into the application's own dictionary since reduced motion was implemented, because that
/// dictionary wins over the merged theme and an animation can only read a resource. This is the same
/// move for eight more values — the accent family, the tint, the gutter, the cover, the two radii
/// and whether the covers carry their titles.
/// </para>
/// <para>
/// <b>The accent is derived rather than stored, and it is left alone in high contrast.</b> A person
/// picking a colour cannot be asked to also meet five contrast ratios, so <c>AccentPalette</c> works
/// them out against the page the accent will be drawn on. High contrast is a need rather than a
/// taste: there the written dictionary wins and this removes its overrides instead of adding to
/// them, which is why they are removed rather than merely not written — a colour left over from the
/// light theme would otherwise survive into it.
/// </para>
/// </remarks>
public sealed class AppearanceService : IAppearanceService
{
    private const string AccentKey = "appearance.accent";
    /// <summary>The store key the backdrop decorator reads; one setting, one place.</summary>
    public const string MicaKey = "appearance.mica";
    private const string TintKey = "appearance.tint";
    private const string DensityKey = "appearance.density";
    private const string CoverKey = "appearance.cover";
    private const string RoundingKey = "appearance.rounding";
    private const string CoverTitlesKey = "appearance.coverTitles";

    /// <summary>The store key the reduced-motion decorator reads; one setting, one place.</summary>
    public const string AnimationsKey = "appearance.animations";

    /// <summary>
    /// Every resource the accent decides, and the tone each one takes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The four tokens are not enough and the owner found it before any gate did: "no cambia todos
    /// los colores de la app — ni slider ni checks ni nada". They were right, and the reason is the
    /// one this repository already has a name for. Fluent's own controls read their own resource
    /// names, and the token file points those at the accent with
    /// <c>&lt;StaticResource ResourceKey="AccentBrush" /&gt;</c> — a <b>static</b> reference, resolved
    /// once when the dictionary loads. Writing AccentBrush afterwards changes the token and reaches
    /// none of the twenty redirections hanging off it.
    /// </para>
    /// <para>
    /// So every redirection is written too. The list is not maintained by hand: <c>AccentTokenTests</c>
    /// reads the same token file, finds every key that redirects to one of the four, and fails if one
    /// of them is missing here — which is what keeps a redirection added next year from quietly not
    /// following the accent.
    /// </para>
    /// </remarks>
    private static readonly (string Key, int Tone)[] AccentResourceKeys =
    [
        ("AccentBrush", Body),
        ("AccentSubtleBrush", Wash),
        ("AccentInkBrush", Ink),
        ("AccentTextBrush", OnBody),
        ("CheckBoxCheckBackgroundFillChecked", Body),
        ("CheckBoxCheckGlyphForegroundChecked", OnBody),
        ("CheckBoxCheckGlyphForegroundUnchecked", OnBody),
        ("ComboBoxBackgroundUnfocused", Wash),
        ("ComboBoxItemBackgroundSelected", Wash),
        ("ComboBoxItemBorderBrushSelected", Body),
        ("ComboBoxItemBorderBrushSelectedPointerOver", Body),
        ("ComboBoxItemBorderBrushSelectedPressed", Body),
        ("RadioButtonCheckGlyphFill", OnBody),
        ("RadioButtonOuterEllipseCheckedFill", Body),
        ("RadioButtonOuterEllipseCheckedStroke", Body),
        ("SliderThumbBackground", Body),
        ("SliderThumbBackgroundPointerOver", Body),
        ("SliderThumbBackgroundPressed", Body),
        ("SliderTrackValueFill", Body),
        ("SliderTrackValueFillPointerOver", Body),
        ("SliderTrackValueFillPressed", Body),
        ("ToggleButtonBackgroundChecked", Body),
        ("ToggleButtonForegroundChecked", OnBody),
    ];

    private const int Body = 0;
    private const int Wash = 1;
    private const int Ink = 2;
    private const int OnBody = 3;

    private readonly Avalonia.Application _application;
    private readonly ISettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly IBackdropService _backdropService;

    /// <summary>
    /// The window on screen, which is an ambient fact rather than one this service can derive.
    /// </summary>
    /// <remarks>
    /// A delegate and not a lookup, for a reason a comment cannot argue away: an application's
    /// lifetime cannot be replaced once it has started, so a host without a desktop lifetime — which
    /// is every automated one — could never reach the two lines that put the material on a window or
    /// take it off. What is passed here in the product is exactly the lookup that used to be written
    /// inline; what is passed in a measurement is a window somebody made.
    /// </remarks>
    private readonly Func<Window?> _liveWindow;

    /// <summary>
    /// The tint the theme declares, read once at construction.
    /// </summary>
    /// <remarks>
    /// The same shape <c>ReadDeclaredMotionDuration</c> has: the number lives in the token file and
    /// this scales it, so a percentage of 100 leaves the interface exactly as it was drawn. Read
    /// once, because after the first write the resource holds this service's own answer.
    /// </remarks>
    private readonly double _declaredTint;

    public AppearanceService(
        Avalonia.Application application,
        ISettingsStore settingsStore,
        IThemeService themeService,
        IBackdropService backdropService,
        Func<Window?>? liveWindow = null)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _backdropService = backdropService ?? throw new ArgumentNullException(nameof(backdropService));
        _liveWindow = liveWindow ?? LiveWindow;
        _declaredTint = Scalar("AccentTintOpacity", 0.156);
        Current = Read();
        Write(Current);
    }

    /// <summary>The resources the accent writes, for the gate that checks none was left out.</summary>
    public static IReadOnlyList<string> AccentResources { get; } =
        [.. AccentResourceKeys.Select(entry => entry.Key)];

    public AppearanceOptions Current { get; private set; }

    public bool WantsBackdrop => Current.Mica;

    public void Apply(AppearanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // The theme is the theme service's, so the toggle writes there rather than keeping a second
        // copy of the same fact: following Windows IS ThemePreference.System, and turning it off has
        // to name the side Windows is on right now — otherwise the screen would not change and the
        // toggle would look broken.
        if (options.FollowsWindowsTheme != Current.FollowsWindowsTheme)
        {
            _themeService.Apply(options.FollowsWindowsTheme
                ? ThemePreference.System
                : _application.ActualThemeVariant == ThemeVariant.Dark
                    ? ThemePreference.Dark
                    : ThemePreference.Light);
        }

        // Clamped here and not only when it is read back, which is the difference between a stored
        // value being repaired and a bad one never being stored: the slider cannot ask for 4000 px,
        // but a caller can, and a cover four thousand pixels wide is a library with one column.
        var motionChanged = options.Animations != Current.Animations;
        Current = options with
        {
            Accent = AccentPalette.IsAccent(options.Accent) ? options.Accent : Current.Accent,
            TintPercent = Math.Clamp(options.TintPercent, 0, 100),
            CoverWidth = Math.Clamp(
                options.CoverWidth,
                AppearanceOptions.MinimumCoverWidth,
                AppearanceOptions.MaximumCoverWidth),
            Density = Enum.IsDefined(options.Density) ? options.Density : Current.Density,
            Rounding = Enum.IsDefined(options.Rounding) ? options.Rounding : Current.Rounding,
        };
        Store(Current);
        Write(Current);

        // Motion is read through IReducedMotionService, and the service that writes MotionDuration
        // is the theme's: re-applying the preference it already holds is what makes it read the
        // decorator again. Only when it changed — re-applying a theme is not free.
        if (motionChanged)
        {
            _themeService.Apply(_themeService.CurrentPreference);
        }
    }

    /// <summary>
    /// Re-reads what the theme decides and paints again.
    /// </summary>
    /// <remarks>
    /// Two things move when the theme does: the accent is derived against the page it is drawn on,
    /// and «follow Windows» IS the system preference rather than a copy of it — so a pill pressed on
    /// the page above changes this row, and reading it from the theme service is what keeps the two
    /// from disagreeing.
    /// </remarks>
    public void Reapply()
    {
        Current = Current with
        {
            FollowsWindowsTheme = _themeService.CurrentPreference == ThemePreference.System,
        };
        Write(Current);
    }

    private AppearanceOptions Read()
    {
        var stored = _settingsStore.Read<string>(AccentKey);
        return new AppearanceOptions
        {
            Accent = AccentPalette.IsAccent(stored) ? stored! : AccentPalette.Presets[0],
            FollowsWindowsTheme = _themeService.CurrentPreference == ThemePreference.System,
            Mica = _settingsStore.Read<bool?>(MicaKey) ?? true,
            TintPercent = Math.Clamp(_settingsStore.Read<int?>(TintKey) ?? 100, 0, 100),
            Density = Defined(_settingsStore.Read<InterfaceDensity?>(DensityKey), InterfaceDensity.Comfortable),
            CoverWidth = Math.Clamp(
                _settingsStore.Read<int?>(CoverKey) ?? AppearanceOptions.DefaultCoverWidth,
                AppearanceOptions.MinimumCoverWidth,
                AppearanceOptions.MaximumCoverWidth),
            Rounding = Defined(_settingsStore.Read<CornerRounding?>(RoundingKey), CornerRounding.Soft),
            CoverTitles = _settingsStore.Read<bool?>(CoverTitlesKey) ?? true,
            Animations = _settingsStore.Read<bool?>(AnimationsKey) ?? true,
        };
    }

    private static T Defined<T>(T? stored, T fallback)
        where T : struct, Enum =>
        stored is { } value && Enum.IsDefined(value) ? value : fallback;

    private void Store(AppearanceOptions options)
    {
        _settingsStore.Write(AccentKey, options.Accent);
        _settingsStore.Write<bool?>(MicaKey, options.Mica);
        _settingsStore.Write<int?>(TintKey, options.TintPercent);
        _settingsStore.Write<InterfaceDensity?>(DensityKey, options.Density);
        _settingsStore.Write<int?>(CoverKey, options.CoverWidth);
        _settingsStore.Write<CornerRounding?>(RoundingKey, options.Rounding);
        _settingsStore.Write<bool?>(CoverTitlesKey, options.CoverTitles);
        _settingsStore.Write<bool?>(AnimationsKey, options.Animations);
    }

    private void Write(AppearanceOptions options)
    {
        WriteAccent(options.Accent);
        _application.Resources["AccentTintOpacity"] = _declaredTint * options.TintPercent / 100.0;

        var gutter = options.Density switch
        {
            InterfaceDensity.Compact => 4.0,
            InterfaceDensity.Roomy => 16.0,
            _ => 8.0,
        };
        _application.Resources["DensityGutter"] = gutter;
        _application.Resources["PosterCardPadding"] = new Thickness(gutter);

        // 2:3 exactly, which is what the card is drawn at and what the token file says in the two
        // numbers it declares: a width chosen without its height would stretch every cover.
        _application.Resources["PosterCardWidth"] = (double)options.CoverWidth;
        _application.Resources["PosterCardHeight"] = Math.Round(options.CoverWidth * 1.5);

        var radius = options.Rounding switch
        {
            CornerRounding.Sharp => 4.0,
            CornerRounding.VeryRound => 18.0,
            _ => 10.0,
        };

        // The small radius keeps its proportion to the medium rather than being a second choice:
        // the scale this tree gates is two radii, and the prototype offers one control over both.
        _application.Resources["CornerRadiusMedium"] = new CornerRadius(radius);
        _application.Resources["CornerRadiusSmall"] = new CornerRadius(Math.Round(radius / 2));
        _application.Resources["CoverTitlesVisible"] = options.CoverTitles;
        WriteBackdrop(options.Mica);
    }

    /// <summary>
    /// Puts the Mica material on the window, or takes it off, while the application is running.
    /// </summary>
    /// <remarks>
    /// The backdrop is decided when a window is created — that is where <c>TryApplyBackdrop</c> is
    /// called from — so a preference that only reached the next launch would be a switch that did
    /// nothing when pressed. Turning it off writes the level directly rather than asking the service
    /// for its opposite: a service that could only add is what the interface says, and inventing a
    /// second method for one caller would be a wider port for a narrower need.
    /// </remarks>
    private void WriteBackdrop(bool mica)
    {
        if (_liveWindow() is not { } window)
        {
            return;
        }

        if (mica)
        {
            _ = _backdropService.TryApply(window);
            return;
        }

        window.TransparencyLevelHint = [WindowTransparencyLevel.None];
    }

    /// <summary>The main window of a desktop application, and nothing at all anywhere else.</summary>
    private Window? LiveWindow() =>
        (_application.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private void WriteAccent(string accent)
    {
        // The two high contrast dictionaries decide their own accent, and it is not a taste: their
        // pair is pure blue on white and pure cyan on black. Removing the overrides is what gives
        // them back — leaving them unwritten would keep whatever the light theme last wrote.
        if (_application.ActualThemeVariant == AppThemeVariants.HighContrastLight
            || _application.ActualThemeVariant == AppThemeVariants.HighContrastDark)
        {
            foreach (var (key, _) in AccentResourceKeys)
            {
                _ = _application.Resources.Remove(key);
            }

            return;
        }

        var tones = AccentPalette.Derive(accent, Colour("ShellSurfaceBrush"), Colour("FocusStrokeBrush"));
        foreach (var (key, tone) in AccentResourceKeys)
        {
            _application.Resources[key] = Brush(tone switch
            {
                Wash => tones.Subtle,
                Ink => tones.Ink,
                OnBody => tones.Text,
                _ => tones.Accent,
            });
        }
    }

    /// <summary>
    /// The colour a token resolves to right now, as <c>#RRGGBB</c>.
    /// </summary>
    /// <remarks>
    /// Read from the dictionary rather than written here, for the reason the whole token file
    /// exists: a page colour named in two places disagrees with itself the first time one of them
    /// moves. The fallback is a host with no theme merged in, which is how several suites mount a
    /// single view.
    /// </remarks>
    private string Colour(string key) =>
        _application.TryFindResource(key, _application.ActualThemeVariant, out var value)
        && value is ISolidColorBrush brush
            ? $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}"
            : "#FBFCFE";

    private double Scalar(string key, double fallback) =>
        _application.TryFindResource(key, _application.ActualThemeVariant, out var value) && value is double number
            ? number
            : fallback;

    private static SolidColorBrush Brush(string hex) => new(Color.Parse(hex));
}

/// <summary>
/// The Mica material, but only while the Appearance page allows it.
/// </summary>
/// <remarks>
/// A decorator for the reason the reduced-motion one is: the backdrop is asked for when a window is
/// created, long before Settings exists, and a window that ignored the stored preference until
/// somebody opened the page would come up wrong on every launch. The preference is read from the
/// store rather than from <see cref="AppearanceService"/> so nothing has to be built before the
/// theme.
/// </remarks>
public sealed class PreferredBackdropService(IBackdropService inner, ISettingsStore settingsStore)
    : IBackdropService
{
    private readonly IBackdropService _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly ISettingsStore _settingsStore =
        settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));

    public bool TryApply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (_settingsStore.Read<bool?>(AppearanceService.MicaKey) == false)
        {
            window.TransparencyLevelHint = [WindowTransparencyLevel.None];
            return false;
        }

        return _inner.TryApply(window);
    }
}

/// <summary>
/// Windows' reduced motion, or the person's own switch: either one turns the animations off.
/// </summary>
/// <remarks>
/// A decorator and not a second service, because everything downstream already asks one question —
/// "is motion reduced" — and two answers to it would disagree. The preference is read from the
/// store rather than from <see cref="AppearanceService"/> so that nothing has to be constructed
/// before the theme, which is what a reference in the other direction would require.
/// </remarks>
public sealed class UserReducedMotionService(IReducedMotionService system, ISettingsStore settingsStore)
    : IReducedMotionService
{
    private readonly IReducedMotionService _system = system ?? throw new ArgumentNullException(nameof(system));
    private readonly ISettingsStore _settingsStore =
        settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));

    public bool IsEnabled =>
        _system.IsEnabled || _settingsStore.Read<bool?>(AppearanceService.AnimationsKey) == false;
}
