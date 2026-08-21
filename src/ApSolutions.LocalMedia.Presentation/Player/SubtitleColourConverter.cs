// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Turns a stored subtitle colour into something the preview can paint with.
/// </summary>
/// <remarks>
/// <para>
/// The two colours are edited as text, so what arrives here is whatever somebody has typed so far —
/// half a hex value, a stray character, an empty box between two keystrokes. A converter that threw on
/// any of that would take the preview down while a person was still typing, so an unreadable value
/// falls back to the colour the domain would use rather than to nothing.
/// </para>
/// <para>
/// The parameter carries the opacity for the background, because a subtitle box is normally partly
/// transparent and the person choosing it has a slider for exactly that. Alpha is applied to the
/// colour rather than to the control: a control's <c>Opacity</c> would fade the text sitting on it too.
/// </para>
/// </remarks>
public sealed class SubtitleColourConverter : IValueConverter
{
    /// <summary>What an unreadable value paints as: opaque white, which is what a subtitle is.</summary>
    private static readonly Color Fallback = Colors.White;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var colour = Color.TryParse(value as string, out var parsed) ? parsed : Fallback;
        if (parameter is not null && double.TryParse(
                parameter as string,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var opacity))
        {
            colour = new Color((byte)Math.Clamp(opacity * 255, 0, 255), colour.R, colour.G, colour.B);
        }

        return new SolidColorBrush(colour);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A subtitle colour is edited as text, not painted back.");
}
