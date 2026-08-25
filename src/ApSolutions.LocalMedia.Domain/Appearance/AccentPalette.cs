// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace ApSolutions.LocalMedia.Domain.Appearance;

/// <summary>
/// The four tones one accent colour turns into: the accent itself, its wash, its ink, and the ink
/// that goes on top of it.
/// </summary>
public readonly record struct AccentTones(string Accent, string Subtle, string Ink, string Text);

/// <summary>
/// Turns any colour a person picks into an accent family that is still readable.
/// </summary>
/// <remarks>
/// <para>
/// The prototype's Appearance page offers six presets <b>and a picker</b>, and a picker is the whole
/// difficulty: <c>ContrastTokenTests</c> holds five obligations on the accent — it must be visible
/// against the shell at 3:1, whatever sits on it must read at 4.5:1, its ink must read on its wash
/// and on the shell at 4.5:1, and it must not be the focus ring's colour. Four of those were met by
/// hand-picking two colours per theme, which is not a thing a person choosing #7A00FF can do.
/// </para>
/// <para>
/// So the family is derived rather than chosen. Hue and saturation are the person's; lightness is
/// walked — down on a light surface, up on a dark one — until the ratio is met, which always
/// terminates because black and white sit at the ends of that walk and one of them always answers.
/// Nothing here reads a theme or a resource: it takes the surface it will be drawn on and gives back
/// four strings, so the same policy answers for the light dictionary and the dark one.
/// </para>
/// <para>
/// It is in the domain because it is a decision about whether somebody can read the interface, and
/// this is where those live.
/// </para>
/// </remarks>
public static class AccentPalette
{
    /// <summary>Text needs 4.5:1 and a large shape needs 3:1; both are WCAG's AA.</summary>
    private const double TextMinimum = 4.5;

    private const double ShapeMinimum = 3.0;

    /// <summary>How far the wash is pulled towards the surface it sits on.</summary>
    /// <remarks>
    /// 0.86 rather than a half: the prototype's <c>--accent-sub</c> is a tint a word is written on,
    /// not a second accent. Pulled less, the ink cannot reach 4.5:1 against it without going nearly
    /// black; pulled more, the wash stops being distinguishable from the page.
    /// </remarks>
    private const double WashTowardsSurface = 0.86;

    /// <summary>One step of the lightness walk: fine enough that the result is not visibly off.</summary>
    private const double LightnessStep = 1.0 / 255.0;

    /// <summary>Where white and black are equally readable; above it a page is light.</summary>
    private const double EqualContrastLuminance = 0.1791;

    /// <summary>
    /// The six the prototype offers, which are its own values and not an interpretation of them.
    /// </summary>
    public static IReadOnlyList<string> Presets { get; } =
    [
        "#1769AA",
        "#2D6A4F",
        "#8E4B2E",
        "#6B4E9B",
        "#B23A48",
        "#0E7490",
    ];

    /// <summary>
    /// The grid a picker opens with: eight hues at five lightnesses, over a row of eight greys.
    /// </summary>
    /// <remarks>
    /// The prototype's picker is the browser's own <c>&lt;input type=color&gt;</c>, which opens a
    /// grid of colours and a way to reach any other. This is that grid, built rather than listed so
    /// it cannot drift: the hues are evenly spaced round the wheel and the lightnesses evenly spaced
    /// up it, which is what makes a grid readable as a grid instead of as forty-eight opinions.
    /// </remarks>
    public static IReadOnlyList<string> Grid { get; } = BuildGrid();

    private static List<string> BuildGrid()
    {
        var colours = new List<string>();
        for (var step = 0; step < 8; step++)
        {
            colours.Add(Join(0, 0, step * 100.0 / 7));
        }

        foreach (var lightness in new[] { 25.0, 40.0, 55.0, 70.0, 85.0 })
        {
            for (var step = 0; step < 8; step++)
            {
                colours.Add(Join(step * 360.0 / 8, 70, lightness));
            }
        }

        return colours;
    }

