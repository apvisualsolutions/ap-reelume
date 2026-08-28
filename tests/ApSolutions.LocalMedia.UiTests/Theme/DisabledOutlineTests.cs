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

        // The corners are the control's own. They were a fixed 4 for every type, so a pill got a
        // nearly square dotted rectangle whose corners sat outside its own edge — which reads as an
        // outline that is bigger than the thing it outlines, and was the one complaint about it.
        Assert.Equal(button.CornerRadius.TopLeft, outline.RadiusX);
        Assert.Equal(button.CornerRadius.TopLeft, outline.RadiusY);

        var pill = new Button { Content = "Ok", Width = 120, Height = 36, IsEnabled = false, CornerRadius = new CornerRadius(18) };
        var pillWindow = Show(pill);
        Assert.Equal(18, RequireOutline(pill).RadiusX);
        pillWindow.Close();

        // What it measures next to what it is drawn over.
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(button.Bounds.Width, outline.Bounds.Width, 1);
        Assert.Equal(button.Bounds.Height, outline.Bounds.Height, 1);
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

    /// <summary>
    /// A control with no corner radius of its own is outlined with the fallback one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The corners are read off <c>TemplatedControl.CornerRadius</c>, and a plain <c>Control</c> is
    /// not one and carries no radius at all — which is what <c>FallbackCornerRadius</c> is for. That
    /// arm had never been taken: every one of the ten types the style names is templated, so the
    /// fallback was written, documented, and measured by nobody.
    /// </para>
    /// <para>
    /// Shown by writing the attached property rather than by disabling something, because the point
    /// is the shape of the control and not how it got here. It is the same door the style uses.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void A_control_with_no_radius_of_its_own_takes_the_fallback_corners()
    {
        var plain = new Canvas { Width = 120, Height = 36 };
        var window = Show(plain);

        plain.SetValue(Presentation.Theme.DisabledOutline.IsShownProperty, true);
        Dispatcher.UIThread.RunJobs();

        var outline = RequireOutline(plain);
        Assert.Equal(4, outline.RadiusX);
        Assert.Equal(4, outline.RadiusY);
        window.Close();
    }

    /// <summary>
    /// Mounts in high contrast, which is the only place this cue is spent since 2026-08-25.
    /// </summary>
    /// <remarks>
    /// It used to be drawn in all four themes, and in light and dark that put a dotted rectangle on
    /// top of a grey fill that already said the same thing — 299 of them across the tree with no
    /// data loaded, which is what the owner counted on seven screens. The reason the outline exists
    /// is that the two high contrast palettes have no grey to spend, so that is where it stays.
    /// </remarks>
    private static Window Show(Control control)
    {
        Avalonia.Application.Current!.RequestedThemeVariant =
            Presentation.Theme.AppThemeVariants.HighContrastDark;
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
