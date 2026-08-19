// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// The other two of phase 2g: a button that stays pressed in, and one option out of several.
/// </summary>
/// <remarks>
/// Two toggle buttons — the two personal actions on a card — and one radio button, the version a
/// duplicate review keeps. Measured before writing anything: a toggle paints from its content
/// presenter exactly as a button does, and a radio button from three ellipses exactly as a checkbox
/// paints from its box. Both said <c>#FF0078D7</c> in all four themes, and the toggle's border was
/// <c>Transparent</c> in all ten of its states, so it had no shape of its own.
///
/// <para>
/// The <c>Indeterminate*</c> family — ten of the toggle's 37 keys — is left alone on purpose:
/// <c>IsThreeState</c> appears nowhere in the tree, so redirecting them would be ten aliases per
/// theme that nothing can reach, which is the defect this repository has a gate for.
/// </para>
/// </remarks>
[Collection("ThemeVariant")]
public sealed class ToggleAndRadioStateTests
{
    private const double TextMinimum = 4.5;
    private const double NonTextMinimum = 3.0;

    public static TheoryData<string> Themes() =>
        ["Light", "Dark", "HighContrastLight", "HighContrastDark"];

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_toggle_that_is_on_is_told_apart_from_one_that_is_off(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new ToggleScene(theme);

        var off = scene.Read(state: null);
        var on = scene.Read(":checked");
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");
        var ratio = ThemeContrast.Ratio(
            ThemeContrast.Painted(on.Fill, surface),
            ThemeContrast.Painted(off.Fill, surface));

        Assert.True(
            ratio >= NonTextMinimum,
            $"{themeName}: a toggle that is on differs from one that is off by {ratio:F2}:1, and "
                + "being on is the only thing it has to say.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_toggle_has_an_outline_of_its_own(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new ToggleScene(theme);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        // Measured before this existed: Transparent in all ten states, so a toggle had no shape and
        // its resting fill was the only thing between it and the page.
        var painted = scene.Read(state: null);
        var fill = ThemeContrast.Painted(painted.Fill, surface);
        var ratio = ThemeContrast.Ratio(ThemeContrast.Painted(painted.Border, fill), fill);

        Assert.True(
            ratio >= NonTextMinimum,
            $"{themeName}: a toggle is outlined at {ratio:F2}:1 against its own fill.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_toggles_label_stays_readable_in_every_state(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new ToggleScene(theme);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        foreach (var state in new string?[] { null, ":pointerover", ":pressed", ":checked", ":disabled" })
        {
            var painted = scene.Read(state);
            var fill = ThemeContrast.Painted(painted.Fill, surface);
            var ratio = ThemeContrast.Ratio(ThemeContrast.Painted(painted.Foreground, fill), fill);

            // Disabled text against the bar this repository already gives it, for the reason recorded
            // in TextFieldStateTests: WCAG exempts it, and the exemption is why it goes unmeasured.
            var bar = state == ":disabled" ? NonTextMinimum : TextMinimum;
            Assert.True(
                ratio >= bar,
                $"{themeName}: a {state ?? "resting"} toggle's label reads {ratio:F2}:1 against the "
                    + $"fill under it, under a bar of {bar:F1}.");
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_switched_off_toggle_does_not_look_like_a_resting_one(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new ToggleScene(theme);

        // The same defect the button had in phase 2a: disabled and resting were byte-identical, and
        // the only thing between them was the grey of the text.
        var resting = scene.Read(state: null);
        var disabled = scene.Read(":disabled");

        // In the two high contrast themes the fill *cannot* say it — the disabled fill, the resting
        // fill and the surface are one colour there — so the assertion is the one ControlStateTests
        // already makes for a button: the fill is the surface, and what says "disabled" is geometry,
        // the dotted outline DisabledOutlineTests measures and which a toggle already takes.
        if (themeName.StartsWith("HighContrast", StringComparison.Ordinal))
        {
            Assert.Equal(
                ThemeContrast.Token(theme, "ShellSurfaceBrush"),
                Assert.IsAssignableFrom<ISolidColorBrush>(disabled.Fill).Color);
            return;
        }

        Assert.NotEqual(Describe(resting.Fill), Describe(disabled.Fill));
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_chosen_option_is_told_apart_from_one_not_chosen(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new RadioScene(theme);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        var chosen = scene.Read(":checked");
        var ratio = ThemeContrast.Ratio(
            ThemeContrast.Painted(chosen.CheckedFill, surface),
            surface);

        Assert.True(
            ratio >= NonTextMinimum,
            $"{themeName}: a chosen option's circle reads {ratio:F2}:1 against the page.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_dot_can_be_seen_inside_the_circle_it_fills(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new RadioScene(theme);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        // Measured before this existed: the dot was White in all four themes, which is the same
        // mistake the checkbox's mark made — a white mark on whatever the accent happens to be. The
        // colour on top of the accent follows the accent's luminance, never the theme's name.
        var chosen = scene.Read(":checked");
        var circle = ThemeContrast.Painted(chosen.CheckedFill, surface);
        var ratio = ThemeContrast.Ratio(ThemeContrast.Painted(chosen.Glyph, circle), circle);

        Assert.True(
            ratio >= NonTextMinimum,
            $"{themeName}: the dot reads {ratio:F2}:1 against the circle it sits in.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void An_option_not_chosen_still_has_a_circle(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new RadioScene(theme);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        var resting = scene.Read(state: null);
        var ratio = ThemeContrast.Ratio(ThemeContrast.Painted(resting.Stroke, surface), surface);

        Assert.True(
            ratio >= NonTextMinimum,
            $"{themeName}: an unchosen option's circle is drawn at {ratio:F2}:1, and if it cannot be "
                + "seen there is nothing to aim at.");
    }

    [AvaloniaFact]
    public void High_contrast_paints_both_differently_from_the_ordinary_themes()
    {
        foreach (var (ordinary, contrast) in new[]
        {
            ("Light", "HighContrastLight"),
            ("Dark", "HighContrastDark"),
        })
        {
            string toggleA, toggleB, radioA, radioB;
            using (var scene = new ToggleScene(Resolve(ordinary)))
            {
                toggleA = Describe(scene.Read(":checked").Fill);
            }

            using (var scene = new ToggleScene(Resolve(contrast)))
            {
                toggleB = Describe(scene.Read(":checked").Fill);
            }

            using (var scene = new RadioScene(Resolve(ordinary)))
            {
                radioA = Describe(scene.Read(":checked").CheckedFill);
            }

            using (var scene = new RadioScene(Resolve(contrast)))
            {
                radioB = Describe(scene.Read(":checked").CheckedFill);
            }

            Assert.True(toggleA != toggleB, $"{contrast} paints a toggle like {ordinary}: {toggleA}.");
            Assert.True(radioA != radioB, $"{contrast} paints a radio like {ordinary}: {radioA}.");
        }
    }

    private static string Describe(IBrush? brush) => brush switch
    {
        null => "null",
        ISolidColorBrush solid => $"{solid.Color}@{solid.Opacity:0.##}",
        _ => brush.GetType().Name,
    };

    private static ThemeVariant Resolve(string name) => name switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        "HighContrastLight" => Presentation.Theme.AppThemeVariants.HighContrastLight,
        "HighContrastDark" => Presentation.Theme.AppThemeVariants.HighContrastDark,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    /// <summary>A toggle paints from its content presenter, exactly as a button does.</summary>
    private sealed class ToggleScene : IDisposable
    {
        private readonly Window _window;
        private readonly ToggleButton _toggle;

        public ToggleScene(ThemeVariant theme)
        {
            Avalonia.Application.Current!.RequestedThemeVariant = theme;
            _toggle = new ToggleButton { Content = "Tag" };
            _window = new Window { Width = 320, Height = 200, Content = _toggle };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public (IBrush? Fill, IBrush? Border, IBrush? Foreground) Read(string? state)
        {
            foreach (var candidate in new[] { ":pointerover", ":pressed", ":checked", ":disabled" })
            {
                ((IPseudoClasses)_toggle.Classes).Set(candidate, candidate == state);
            }

            Dispatcher.UIThread.RunJobs();
            var presenter = _toggle.GetVisualDescendants().OfType<ContentPresenter>().Single();
            return (
                presenter.Background,
                presenter.BorderBrush,
                Avalonia.Controls.Documents.TextElement.GetForeground(presenter));
        }

        public void Dispose()
        {
            _window.Close();
            Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    }

    /// <summary>A radio button paints from three ellipses, as a checkbox paints from its box.</summary>
    private sealed class RadioScene : IDisposable
    {
        private readonly Window _window;
        private readonly RadioButton _radio;

        public RadioScene(ThemeVariant theme)
        {
            Avalonia.Application.Current!.RequestedThemeVariant = theme;
            _radio = new RadioButton { Content = "One" };
            _window = new Window { Width = 320, Height = 200, Content = _radio };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public (IBrush? Fill, IBrush? Stroke, IBrush? CheckedFill, IBrush? Glyph) Read(string? state)
        {
            foreach (var candidate in new[] { ":pointerover", ":pressed", ":checked", ":disabled" })
            {
                ((IPseudoClasses)_radio.Classes).Set(candidate, candidate == state);
            }

            Dispatcher.UIThread.RunJobs();
            var ellipses = _radio.GetVisualDescendants().OfType<Ellipse>().ToList();
            var outer = ellipses.Single(e => e.Name == "OuterEllipse");
            var chosen = ellipses.Single(e => e.Name == "CheckOuterEllipse");
            var glyph = ellipses.Single(e => e.Name == "CheckGlyph");
            return (outer.Fill, outer.Stroke, chosen.Fill, glyph.Fill);
        }

        public void Dispose()
        {
            _window.Close();
            Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    }
}
