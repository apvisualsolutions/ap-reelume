// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace ApSolutions.LocalMedia.Presentation.Theme;

/// <summary>
/// The dotted outline that says a control is disabled, drawn over it in the adorner layer.
/// </summary>
/// <remarks>
/// In the light and dark themes a disabled control reads off its fill, which is a third grey. The two
/// high contrast palettes have no third colour to spend: measured, <c>ControlFillBrush</c>,
/// <c>ControlFillDisabledBrush</c> and <c>ShellSurfaceBrush</c> are one colour, the border is the
/// theme's single border in all four states, and <c>TextDisabledBrush</c> equals
/// <c>TextPrimaryBrush</c>. A disabled control there was pixel for pixel a resting one, so the cue has
/// to be geometry.
///
/// It is an adorner rather than a control theme of our own for the reason the focus ring already
/// measured: one implementation reaches all ten types, including the <c>ToggleSwitch</c>, which hangs
/// its adorner off the grid inside its template, and the <c>NumericUpDown</c>, which hangs it off its
/// text box. Copying nine Fluent templates for one dashed line would be nine surfaces to keep in step
/// with every Avalonia release.
///
/// <em>When</em> it is drawn is a selector's job and not this class's: the style says
/// <c>:disabled</c>, so the set of types is written where the focus ring's is, and reverting is
/// Avalonia's own — a control that becomes enabled loses the setter and this hears false.
/// </remarks>
public static class DisabledOutline
{
    /// <summary>
    /// The dash pattern, in multiples of the stroke thickness: a 3 px dash and a 2 px gap.
    /// </summary>
    /// <remarks>
    /// Long enough to survive the corner radius on a control as short as a checkbox, and short enough
    /// that the top edge of a 120 px button holds two dozen dashes rather than a handful.
    /// </remarks>
    private static readonly double[] DashPattern = [3, 2];

    /// <summary>
    /// The corner radius, which is <c>CornerRadiusSmall</c>'s. A <c>Rectangle</c> takes two doubles
    /// where a <c>Border</c> takes a <c>CornerRadius</c>, so the value is repeated here rather than
    /// read; <c>DisabledOutlineTests</c> asserts the two are the same so they cannot drift apart.
    /// </summary>
    private const double CornerRadius = 4;

    /// <summary>Whether the dotted outline is drawn over this control.</summary>
    public static readonly AttachedProperty<bool> IsShownProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsShown", typeof(DisabledOutline));

    static DisabledOutline() =>
        IsShownProperty.Changed.AddClassHandler<Control, bool>((control, change) =>
            AdornerLayer.SetAdorner(
                control,
                Wanted(control, change.NewValue.GetValueOrDefault()) ? Draw() : null));

    /// <summary>
    /// Whether this control is the one to draw around, rather than a piece of another one's template.
    /// </summary>
    /// <remarks>
    /// Disabling is inherited, and an application style reaches template elements the same way it
    /// reaches the control. Measured on the ten types: eight take one outline and two take two — a
    /// <c>ComboBox</c> and a <c>NumericUpDown</c> each hold a <c>TextBox</c> whose own
    /// <c>IsEnabled</c> is still true and whose <c>TemplatedParent</c> is the control around it. Two
    /// dashed rectangles a few pixels apart is not the cue; one is. The test is the templated parent
    /// and not the local <c>IsEnabled</c>, because those two answers differ for a control inside a
    /// panel that was disabled as a whole: the panel is not one of the ten types, so keying off the
    /// local flag would leave that case with no outline at all.
    /// </remarks>
    private static bool Wanted(Control control, bool shown) => shown && control.TemplatedParent is null;

    /// <summary>A fresh outline. Each adorner is one visual over one control, so it is never shared.</summary>
    private static Rectangle Draw()
    {
        var outline = new Rectangle
        {
            StrokeThickness = 1,
            StrokeDashArray = [.. DashPattern],
            RadiusX = CornerRadius,
            RadiusY = CornerRadius,
            // The control underneath is disabled and takes no input; the adorner must not start
            // taking any on its behalf.
            IsHitTestVisible = false,
        };

        // Dynamic, not static: the adorner outlives a theme change, and in high contrast this stroke
        // is the entire difference between disabled and resting.
        outline[!Shape.StrokeProperty] = new DynamicResourceExtension("ShellBorderBrush");
        return outline;
    }
}
