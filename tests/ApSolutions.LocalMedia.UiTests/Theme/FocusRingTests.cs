// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// The focus ring is two rings, and this asserts that both of them reach the screen.
/// </summary>
/// <remarks>
/// Focus used to be drawn by lending the control its own border the focus colour. Two holes were
/// measured in that: a <c>Slider</c> has no border to draw on, and in high contrast light the border
/// and the focus colour are the same black, so focus changed a pixel of thickness and nothing a
/// person could see. The ring is now an adorner of two concentric borders — a geometry cue, which
/// survives a palette of one colour.
/// </remarks>
// The theme variant is one setting on one application, and these three classes all change it. They
// are serialised so that a class reading a theme cannot be reading one another class just replaced —
// a race that would only ever show up on some runs, which is the kind this repository keeps finding
// on CI's second pass.
[Collection("ThemeVariant")]
public sealed class FocusRingTests
{
    [AvaloniaFact]
    public void Keyboard_focus_draws_two_concentric_rings_over_the_control()
    {
        var button = new Button { Content = "Ok", Width = 120, Height = 36 };
        var window = ShowFocused(button);

        var (outer, inner) = RequireRings(button);

        Assert.Equal(2, outer.BorderThickness.Top);
        Assert.Equal(1, inner.BorderThickness.Top);
        Assert.NotEqual(ColorOf(outer.BorderBrush), ColorOf(inner.BorderBrush));
        window.Close();
    }

    [AvaloniaFact]
    public void The_ring_takes_its_colours_from_the_theme_in_force()
    {
        var button = new Button { Content = "Ok", Width = 120, Height = 36 };
        Avalonia.Application.Current!.RequestedThemeVariant = HighContrastDark();
        var window = ShowFocused(button);

        var (outer, inner) = RequireRings(button);

        // Yellow outside, the surface's own black inside: in this theme the ring is the only thing
        // on screen that is neither black nor white.
        Assert.Equal(Color.Parse("#FFFF00"), ColorOf(outer.BorderBrush));
        Assert.Equal(Color.Parse("#000000"), ColorOf(inner.BorderBrush));
        window.Close();
        Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Default;
    }

    [AvaloniaFact]
    public void Every_control_type_that_takes_focus_gets_the_ring()
    {
        // The list is the one the tokens name. A type left out of it falls back to whatever the base
        // theme draws, which no contrast check covers — which is the hole this closes.
        var controls = new Control[]
        {
            new Button { Content = "Ok" },
            new ToggleButton { Content = "Toggle" },
            new ToggleSwitch(),
            new RadioButton { Content = "Pick" },
            new TextBox(),
            new ComboBox(),
            new CheckBox { Content = "Tick" },
            new Slider { Width = 120 },
            new NumericUpDown(),
            new ListBoxItem { Content = "Row" },
        };

        foreach (var control in controls)
        {
            var window = ShowFocused(control);
            var (outer, inner) = RequireRings(control);
            Assert.Equal(2, outer.BorderThickness.Top);
            Assert.Equal(1, inner.BorderThickness.Top);
            window.Close();
        }
    }

    private static ThemeVariant HighContrastDark() =>
        Presentation.Theme.AppThemeVariants.HighContrastDark;

    private static Window ShowFocused(Control control)
    {
        var window = new Window { Width = 320, Height = 200, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        // The tab key, rather than a call that says "take focus": a NumericUpDown passes the keyboard
        // on to the TextBox inside it, and what the ring depends on is *how* the focus arrived. Only
        // the key press carries that.
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
        if (!control.IsKeyboardFocusWithin)
        {
            control.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(
            control.IsKeyboardFocusWithin,
            $"{control.GetType().Name} never took focus, so nothing was proven.");
        return window;
    }

    private static (Border Outer, Border Inner) RequireRings(Control control)
    {
        var layer = AdornerLayer.GetAdornerLayer(control);
        Assert.True(layer is not null, $"{control.GetType().Name} has no adorner layer to draw a ring in.");
        // A ToggleSwitch hangs the ring on the Grid inside its own template rather than on itself,
        // which is where the switch actually is; what matters is that the ring is drawn for this
        // control, not which element of it carries the adorner.
        var outer = layer.Children
            .OfType<Border>()
            .SingleOrDefault(border => AdornerLayer.GetAdornedElement(border) is { } adorned
                && (adorned == control || control.IsVisualAncestorOf(adorned)));
        Assert.True(
            outer is not null,
            $"{control.GetType().Name} took keyboard focus and no ring was drawn over it: the adorner "
                + $"layer holds {layer.Children.Count} child(ren) — "
                + string.Join(
                    ", ",
                    layer.Children.Select(child =>
                        $"{child.GetType().Name} over {AdornerLayer.GetAdornedElement(child)?.GetType().Name ?? "nothing"}")));
        var inner = outer.Child as Border;
        Assert.True(
            inner is not null,
            $"{control.GetType().Name} has an outer ring and no inner one, so the cue is a colour again.");
        return (outer, inner);
    }

    private static Color ColorOf(IBrush? brush) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
}