    /// <summary>Whether a string is an opaque <c>#RRGGBB</c> this can work with.</summary>
    public static bool IsAccent(string? value) =>
        value is { Length: 7 }
        && value[0] == '#'
        && int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);

    /// <summary>
    /// The family <paramref name="accent"/> becomes on <paramref name="surface"/>.
    /// </summary>
    /// <param name="accent">What the person picked, as <c>#RRGGBB</c>.</param>
    /// <param name="surface">The page the accent is drawn on, as <c>#RRGGBB</c>.</param>
    /// <param name="focus">
    /// The focus ring's colour. The accent is nudged off it when the two land together, because a
    /// mark and the keyboard's position in one colour are one signal doing two jobs.
    /// </param>
    public static AccentTones Derive(string accent, string surface, string focus)
    {
        var picked = Parse(accent);
        var ground = Parse(surface);
        var ring = Parse(focus);
        // Which way the walk goes is a fact about the page, not about the pick: on a light page
        // every readable accent is darker than it, and on a dark page every one is lighter. Deciding
        // it by comparing the two was measured wrong — #000033 is darker than the dark theme's own
        // page, and walking it further down reaches black, which is the one direction that cannot
        // help. 0.1791 is where black and white contrast equally, so it is the line between the two.
        var darker = Luminance(ground) > EqualContrastLuminance;

        var (hue, saturation, lightness) = ToHsl(picked);
        var body = Walk(hue, saturation, lightness, darker, candidate =>
            Contrast(candidate, ground) >= ShapeMinimum);

        // The ring and the accent apart, by one step of the same walk: they are compared as colours
        // rather than as strings, so a ring written #005A9C and an accent that landed on the same
        // three bytes is caught however either was spelled.
        if (body == ring)
        {
            body = Walk(hue, saturation, ToHsl(body).L, darker, candidate =>
                Contrast(candidate, ground) >= ShapeMinimum && candidate != ring);
        }

        var wash = Blend(body, ground, WashTowardsSurface);
        var ink = Walk(hue, saturation, ToHsl(body).L, darker, candidate =>
            Contrast(candidate, ground) >= TextMinimum && Contrast(candidate, wash) >= TextMinimum);

        // White or black, whichever reads better on the accent — and one of them always does. A
        // colour light enough that white fails is dark enough for black to pass, and the two
        // thresholds overlap rather than leaving a gap.
        var white = ((byte)255, (byte)255, (byte)255);
        var black = ((byte)0, (byte)0, (byte)0);
        var text = Contrast(white, body) >= Contrast(black, body) ? white : black;

        return new AccentTones(Format(body), Format(wash), Format(ink), Format(text));
    }

    /// <summary>The contrast ratio between two opaque colours, as WCAG defines it.</summary>
    public static double Contrast(string first, string second) => Contrast(Parse(first), Parse(second));

    /// <summary>
    /// A colour as hue, saturation and lightness, in the units a person moves a slider in.
    /// </summary>
    /// <remarks>
    /// Public because the picker is three sliders and a swatch: it has to take the colour apart to
    /// show where the handles are and put it back together as they move. The conversion is the same
    /// one the derivation walks lightness along — one implementation, so the picker and the walk can
    /// never disagree about what a colour is.
    /// </remarks>
    public static (double Hue, double Saturation, double Lightness) Split(string colour)
    {
        var (hue, saturation, lightness) = ToHsl(Parse(colour));
        return (hue * 360, saturation * 100, lightness * 100);
    }

    /// <summary>The colour those three make, as <c>#RRGGBB</c>.</summary>
    public static string Join(double hue, double saturation, double lightness) =>
        Format(FromHsl(
            Math.Clamp(hue, 0, 360) / 360,
            Math.Clamp(saturation, 0, 100) / 100,
            Math.Clamp(lightness, 0, 100) / 100));

    private static (byte R, byte G, byte B) Walk(
        double hue,
        double saturation,
        double lightness,
        bool darker,
        Func<(byte R, byte G, byte B), bool> accepts)
    {
        var current = FromHsl(hue, saturation, lightness);
        if (accepts(current))
        {
            return current;
        }

        // Bounded by the ends of the scale rather than by a count: the walk stops at black going
        // down and at white going up, and both of those satisfy every ratio this asks for against
        // the surface they are walking away from.
        for (var step = lightness; step >= 0 && step <= 1; step += darker ? -LightnessStep : LightnessStep)
        {
            var candidate = FromHsl(hue, saturation, step);
            if (accepts(candidate))
            {
                return candidate;
            }
        }

        return darker ? ((byte)0, (byte)0, (byte)0) : ((byte)255, (byte)255, (byte)255);
    }

    private static (byte R, byte G, byte B) Blend((byte R, byte G, byte B) colour, (byte R, byte G, byte B) towards, double amount) =>
        (
            (byte)Math.Round((colour.R * (1 - amount)) + (towards.R * amount)),
            (byte)Math.Round((colour.G * (1 - amount)) + (towards.G * amount)),
            (byte)Math.Round((colour.B * (1 - amount)) + (towards.B * amount)));

    private static (byte R, byte G, byte B) Parse(string value)
    {
        if (!IsAccent(value))
        {
            throw new ArgumentException($"'{value}' is not an opaque #RRGGBB colour.", nameof(value));
        }

        return (
            byte.Parse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static string Format((byte R, byte G, byte B) colour) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");

    private static double Contrast((byte R, byte G, byte B) first, (byte R, byte G, byte B) second)
    {
        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance((byte R, byte G, byte B) colour)
    {
        static double Linearize(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linearize(colour.R))
            + (0.7152 * Linearize(colour.G))
            + (0.0722 * Linearize(colour.B));
    }

    private static (double H, double S, double L) ToHsl((byte R, byte G, byte B) colour)
    {
        var red = colour.R / 255.0;
        var green = colour.G / 255.0;
        var blue = colour.B / 255.0;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var lightness = (max + min) / 2;
        if (max == min)
        {
            return (0, 0, lightness);
        }

        var delta = max - min;
        var saturation = lightness > 0.5 ? delta / (2 - max - min) : delta / (max + min);
        double hue;
        if (max == red)
        {
            hue = ((green - blue) / delta) + (green < blue ? 6 : 0);
        }
        else if (max == green)
        {
            hue = ((blue - red) / delta) + 2;
        }
        else
        {
            hue = ((red - green) / delta) + 4;
        }

        return (hue / 6, saturation, lightness);
    }

    private static (byte R, byte G, byte B) FromHsl(double hue, double saturation, double lightness)
    {
        lightness = Math.Clamp(lightness, 0, 1);
        if (saturation == 0)
        {
            var grey = (byte)Math.Round(lightness * 255);
            return (grey, grey, grey);
        }

        var second = lightness < 0.5
            ? lightness * (1 + saturation)
            : lightness + saturation - (lightness * saturation);
        var first = (2 * lightness) - second;

        return (
            Channel(first, second, hue + (1.0 / 3)),
            Channel(first, second, hue),
            Channel(first, second, hue - (1.0 / 3)));

        static byte Channel(double first, double second, double hue)
        {
            if (hue < 0)
            {
                hue += 1;
            }

            if (hue > 1)
            {
                hue -= 1;
            }

            var value = hue switch
            {
                < 1.0 / 6 => first + ((second - first) * 6 * hue),
                < 1.0 / 2 => second,
                < 2.0 / 3 => first + ((second - first) * ((2.0 / 3) - hue) * 6),
                _ => first,
            };

            return (byte)Math.Round(value * 255);
        }
    }
}
