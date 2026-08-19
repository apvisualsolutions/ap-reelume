// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// A checkbox's states, in all four themes, read off what actually paints them.
/// </summary>
/// <remarks>
/// The second type of phase 2, after the button, by measured use: the views hold 18 checkboxes. It is
/// not a second button. A button consumes twelve theme resources; a checkbox consumes seventy-three,
/// in six families across three checked states and four pointer states — and of those six, two paint
/// the whole control (transparent, by design: a checkbox sits on the surface it is on) and four paint
/// the box, the mark and the label.
///
/// Nothing in the tree sets <c>IsThreeState</c>, so the indeterminate third is unreachable and is
/// left where it is rather than pointed at tokens nobody can see.
/// </remarks>
// The theme variant is one setting on one application, and these classes all change it. They are
// serialised so that a class reading a theme cannot be reading one another class just replaced.
[Collection("ThemeVariant")]
public sealed class CheckBoxStateTests
{
    private const double TextMinimum = 4.5;
    private const double NonTextMinimum = 3.0;

    public static TheoryData<string> Themes() =>
        ["Light", "Dark", "HighContrastLight", "HighContrastDark"];

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_checked_box_that_is_switched_off_keeps_its_mark_readable(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme, isChecked: true);

        var painted = scene.Read(":disabled");

