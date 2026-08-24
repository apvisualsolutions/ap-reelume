// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Controls.Documents;
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
/// A button's five states, in all four themes, read off what actually paints them.
/// </summary>
/// <remarks>
/// Before these styles the base theme painted all five: a 20 % black fill, a transparent border
/// where the design asks for one pixel of the control boundary, a disabled state identical to rest,
/// and a pointer-over whose fill went solid black under black text. None of it came from a token, so
/// no contrast check ever saw it. Measured on the <c>ContentPresenter</c>, because that is what
/// paints — a <c>Background</c> setter on the button itself does not win.
/// </remarks>
// The theme variant is one setting on one application, and these three classes all change it. They
// are serialised so that a class reading a theme cannot be reading one another class just replaced —
// a race that would only ever show up on some runs, which is the kind this repository keeps finding
// on CI's second pass.
[Collection("ThemeVariant")]
public sealed class ControlStateTests
{
    public static TheoryData<string> Themes() =>
        ["Light", "Dark", "HighContrastLight", "HighContrastDark"];

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Every_state_of_a_button_comes_from_a_token(string themeName)
    {
        var theme = Resolve(themeName);
        Avalonia.Application.Current!.RequestedThemeVariant = theme;
        var button = new Button { Content = "Ok", Width = 120, Height = 36 };
        var window = new Window { Width = 320, Height = 200, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var rest = Read(button, state: null);
            Assert.Equal(Token(theme, "ControlFillBrush"), rest.Background);
            Assert.Equal(Token(theme, "ShellBorderBrush"), rest.Border);
            Assert.Equal(1, rest.Thickness);

            var hover = Read(button, ":pointerover");
            Assert.Equal(Token(theme, "ControlFillHoverBrush"), hover.Background);

            var pressed = Read(button, ":pressed");
            Assert.Equal(Token(theme, "ControlFillPressedBrush"), pressed.Background);

            var disabled = Read(button, ":disabled");
            Assert.Equal(Token(theme, "ControlFillDisabledBrush"), disabled.Background);

            // A disabled control has to look different from a resting one. In light and dark the
            // fill says it. In the two high contrast themes the fill *cannot*: the disabled fill is
            // the surface, by design, because those palettes have no third colour to spend. There the
            // difference is geometry — the dotted outline, which DisabledOutlineTests measures — so
            // this asserts the fill where the fill is the cue, and asserts what the fill actually is
            // where it is not, rather than the assertion being quietly loosened for all four.
            if (theme == ThemeVariant.Light || theme == ThemeVariant.Dark)
            {
                Assert.NotEqual(rest.Background, disabled.Background);
            }
            else
            {
                Assert.Equal(Token(theme, "ShellSurfaceBrush"), disabled.Background);
            }
        }
        finally
        {
            window.Close();
            Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Default;
        }
    }

