// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ApSolutions.LocalMedia.Presentation.Library;

/// <summary>
/// The colour a poster is painted with, computed from its title.
/// </summary>
/// <remarks>
/// <para>
/// <b>The prototype's artwork is not artwork.</b> Measured on 2026-08-22 by reading its own source:
/// every cover in <c>design/AP Reelume.dc.html</c> is four CSS gradients built from a single hue —
/// <c>linear-gradient(200deg, hsl(H 38% 30%), hsl(H+34 46% 12%))</c> under a radial glow, a diagonal
/// hatch and a ring. There is not one image in it. So the wall of colour that makes the prototype
/// look like what it looks like costs no network, no TMDB token and no file on disk, and none of the
/// reasons artwork was ruled out of 0.2.0 apply to it.
/// </para>
/// <para>
/// The hue comes from the title because the title is what every one of the four view models behind
/// <see cref="IPosterCard"/> already has. It is deterministic and culture-free: the same title is the
/// same colour on every machine and in both languages, which is what makes a grid of them learnable
/// rather than decorative.
/// </para>
/// <para>
/// The initials stay on top of it. The prototype has none — it puts a ring there — but two letters
/// say which title this is before the colour has taught anybody anything, and a colour alone is a
/// distinction somebody who cannot see colour does not get.
/// </para>
/// </remarks>
public static class PosterArt
{
    /// <summary>The prototype's own numbers, kept as its numbers.</summary>
    private const double BaseSaturation = 0.38;

    private const double BaseLightness = 0.30;

    private const int SecondStopShift = 34;

    private const double SecondSaturation = 0.46;

    private const double SecondLightness = 0.12;

    private const double GlowSaturation = 0.62;

    private const double GlowLightness = 0.58;

    private const double GlowAlpha = 0.5;

    /// <summary>
    /// The hue a title is drawn in, from 0 to 359.
    /// </summary>
    /// <remarks>
    /// A rolling hash rather than <c>string.GetHashCode</c>, which is randomised per process since
    /// .NET Core: the same library would be a different set of colours on every launch, and a colour
    /// that changes is a colour nobody can learn.
    /// </remarks>
    public static int HueOf(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return 0;
        }

        var hash = 0;
        foreach (var character in title)
        {
            hash = unchecked((hash * 31) + character);
        }

        return (((hash % 360) + 360) % 360);
    }

    /// <summary>The two-stop gradient the whole card sits on.</summary>
    public static IBrush BaseOf(string? title)
    {
        var hue = HueOf(title);
        return new LinearGradientBrush
        {
            // The prototype's 200deg, which in CSS points down and slightly left: from the top right
            // corner to the bottom left one.
            StartPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(FromHsl(hue, BaseSaturation, BaseLightness, 1), 0),
                new GradientStop(
                    FromHsl((hue + SecondStopShift) % 360, SecondSaturation, SecondLightness, 1),
                    1),
            ],
        };
    }

    /// <summary>The light the prototype puts in the card's top left corner.</summary>
    public static IBrush GlowOf(string? title)
    {
        var hue = HueOf(title);
        return new RadialGradientBrush
        {
            Center = new RelativePoint(0.18, 0.08, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.18, 0.08, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(1.2, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.85, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(FromHsl(hue, GlowSaturation, GlowLightness, GlowAlpha), 0),
                new GradientStop(FromHsl(hue, GlowSaturation, GlowLightness, 0), 0.62),
            ],
        };
    }

    /// <summary>
    /// HSL to a colour, by the formula rather than by the six-sector switch.
    /// </summary>
    /// <remarks>
    /// The <c>f(n)</c> form has no branches at all, which is why it is the one written here: a
    /// six-way switch would be six branches to cover for a function whose only job is arithmetic.
    /// </remarks>
    public static Color FromHsl(double hue, double saturation, double lightness, double alpha)
    {
        var amplitude = saturation * Math.Min(lightness, 1d - lightness);
        return Color.FromArgb(
            Channel(alpha),
            Channel(Component(0, hue, amplitude, lightness)),
            Channel(Component(8, hue, amplitude, lightness)),
            Channel(Component(4, hue, amplitude, lightness)));
    }

    private static double Component(double n, double hue, double amplitude, double lightness)
    {
        var k = (n + (hue / 30d)) % 12d;
        return lightness - (amplitude * Math.Max(-1d, Math.Min(Math.Min(k - 3d, 9d - k), 1d)));
    }

    private static byte Channel(double value) => (byte)Math.Round(Math.Clamp(value, 0d, 1d) * 255d);
}

/// <summary>
/// Turns a title into the brush its poster is painted with; <c>glow</c> asks for the second layer.
/// </summary>
/// <remarks>
/// A converter and not a property on the view model, because the colour is a fact about the title
/// and not a decision any of the four models makes. Adding it to <see cref="IPosterCard"/> would be
/// four implementations of one arithmetic, which is the shape <see cref="PosterInitials"/> exists to
/// avoid.
/// </remarks>
public sealed class PosterArtConverter : IValueConverter
{
    /// <summary>What the second layer is asked for by.</summary>
    private const string Glow = "glow";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = culture;
        var title = value as string;
        return string.Equals(parameter as string, Glow, StringComparison.Ordinal)
            ? PosterArt.GlowOf(title)
            : PosterArt.BaseOf(title);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A poster's colour is computed from its title in one direction only.");
}