        // Measured before this test existed: in the light theme the mark stayed White over the grey
        // that #33000000 leaves on the surface — 1.68:1. The mark is the whole information a checkbox
        // carries, so losing it loses the control's state rather than its decoration.
        //
        // The bar is the non-text one, because a tick is a graphic and not a letter. It was written
        // at 4.5 first and lowered on measuring, which is worth saying plainly: nothing was rescued
        // by it — today's 1.68:1 fails either bar, and the mapping that replaces it clears 3:1 by
        // 4.26 at its narrowest. What changed is that the bar now matches what is being measured.
        //
        // Composited rather than read raw: see ThemeContrast, which is where that arithmetic lives
        // for every theme test, because two of them measuring the same thing differently drift.
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");
        var fill = ThemeContrast.Painted(new SolidColorBrush(painted.BoxFill), surface);
        var ratio = ThemeContrast.Ratio(ThemeContrast.Painted(new SolidColorBrush(painted.Glyph), fill), fill);
        Assert.True(
            ratio >= NonTextMinimum,
            $"{themeName}: a checked, switched-off box shows its mark at {ratio:F2}:1 against the fill "
                + "under it, so the one thing the control says cannot be read.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void An_empty_box_can_be_seen_in_every_state(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme, isChecked: false);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        foreach (var state in new string?[] { null, ":pointerover", ":pressed", ":disabled" })
        {
            var painted = scene.Read(state);

            // Either half may carry it, and which one does is the theme's business. Ordinarily it is
            // the outline over a fill close to the surface; in high contrast, hovering and pressing
            // invert, so the box becomes solid and its outline vanishes into it. Asking only about
            // the outline would call that inversion a failure, when it is the clearest state of the
            // four. Measured before this: the disabled outline read 2.83:1.
            var ratio = Math.Max(
                ThemeContrast.Ratio(ThemeContrast.Painted(new SolidColorBrush(painted.BoxStroke), surface), surface),
                ThemeContrast.Ratio(ThemeContrast.Painted(new SolidColorBrush(painted.BoxFill), surface), surface));
            Assert.True(
                ratio >= NonTextMinimum,
                $"{themeName} {state ?? ":rest"}: the box reads {ratio:F2}:1 against the surface by "
                    + "its outline and by its fill alike, so there is nothing there to tick.");
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_checked_box_is_filled_with_the_theme_accent(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme, isChecked: true);

        var painted = scene.Read(state: null);

        // Measured: #0078D7 in all four themes — Windows 10's blue, which is nobody's token here and
        // in the two high contrast palettes is not the accent either (#0000FF and #00FFFF).
        Assert.Equal(
            ThemeContrast.Token(theme, "AccentBrush"),
            ThemeContrast.Painted(new SolidColorBrush(painted.BoxFill), ThemeContrast.Token(theme, "ShellSurfaceBrush")));
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_label_comes_from_a_token(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme, isChecked: false);

        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");
        Assert.Equal(ThemeContrast.Token(theme, "TextPrimaryBrush"), ThemeContrast.Painted(new SolidColorBrush(scene.Read(state: null).Label), surface));
        Assert.Equal(ThemeContrast.Token(theme, "TextDisabledBrush"), ThemeContrast.Painted(new SolidColorBrush(scene.Read(":disabled").Label), surface));
    }

    [AvaloniaFact]
    public void High_contrast_paints_a_checkbox_differently_from_the_ordinary_themes()
    {
        // Measured before this test existed: Light and HighContrastLight painted a checkbox
        // identically, and so did Dark and HighContrastDark. Nothing of this project's reached it, so
        // switching Windows into high contrast changed every control but this one.
        foreach (var (ordinary, contrast) in new[]
        {
            ("Light", "HighContrastLight"),
            ("Dark", "HighContrastDark"),
        })
        {
            Painted a;
            using (var first = new Scene(Resolve(ordinary), isChecked: false))
            {
                a = first.Read(":pointerover");
            }

            Painted b;
            using (var second = new Scene(Resolve(contrast), isChecked: false))
            {
                b = second.Read(":pointerover");
            }

            Assert.True(
                a != b,
                $"{contrast} paints a hovered checkbox exactly like {ordinary}: "
                    + $"fill={a.BoxFill} stroke={a.BoxStroke} label={a.Label}.");
        }
    }

    private static ThemeVariant Resolve(string name) => name switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        "HighContrastLight" => Presentation.Theme.AppThemeVariants.HighContrastLight,
        "HighContrastDark" => Presentation.Theme.AppThemeVariants.HighContrastDark,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private readonly record struct Painted(Color BoxFill, Color BoxStroke, Color Glyph, Color Label);

    /// <summary>One checkbox in one theme, with the window it needs and the reset it owes.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly Window _window;
        private readonly CheckBox _box;

        public Scene(ThemeVariant theme, bool isChecked)
        {
            Avalonia.Application.Current!.RequestedThemeVariant = theme;
            _box = new CheckBox { Content = "Tick", IsChecked = isChecked };
            _window = new Window { Width = 320, Height = 200, Content = _box };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public Painted Read(string? state)
        {
            foreach (var candidate in new[] { ":pointerover", ":pressed", ":disabled" })
            {
                ((IPseudoClasses)_box.Classes).Set(candidate, candidate == state);
            }

            Dispatcher.UIThread.RunJobs();

            // The box is the template's NormalRectangle, the mark its CheckGlyph and the label the
            // content presenter. Named parts, because a checkbox's own Background paints the whole
            // control and says nothing about the box.
            var box = Require<Border>("NormalRectangle");
            var glyph = Require<Avalonia.Controls.Shapes.Path>("CheckGlyph");
            var label = _box.GetVisualDescendants().OfType<ContentPresenter>().First();
            return new Painted(
                ColorOf(box.Background),
                ColorOf(box.BorderBrush),
                ColorOf(glyph.Fill),
                ColorOf(TextElement.GetForeground(label)));
        }

        public void Dispose()
        {
            _window.Close();
            Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }

        private static Color ColorOf(IBrush? brush) =>
            Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

        private T Require<T>(string name)
            where T : Avalonia.Visual
        {
            var found = _box.GetVisualDescendants()
                .OfType<T>()
                .FirstOrDefault(visual => (visual as Avalonia.StyledElement)?.Name == name);
            Assert.True(found is not null, $"The checkbox template has no {typeof(T).Name} named {name}.");
            return found;
        }
    }
}
