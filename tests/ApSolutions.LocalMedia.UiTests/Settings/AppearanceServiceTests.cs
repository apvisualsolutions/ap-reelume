// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Appearance;
using ApSolutions.LocalMedia.Presentation.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The nine appearance preferences, measured on the resources the views actually read.
/// </summary>
/// <remarks>
/// A preference that is stored and changes nothing on screen is the defect this repository has a
/// name for, so nothing here asserts that a value came back out of the record: each one is checked
/// against the resource the interface reads it from — the accent brushes, the tint the glow spends,
/// the gutter the grid counts columns with, the cover the card is drawn at, the two radii, and the
/// flag the caption under a cover is bound to.
/// </remarks>
public sealed class AppearanceServiceTests
{
    [AvaloniaFact]
    public void A_profile_with_nothing_stored_renders_what_this_tree_always_drew()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ResourceScope(application);
        var service = Build(new InMemoryStore());

        Assert.Equal(AccentPalette.Presets[0], service.Current.Accent);
        Assert.True(service.Current.Mica);
        Assert.True(service.WantsBackdrop);
        Assert.Equal(100, service.Current.TintPercent);
        Assert.Equal(InterfaceDensity.Comfortable, service.Current.Density);
        Assert.Equal(AppearanceOptions.DefaultCoverWidth, service.Current.CoverWidth);
        Assert.Equal(CornerRounding.Soft, service.Current.Rounding);
        Assert.True(service.Current.CoverTitles);
        Assert.True(service.Current.Animations);

