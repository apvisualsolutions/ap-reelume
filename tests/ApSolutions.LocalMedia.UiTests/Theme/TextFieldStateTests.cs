// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// A text field's states, in all four themes, read off what actually paints them.
/// </summary>
/// <remarks>
/// The fourth type of phase 2, and it is worth three: measured on twelve control types, the
/// <c>TextControl*</c> brushes are taken by the <c>TextBox</c> (25 places), the <c>NumericUpDown</c>
/// (35, since it is a text box with two spinners around it) and the <c>ComboBox</c>'s inner box — and
/// by none of the button family, the checkbox or the slider. One family of resources therefore
/// reaches the 15 text boxes, the 5 numeric fields and part of the 8 combo boxes.
/// </remarks>
// The theme variant is one setting on one application, and these classes all change it. They are
// serialised so that a class reading a theme cannot be reading one another class just replaced.
[Collection("ThemeVariant")]
public sealed class TextFieldStateTests
{
    private const double TextMinimum = 4.5;
    private const double NonTextMinimum = 3.0;

    public static TheoryData<string> Themes() =>
        ["Light", "Dark", "HighContrastLight", "HighContrastDark"];

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_hint_inside_an_empty_field_can_be_read(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        var painted = scene.Read(state: null);
        var fill = ThemeContrast.Painted(painted.Fill, surface);

        // Measured before this existed: 2.11:1 in the light theme. The hint carries alpha twice over
        // — #99000000 in the colour and Opacity 0.5 on the element — which is what took it there, and
        // it is the text that tells you what a field is for while the field is empty.
        var ratio = ThemeContrast.Ratio(
            ThemeContrast.Painted(painted.Placeholder, fill, painted.PlaceholderOpacity),
            fill);
        Assert.True(
            ratio >= TextMinimum,
            $"{themeName}: the hint inside an empty field reads {ratio:F2}:1 against the field.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_switched_off_field_keeps_its_text_and_its_shape(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        var painted = scene.Read(":disabled");
        var fill = ThemeContrast.Painted(painted.Fill, surface);

        // Measured before this existed, in the light theme: the text read 2.56:1 and the outline
        // 2.51:1 against the surface, so a switched-off field was neither readable nor a shape.
        // Disabled text is exempt from WCAG 1.4.3, which is exactly why it goes unmeasured and ends
        // up illegible; it is held to the non-text bar rather than to nothing, as the token matrix
        // already holds it.
        var text = ThemeContrast.Ratio(ThemeContrast.Painted(painted.Text, fill), fill);
        Assert.True(
            text >= NonTextMinimum,
            $"{themeName}: a switched-off field shows its text at {text:F2}:1 against its own fill.");

        var outline = ThemeContrast.Ratio(ThemeContrast.Painted(painted.Border, surface), surface);
        Assert.True(
            outline >= NonTextMinimum,
            $"{themeName}: a switched-off field's outline reads {outline:F2}:1 against the surface, so "
                + "the field has no shape left.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Focus_is_drawn_in_the_theme_focus_colour(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);

        var painted = scene.Read(":focus-visible");

        // Measured: the template painted #0078D7 in all four themes — including the one whose focus
        // colour is #FFFF00. The application style that sets BorderBrush on a focused TextBox does
        // reach the control and the template ignores it, so it was a setter that painted nothing.
        Assert.Equal(
            ThemeContrast.Token(theme, "FocusStrokeBrush"),
            ThemeContrast.Painted(painted.Border, ThemeContrast.Token(theme, "ShellSurfaceBrush")));
    }

    [AvaloniaFact]
    public void High_contrast_paints_a_field_differently_from_the_ordinary_themes()
    {
        foreach (var (ordinary, contrast) in new[]
        {
            ("Light", "HighContrastLight"),
            ("Dark", "HighContrastDark"),
        })
        {
            string a;
            using (var first = new Scene(Resolve(ordinary)))
            {
                a = Show(first.Read(":pointerover"));
            }

            string b;
            using (var second = new Scene(Resolve(contrast)))
            {
                b = Show(second.Read(":pointerover"));
            }

            Assert.True(a != b, $"{contrast} paints a hovered field exactly like {ordinary}: {a}.");
        }
    }

    [AvaloniaFact]
    public void A_control_built_around_a_text_field_is_painted_with_it()
    {
        // One family of resources, more than one type. Measured: a NumericUpDown takes TextControl*
        // in 35 places, because it is a text box with two spinners around it, so this change reaches
        // the tree's 5 numeric fields as well as its 15 text boxes.
        //
        // The ComboBox is not here, and that is measured too: it takes TextControl* only through the
        // inner box it grows when it is editable, and nothing in the tree sets IsEditable — a closed
        // combo box has no PART_BorderElement at all. It gets its own resources when its turn comes.
        var theme = Presentation.Theme.AppThemeVariants.HighContrastDark;
        Avalonia.Application.Current!.RequestedThemeVariant = theme;
        Control control = new NumericUpDown { Width = 180 };
        var window = new Window { Width = 320, Height = 200, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            // Not PART_BorderElement, and that is measured rather than assumed: a NumericUpDown holds
            // two frames, and the inner text box's PART_BorderElement is deliberately transparent so
            // the control does not draw two rectangles one inside the other. What paints is the
            // ButtonSpinner's own border, which is the one a person sees.
            var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");
            var frame = control.GetVisualDescendants()
                .OfType<Border>()
                .FirstOrDefault(b => b.BorderThickness.Left > 0
                    && ThemeContrast.Painted(b.BorderBrush, surface) != surface);
            Assert.True(
                frame is not null,
                "A NumericUpDown draws no frame at all: every border in it is either transparent or "
                    + "the surface's own colour.");
            Assert.Equal(
                ThemeContrast.Token(theme, "ShellBorderBrush"),
                ThemeContrast.Painted(frame.BorderBrush, surface));
        }
        finally
        {
            window.Close();
            Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Default;
        }
    }

    private static string Show(Painted painted) =>
        $"fill={Describe(painted.Fill)} border={Describe(painted.Border)} text={Describe(painted.Text)}";

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

    private readonly record struct Painted(
        IBrush? Fill,
        IBrush? Border,
        IBrush? Text,
        IBrush? Placeholder,
        double PlaceholderOpacity);

    /// <summary>One text field in one theme, with the window it needs and the reset it owes.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly Window _window;
        private readonly TextBox _field;

        public Scene(ThemeVariant theme)
        {
            Avalonia.Application.Current!.RequestedThemeVariant = theme;
            _field = new TextBox { Text = "abc", Width = 180, PlaceholderText = "hint" };
            _window = new Window { Width = 320, Height = 200, Content = _field };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public Painted Read(string? state)
        {
            // :focus-visible, because that is the selector the ring is written with: it answers to
            // the keyboard and not to a pointer, which is what stopped every clicked control from
            // wearing a ring nobody asked for.
            foreach (var candidate in new[] { ":pointerover", ":focus-visible", ":disabled" })
            {
                ((IPseudoClasses)_field.Classes).Set(candidate, candidate == state);
            }

            Dispatcher.UIThread.RunJobs();

            // PART_BorderElement paints the field; the field's own Background and BorderBrush are the
            // resting values in every state, so reading the control measures nothing.
            var border = Require<Border>("PART_BorderElement");
            var presenter = Require<TextPresenter>("PART_TextPresenter");
            var placeholder = Require<TextBlock>("PART_Placeholder");
            return new Painted(
                border.Background,
                border.BorderBrush,
                presenter.Foreground,
                placeholder.Foreground,
                placeholder.Opacity);
        }

        public void Dispose()
        {
            _window.Close();
            Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }

        private T Require<T>(string name)
            where T : Avalonia.Visual
        {
            var found = _field.GetVisualDescendants()
                .OfType<T>()
                .FirstOrDefault(visual => (visual as Avalonia.StyledElement)?.Name == name);
            Assert.True(found is not null, $"The text box template has no {typeof(T).Name} named {name}.");
            return found;
        }
    }
}
