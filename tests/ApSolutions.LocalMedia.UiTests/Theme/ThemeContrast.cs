// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Media;
using Avalonia.Styling;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Reading a painted colour as it is seen, and the contrast between two of them.
/// </summary>
/// <remarks>
/// One arithmetic for every theme test, because two that measure the same thing differently always
/// drift apart. What it exists to get right is transparency, which this repository has now read
/// wrong twice: <c>ButtonBackgroundPointerOver</c> says <c>Black</c> and carries
/// <c>Opacity 0.1</c>, and the base theme's checkbox and list brushes carry alpha in the colour
/// itself (<c>#99000000</c>, <c>#33000000</c>) — and a selected row carries it in <em>both</em>,
/// a fully opaque <c>#FF0078D7</c> at <c>Opacity 0.4</c>. A luminance taken off the raw colour
/// measures something nobody sees, and its dangerous form is not a loud failure but a quiet pass: a
/// border seen at 2.83:1 reads as 21:1 if the alpha is dropped.
/// </remarks>
internal static class ThemeContrast
{
    /// <summary>The colour a brush actually puts on screen over <paramref name="background"/>.</summary>
    /// <remarks>
    /// A null brush paints nothing, so what is on screen is the background — and that is a number
    /// (1:1 against it) rather than an exception, because "the control has no border at all" is an
    /// answer a contrast assertion should be allowed to state instead of crashing on.
    /// </remarks>
    /// <param name="elementOpacity">
    /// The opacity of the element carrying the brush, for the case where transparency is applied
    /// twice over: a text box's placeholder is <c>#99000000</c> at <c>Opacity 0.5</c> on top.
    /// </param>
    public static Color Painted(IBrush? brush, Color background, double elementOpacity = 1.0)
    {
        if (brush is null)
        {
            return background;
        }

        var solid = Assert.IsAssignableFrom<ISolidColorBrush>(brush);
        var alpha = solid.Color.A / 255.0 * solid.Opacity * elementOpacity;
        return Color.FromRgb(
            (byte)Math.Round((solid.Color.R * alpha) + (background.R * (1 - alpha))),
            (byte)Math.Round((solid.Color.G * alpha) + (background.G * (1 - alpha))),
            (byte)Math.Round((solid.Color.B * alpha) + (background.B * (1 - alpha))));
    }

    /// <summary>The WCAG contrast ratio between two opaque colours.</summary>
    public static double Ratio(Color first, Color second)
    {
        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>A theme's brush, by key, or a failure naming the key that is missing.</summary>
    public static Color Token(ThemeVariant theme, string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryGetResource(key, theme, out var value),
            $"{key} is missing from {theme}, so whatever reads it falls back to the base theme.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    private static double Luminance(Color color) =>
        Presentation.Theme.HighContrastPolicy.RelativeLuminance(color.R, color.G, color.B);
}