    /// <summary>
    /// The one button that is the point of its screen looks like it, and behaves like every other.
    /// </summary>
    /// <remarks>
    /// <c>primary-action</c> was a class put on <c>ResumeHeroView</c>'s button and defined by
    /// nobody — no style declared it and no test looked for it — so the button that resumes what you
    /// were watching was painted exactly like every secondary button beside it. The house defect with
    /// a class attribute's face.
    /// <para>
    /// The hierarchy is carried entirely by the resting state, which is when a person looks at a
    /// screen and decides. Hovering and pressing invert exactly as every other control does, because
    /// one grammar of states across the application is worth more than a fourth way of saying
    /// "pressed" — and in the two high contrast themes an accent that stayed put through hover would
    /// be the only control that stopped answering the mouse.
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_primary_action_leads_at_rest_and_answers_like_the_rest(string themeName)
    {
        var theme = Resolve(themeName);
        Avalonia.Application.Current!.RequestedThemeVariant = theme;
        var button = new Button { Content = "Resume", Width = 160, Height = 36 };
        button.Classes.Add("primary-action");
        var window = new Window { Width = 320, Height = 200, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            // Re-declared 2026-08-24: the primary wears its own family - in dark the prototype's
            // light pill - so the accent stays the mark of state and the primary the mark of the
            // one leading action.
            var rest = Read(button, state: null);
            Assert.Equal(Token(theme, "PrimaryActionBrush"), rest.Background);
            Assert.Equal(Token(theme, "PrimaryActionTextBrush"), rest.Foreground);
            Assert.Equal(Token(theme, "PrimaryActionBrush"), rest.Border);

            // It has to lead, and that is a number: the ordinary button's resting fill is what it is
            // being told apart from.
            var ordinary = new Button { Content = "Ok", Width = 120, Height = 36 };
            var second = new Window { Width = 320, Height = 200, Content = ordinary };
            second.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                var ratio = Contrast(rest.Background, Read(ordinary, state: null).Background);
                Assert.True(
                    ratio >= 3.0,
                    $"{themeName}: the primary action differs from an ordinary button by {ratio:F2}:1, "
                        + "so nothing on the screen says which one is the point of it.");
            }
            finally
            {
                second.Close();
            }

            // Re-declared 2026-08-24 with the primary's own family: under the hand it stays the
            // primary - the prototype's light pill brightens rather than falling to the common
            // grammar, because a leading action that dressed down on hover stopped leading exactly
            // when somebody was about to take it. Disabled still falls to the common fill below:
            // a primary that cannot be pressed has nothing left to lead.
            Assert.Equal(Token(theme, "PrimaryActionHoverBrush"), Read(button, ":pointerover").Background);
            Assert.Equal(Token(theme, "PrimaryActionPressedBrush"), Read(button, ":pressed").Background);
            Assert.Equal(Token(theme, "ControlFillDisabledBrush"), Read(button, ":disabled").Background);
            foreach (var state in new[] { ":pointerover", ":pressed" })
            {
                var painted = Read(button, state);
                Assert.Equal(Token(theme, "PrimaryActionTextBrush"), painted.Foreground);
                Assert.True(
                    Contrast(painted.Foreground, painted.Background) >= 4.5,
                    $"{themeName} {state}: the primary action's label stops being readable.");
            }

            // A disabled primary action stops leading, or it would still be shouting for a press it
            // will not accept.
            Assert.Equal(Token(theme, "TextDisabledBrush"), Read(button, ":disabled").Foreground);
        }
        finally
        {
            window.Close();
            Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Default;
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Hovering_and_pressing_keep_the_label_readable(string themeName)
    {
        var theme = Resolve(themeName);
        Avalonia.Application.Current!.RequestedThemeVariant = theme;
        var button = new Button { Content = "Ok", Width = 120, Height = 36 };
        var window = new Window { Width = 320, Height = 200, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            foreach (var state in new[] { ":pointerover", ":pressed" })
            {
                var painted = Read(button, state);
                var ratio = Contrast(painted.Foreground, painted.Background);
                Assert.True(
                    ratio >= 4.5,
                    $"{themeName} {state}: the label reads {ratio:F2}:1 against the fill under it. The "
                        + "base theme's pointer-over used to put black text on a black fill, which is "
                        + "what the token for active text exists to prevent.");
            }
        }
        finally
        {
            window.Close();
            Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Default;
        }
    }

    [AvaloniaFact]
    public void High_contrast_inverts_a_pressed_button_instead_of_tinting_it()
    {
        foreach (var themeName in new[] { "HighContrastLight", "HighContrastDark" })
        {
            var theme = Resolve(themeName);
            Avalonia.Application.Current!.RequestedThemeVariant = theme;
            var button = new Button { Content = "Ok", Width = 120, Height = 36 };
            var window = new Window { Width = 320, Height = 200, Content = button };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            try
            {
                var rest = Read(button, state: null);
                var pressed = Read(button, ":pressed");

                // Inversion, not a tint: the fill becomes what the border was and the text becomes
                // what the surface was. A tint would be invisible in a palette of two colours.
                Assert.Equal(Token(theme, "ShellBorderBrush"), pressed.Background);
                Assert.Equal(Token(theme, "ShellSurfaceBrush"), pressed.Foreground);
                Assert.Equal(rest.Background, pressed.Foreground);

                // The design also asks for a 2 px border while pressed. Decided against, and on
                // purpose: pressing in high contrast already inverts both the fill and the text, at
                // 21:1, so thickness would be a third signal on a state that has two — and the base
                // template keeps one thickness for every state, so it would cost an adorner or a
                // template of our own. Asserted at what it is today so the day it changes is a red.
                Assert.Equal(1, pressed.Thickness);
            }
            finally
            {
                window.Close();
                Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Default;
            }
        }
    }

    /// <summary>
    /// A toggle is a button that stays pressed, and it stands in the same rows as the ordinary ones:
    /// "Favorito" beside "Marcar como visto". The pill geometry is declared on the Button selector,
    /// which a ToggleButton does not match, so it kept the base theme's shorter box with its own
    /// padding — a square corner and a text baseline out of line with the pills beside it.
    /// </summary>
    [AvaloniaFact]
    public void A_toggle_is_the_same_pill_as_the_button_beside_it()
    {
        var toggle = new ToggleButton { Content = "Favorito" };
        var button = new Button { Content = "Marcar como visto" };
        var window = new Window
        {
            Width = 420,
            Height = 200,
            Content = new StackPanel { Children = { toggle, button } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            Assert.Equal(button.CornerRadius, toggle.CornerRadius);
            Assert.Equal(button.MinHeight, toggle.MinHeight);
            Assert.Equal(button.Padding, toggle.Padding);
        }
        finally
        {
            window.Close();
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

    private static Color Token(ThemeVariant theme, string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryGetResource(key, theme, out var value),
            $"{key} is missing from {theme}, so the state below it would fall back to the base theme.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    private static (Color Background, Color Border, Color Foreground, double Thickness) Read(
        Button button,
        string? state)
    {
        foreach (var candidate in new[] { ":pointerover", ":pressed", ":disabled" })
        {
            ((IPseudoClasses)button.Classes).Set(candidate, candidate == state);
        }

        Dispatcher.UIThread.RunJobs();
        var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault();
        Assert.True(presenter is not null, "The button has no content presenter, so nothing paints it.");
        return (
            ColorOf(presenter.Background),
            ColorOf(presenter.BorderBrush),
            ColorOf(TextElement.GetForeground(presenter)),
            presenter.BorderThickness.Top);
    }

    private static Color ColorOf(IBrush? brush) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static double Contrast(Color first, Color second)
    {
        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color color) =>
        Presentation.Theme.HighContrastPolicy.RelativeLuminance(color.R, color.G, color.B);
}
