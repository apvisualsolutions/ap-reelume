// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Presentation.Theme;

/// <summary>
/// What the system's answers <b>mean</b>, kept apart from the asking.
/// </summary>
/// <remarks>
/// The host reads two numbers out of Windows — a flags word and a window colour — and neither of them
/// decides anything on its own. Those decisions are here, where they can be measured without a
/// machine that happens to be in high contrast at the time.
/// </remarks>
public static class HighContrastPolicy
{
    /// <summary>The bit <c>SPI_GETHIGHCONTRAST</c> raises when high contrast is on.</summary>
    private const uint HighContrastOn = 0x00000001;

    /// <summary>Whether a <c>HIGHCONTRAST</c> flags word says high contrast is on.</summary>
    public static bool IsOn(uint flags) => (flags & HighContrastOn) == HighContrastOn;

    /// <summary>
    /// Whether a Windows <c>COLORREF</c> is light enough to be a light theme's window colour.
    /// </summary>
    /// <remarks>
    /// Light or dark is taken from the colour and never from the theme's name: Windows ships four
    /// high contrast themes, anyone can define their own, and every name is localised — "Contraste
    /// alto negro" and "High Contrast Black" are one theme. A colour is not translated. A
    /// <c>COLORREF</c> is <c>0x00BBGGRR</c>, which is the reverse of every other colour in this
    /// codebase, so the channels are pulled out here rather than at the call site.
    /// </remarks>
    public static bool IsLight(uint windowColour) => RelativeLuminance(
        (byte)(windowColour & 0xFF),
        (byte)((windowColour >> 8) & 0xFF),
        (byte)((windowColour >> 16) & 0xFF)) > 0.5;

    /// <summary>The WCAG relative luminance of an opaque colour, in 0..1.</summary>
    public static double RelativeLuminance(byte red, byte green, byte blue) =>
        (0.2126 * Linearise(red))
        + (0.7152 * Linearise(green))
        + (0.0722 * Linearise(blue));

    private static double Linearise(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
