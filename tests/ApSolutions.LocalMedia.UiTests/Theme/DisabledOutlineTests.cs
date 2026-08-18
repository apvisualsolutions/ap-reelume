// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
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
/// A disabled control is drawn with a dotted outline, because in high contrast nothing else says so.
/// </summary>
/// <remarks>
/// In the light and dark themes a disabled fill is a third grey and the state reads off the colour.
/// The two high contrast palettes have no third colour to spend: measured, <c>ControlFillBrush</c>,
/// <c>ControlFillDisabledBrush</c> and <c>ShellSurfaceBrush</c> are one and the same, the border is
/// the theme's single border in all four states, and <c>TextDisabledBrush</c> equals
/// <c>TextPrimaryBrush</c>. So a disabled control there was pixel for pixel a resting one. The cue
/// has to be geometry, which is what the focus ring already proved reaches ten control types from
/// one implementation.
/// </remarks>
// The theme variant is one setting on one application, and these classes all change it. They are
// serialised so that a class reading a theme cannot be reading one another class just replaced.
[Collection("ThemeVariant")]
public sealed class DisabledOutlineTests
{
    [AvaloniaFact]
    public void A_disabled_control_is_drawn_with_a_dotted_outline()
    {
        var button = new Button { Content = "Ok", Width = 120, Height = 36, IsEnabled = false };
        var window = Show(button);

        var outline = RequireOutline(button);

        Assert.NotEmpty(outline.StrokeDashArray!);
        // A Rectangle takes two doubles where a Border takes a CornerRadius, so the outline repeats
        // CornerRadiusSmall's value rather than reading it. Asserted here so the copy cannot drift:
        // a parallel copy of a token that nothing compared is exactly how ContrastTokenTests came to
        // measure a colour the application did not paint.
        Assert.True(
            Avalonia.Application.Current!.TryGetResource(
                "CornerRadiusSmall",
                ThemeVariant.Default,
                out var radius),
            "CornerRadiusSmall is gone, so the outline's corners match nothing.");
        Assert.Equal(Assert.IsType<CornerRadius>(radius).TopLeft, outline.RadiusX);
        window.Close();
    }

    [AvaloniaFact]
    public void Enabling_the_control_takes_the_outline_away()
    {
        var button = new Button { Content = "Ok", Width = 120, Height = 36, IsEnabled = false };
        var window = Show(button);
        RequireOutline(button);

        button.IsEnabled = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Null(FindOutline(button));
        window.Close();
    }

    [AvaloniaFact]
    public void Every_control_type_that_takes_focus_also_shows_the_outline()
    {
        // The same ten types the focus ring covers. A type left out would be indistinguishable from
        // a resting one in high contrast, which is the hole this closes.
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
            control.IsEnabled = false;
            var window = Show(control);
            RequireOutline(control);
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("ComboBox")]
    [InlineData("NumericUpDown")]
    public void A_control_built_out_of_others_is_outlined_once(string typeName)
    {
        // Measured before this was written: disabling is inherited and an application style reaches
        // template elements too, so these two drew twice — once around themselves and once around the
        // TextBox inside them, whose own IsEnabled is still true. Two dashed rectangles a few pixels
        // apart is not the cue the design asks for.
        Control control = typeName == "ComboBox" ? new ComboBox() : new NumericUpDown();
        control.IsEnabled = false;
        var window = Show(control);

        var layer = AdornerLayer.GetAdornerLayer(control)!;
        var outlines = layer.Children
            .OfType<Rectangle>()
            .Select(shape => AdornerLayer.GetAdornedElement(shape))
            .ToList();

        Assert.Equal([control], outlines);
        window.Close();
    }

    [AvaloniaFact]
    public void The_outline_takes_its_colour_from_the_theme_in_force()
    {
        Avalonia.Application.Current!.RequestedThemeVariant =
            Presentation.Theme.AppThemeVariants.HighContrastDark;
        var button = new Button { Content = "Ok", Width = 120, Height = 36, IsEnabled = false };
        var window = Show(button);

        var outline = RequireOutline(button);

        // White on black: the one theme where the outline is the entire difference between a
        // disabled control and a resting one.
        Assert.Equal(Color.Parse("#FFFFFF"), ColorOf(outline.Stroke));
        window.Close();
        Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Default;
    }

    private static Window Show(Control control)
    {
        var window = new Window { Width = 320, Height = 200, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Rectangle? FindOutline(Control control)
    {
        var layer = AdornerLayer.GetAdornerLayer(control);
        Assert.True(layer is not null, $"{control.GetType().Name} has no adorner layer to draw in.");
        return layer.Children
            .OfType<Rectangle>()
            .SingleOrDefault(shape => AdornerLayer.GetAdornedElement(shape) is { } adorned
                && (adorned == control || control.IsVisualAncestorOf(adorned)));
    }

    private static Rectangle RequireOutline(Control control)
    {
        var outline = FindOutline(control);
        Assert.True(
            outline is not null,
            $"{control.GetType().Name} is disabled and nothing was drawn over it, so in high contrast "
                + "it is pixel for pixel a resting control.");
        return outline;
    }

    private static Color ColorOf(IBrush? brush) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
}