        // 148 x 222 is what the card is declared at, and the gutter is the 8 the grid counts with.
        Assert.Equal(148d, application.Resources["PosterCardWidth"]);
        Assert.Equal(222d, application.Resources["PosterCardHeight"]);
        Assert.Equal(8d, application.Resources["DensityGutter"]);
        Assert.Equal(new Thickness(8), application.Resources["PosterCardPadding"]);
        Assert.Equal(new CornerRadius(10), application.Resources["CornerRadiusMedium"]);
        Assert.Equal(true, application.Resources["CoverTitlesVisible"]);
    }

    [AvaloniaFact]
    public void Every_preference_reaches_the_resource_the_interface_reads_it_from()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ResourceScope(application);
        var service = Build(new InMemoryStore());

        service.Apply(service.Current with
        {
            TintPercent = 50,
            Density = InterfaceDensity.Roomy,
            CoverWidth = 200,
            Rounding = CornerRounding.VeryRound,
            CoverTitles = false,
        });

        // The tint is a percentage of the number the token file declares, not a number of its own:
        // 0.156 at 100 %, half of it at 50.
        Assert.Equal(0.078, (double)application.Resources["AccentTintOpacity"]!, 3);
        Assert.Equal(16d, application.Resources["DensityGutter"]);
        Assert.Equal(new Thickness(16), application.Resources["PosterCardPadding"]);
        Assert.Equal(200d, application.Resources["PosterCardWidth"]);
        Assert.Equal(300d, application.Resources["PosterCardHeight"]);
        Assert.Equal(new CornerRadius(18), application.Resources["CornerRadiusMedium"]);
        Assert.Equal(new CornerRadius(9), application.Resources["CornerRadiusSmall"]);
        Assert.Equal(false, application.Resources["CoverTitlesVisible"]);

        // And what the slider cannot ask for is clamped rather than drawn: a caller can hand over
        // 4000 px, and a cover four thousand pixels wide is a library with one column.
        service.Apply(service.Current with { CoverWidth = 4000, TintPercent = 500 });
        Assert.Equal(AppearanceOptions.MaximumCoverWidth, service.Current.CoverWidth);
        Assert.Equal(100, service.Current.TintPercent);
        service.Apply(service.Current with { CoverWidth = 1, TintPercent = -20 });
        Assert.Equal(AppearanceOptions.MinimumCoverWidth, service.Current.CoverWidth);
        Assert.Equal(0, service.Current.TintPercent);

        // The same for a name no enum has, which is what a hand-edited settings file hands over.
        service.Apply(service.Current with { Density = (InterfaceDensity)77, Rounding = (CornerRounding)77 });
        Assert.Equal(InterfaceDensity.Roomy, service.Current.Density);
        Assert.Equal(CornerRounding.VeryRound, service.Current.Rounding);
    }

    [AvaloniaFact]
    public void The_accent_family_is_derived_and_every_one_of_its_four_tones_is_written()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ResourceScope(application);
        var service = Build(new InMemoryStore());

        service.Apply(service.Current with { Accent = "#B23A48" });

        var accent = Hex(application, "AccentBrush");
        var subtle = Hex(application, "AccentSubtleBrush");
        var ink = Hex(application, "AccentInkBrush");
        var text = Hex(application, "AccentTextBrush");
        var surface = Hex(application, "ShellSurfaceBrush");

        Assert.True(AccentPalette.Contrast(accent, surface) >= 3.0);
        Assert.True(AccentPalette.Contrast(text, accent) >= 4.5);
        Assert.True(AccentPalette.Contrast(ink, subtle) >= 4.5);
        Assert.True(AccentPalette.Contrast(ink, surface) >= 4.5);

        // A colour that is not a colour is refused rather than written, so a broken stored value
        // cannot leave the interface painted with nothing.
        service.Apply(service.Current with { Accent = "not a colour" });
        Assert.Equal("#B23A48", service.Current.Accent);
    }

    [AvaloniaFact]
    public void High_contrast_takes_its_own_accent_back_rather_than_keeping_the_last_one_written()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ResourceScope(application);
        var service = Build(new InMemoryStore());
        service.Apply(service.Current with { Accent = "#B23A48" });
        Assert.True(application.Resources.ContainsKey("AccentBrush"));

        // High contrast is a need rather than a taste: its pair is decided in the dictionary, and
        // what this has to do is get out of the way — leaving the override in place would paint the
        // light theme's derived colour over it.
        var previous = application.RequestedThemeVariant;
        application.RequestedThemeVariant = AppThemeVariants.HighContrastDark;
        service.Reapply();
        Assert.False(application.Resources.ContainsKey("AccentBrush"));
        Assert.False(application.Resources.ContainsKey("AccentInkBrush"));
        application.RequestedThemeVariant = previous;
    }

    [AvaloniaFact]
    public void Turning_the_animations_off_is_the_same_switch_windows_owns()
    {
        var store = new InMemoryStore();
        var application = Avalonia.Application.Current!;
        using var scope = new ResourceScope(application);
        var motion = new UserReducedMotionService(new NeverReduced(), store);
        Assert.False(motion.IsEnabled);

        var service = Build(store);
        service.Apply(service.Current with { Animations = false });

        // One question, one answer: everything downstream asks the reduced-motion service, and the
        // person's switch is a second source behind that one interface rather than a second service.
        Assert.True(motion.IsEnabled);
        Assert.True(new UserReducedMotionService(new AlwaysReduced(), store).IsEnabled);

        service.Apply(service.Current with { Animations = true });
        Assert.False(motion.IsEnabled);

        // And Windows still wins on its own: the person's switch can only add to it.
        Assert.True(new UserReducedMotionService(new AlwaysReduced(), store).IsEnabled);
    }

    [AvaloniaFact]
    public void Following_the_windows_theme_is_the_system_preference_and_not_a_copy_of_it()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ResourceScope(application);
        var theme = new RecordingTheme();
        var service = new AppearanceService(application, new InMemoryStore(), theme, new NoBackdrop());
        Assert.True(service.Current.FollowsWindowsTheme);

        service.Apply(service.Current with { FollowsWindowsTheme = false });
        Assert.Contains(theme.Applied, preference => preference is ThemePreference.Light or ThemePreference.Dark);

        theme.Preference = ThemePreference.Dark;
        service.Reapply();
        Assert.False(service.Current.FollowsWindowsTheme);

        theme.Preference = ThemePreference.System;
        service.Reapply();
        Assert.True(service.Current.FollowsWindowsTheme);
    }

    [AvaloniaFact]
    public void What_is_stored_is_what_comes_back_on_the_next_launch()
    {
        var application = Avalonia.Application.Current!;
        using var scope = new ResourceScope(application);
        var store = new InMemoryStore();
        Build(store).Apply(new AppearanceOptions
        {
            Accent = "#0E7490",
            Mica = false,
            TintPercent = 25,
            Density = InterfaceDensity.Compact,
            CoverWidth = 120,
            Rounding = CornerRounding.Sharp,
            CoverTitles = false,
            Animations = false,
        });

        var reopened = Build(store);
        Assert.Equal("#0E7490", reopened.Current.Accent);
        Assert.False(reopened.Current.Mica);
        Assert.False(reopened.WantsBackdrop);
        Assert.Equal(25, reopened.Current.TintPercent);
        Assert.Equal(InterfaceDensity.Compact, reopened.Current.Density);
        Assert.Equal(120, reopened.Current.CoverWidth);
        Assert.Equal(CornerRounding.Sharp, reopened.Current.Rounding);
        Assert.False(reopened.Current.CoverTitles);
        Assert.False(reopened.Current.Animations);

        // A stored value that is not one of the names is the shape a hand-edited settings file has,
        // and it answers with the default rather than with an enum nothing can draw.
        store.Write<InterfaceDensity?>("appearance.density", (InterfaceDensity)77);
        store.Write<CornerRounding?>("appearance.rounding", (CornerRounding)77);
        store.Write("appearance.accent", "nonsense");
        var repaired = Build(store);
        Assert.Equal(InterfaceDensity.Comfortable, repaired.Current.Density);
        Assert.Equal(CornerRounding.Soft, repaired.Current.Rounding);
        Assert.Equal(AccentPalette.Presets[0], repaired.Current.Accent);
    }

    [AvaloniaFact]
    public void The_backdrop_is_asked_for_only_while_the_preference_allows_it()
    {
        var store = new InMemoryStore();
        var inner = new CountingBackdrop();
        var backdrop = new PreferredBackdropService(inner, store);
        var window = new Window();

        Assert.True(backdrop.TryApply(window));
        Assert.Equal(1, inner.Applied);

        // Off, the window is told what it is instead of being left as the last one was: a decorator
        // that only declined would leave Mica standing from whichever window asked before it.
        store.Write<bool?>(AppearanceService.MicaKey, false);
        Assert.False(backdrop.TryApply(window));
        Assert.Equal(1, inner.Applied);
        Assert.Equal([WindowTransparencyLevel.None], window.TransparencyLevelHint);

        _ = Assert.Throws<ArgumentNullException>(() => backdrop.TryApply(null!));
    }

    [AvaloniaFact]
    public void Nothing_is_built_without_what_it_needs()
    {
        var application = Avalonia.Application.Current!;
        var store = new InMemoryStore();
        var theme = new RecordingTheme();
        var backdrop = new NoBackdrop();
        _ = Assert.Throws<ArgumentNullException>(() => new AppearanceService(null!, store, theme, backdrop));
        _ = Assert.Throws<ArgumentNullException>(() => new AppearanceService(application, null!, theme, backdrop));
        _ = Assert.Throws<ArgumentNullException>(() => new AppearanceService(application, store, null!, backdrop));
        _ = Assert.Throws<ArgumentNullException>(() => new AppearanceService(application, store, theme, null!));
        _ = Assert.Throws<ArgumentNullException>(() => new UserReducedMotionService(null!, store));
        _ = Assert.Throws<ArgumentNullException>(() => new UserReducedMotionService(new NeverReduced(), null!));
        _ = Assert.Throws<ArgumentNullException>(() => new PreferredBackdropService(null!, store));
        _ = Assert.Throws<ArgumentNullException>(() => new PreferredBackdropService(new NoBackdrop(), null!));

        using var scope = new ResourceScope(application);
        _ = Assert.Throws<ArgumentNullException>(() => Build(store).Apply(null!));
    }

    private static AppearanceService Build(ISettingsStore store) => new(
        Avalonia.Application.Current!,
        store,
        new RecordingTheme(),
        new NoBackdrop());

    private static string Hex(Avalonia.Application application, string key) =>
        application.TryFindResource(key, application.ActualThemeVariant, out var value)
            && value is ISolidColorBrush brush
                ? $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}"
                : throw new InvalidOperationException($"{key} did not resolve to a colour.");

    /// <summary>
    /// Puts the application's own dictionary back the way it was found.
    /// </summary>
    /// <remarks>
    /// These tests write into the resources of the one application every suite in this assembly
    /// shares, so a run that left an accent behind would decide the colours of whatever ran next.
    /// </remarks>
    private sealed class ResourceScope : IDisposable
    {
        private static readonly string[] Keys =
        [
            "AccentBrush",
            "AccentSubtleBrush",
            "AccentInkBrush",
            "AccentTextBrush",
            "AccentTintOpacity",
            "DensityGutter",
            "PosterCardPadding",
            "PosterCardWidth",
            "PosterCardHeight",
            "CornerRadiusMedium",
            "CornerRadiusSmall",
            "CoverTitlesVisible",
        ];

        private readonly Avalonia.Application _application;
        private readonly Dictionary<string, object?> _before = [];

        public ResourceScope(Avalonia.Application application)
        {
            _application = application;
            foreach (var key in Keys)
            {
                if (application.Resources.TryGetValue(key, out var value))
                {
                    _before[key] = value;
                }
            }
        }

        public void Dispose()
        {
            foreach (var key in Keys)
            {
                _ = _application.Resources.Remove(key);
                if (_before.TryGetValue(key, out var value))
                {
                    _application.Resources[key] = value;
                }
            }
        }
    }

    private sealed class InMemoryStore : ISettingsStore
    {
        private readonly Dictionary<string, object?> _values = [];

        public T? Read<T>(string key) =>
            _values.TryGetValue(key, out var value) && value is T typed ? typed : default;

        public void Write<T>(string key, T value) => _values[key] = value;
    }

    private sealed class RecordingTheme : IThemeService
    {
        public List<ThemePreference> Applied { get; } = [];

        public ThemePreference Preference { get; set; } = ThemePreference.System;

        public ThemePreference CurrentPreference => Preference;

        public ThemeVariant PlayerThemeVariant => ThemeVariant.Dark;

        public bool AnimationsEnabled => true;

        public TimeSpan MotionDuration => TimeSpan.FromMilliseconds(160);

        public void Apply(ThemePreference preference)
        {
            Applied.Add(preference);
            Preference = preference;
        }

        public bool TryApplyBackdrop(Window window) => false;
    }

    private sealed class NoBackdrop : IBackdropService
    {
        public bool TryApply(Window window) => false;
    }

    private sealed class CountingBackdrop : IBackdropService
    {
        public int Applied { get; private set; }

        public bool TryApply(Window window)
        {
            Applied++;
            return true;
        }
    }

    private sealed class NeverReduced : IReducedMotionService
    {
        public bool IsEnabled => false;
    }

    private sealed class AlwaysReduced : IReducedMotionService
    {
        public bool IsEnabled => true;
    }
}
