// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Appearance;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Appearance;

/// <summary>
/// The five obligations the accent carries, held for <b>every</b> colour a person can pick.
/// </summary>
/// <remarks>
/// <c>ContrastTokenTests</c> measures the accent the dictionaries are written with, which is two
/// colours per theme somebody chose by hand. A picker makes that guarantee meaningless unless the
/// derivation itself is what is measured — so this sweeps the hue and saturation wheel against the
/// two surfaces a custom accent is ever drawn on, and asserts the same five ratios on every result.
/// The two high-contrast dictionaries are not swept because their accent is not choosable: high
/// contrast is a need rather than a taste, and the values there are fixed.
/// </remarks>
public sealed class AccentPaletteTests
{
    /// <summary>The light theme's page and its focus ring, and the dark theme's.</summary>
    public static TheoryData<string, string> Surfaces =>
        new()
        {
            { "#FBFCFE", "#005A9C" },
            { "#08090C", "#7CC4FF" },
        };

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void Every_colour_on_the_wheel_derives_a_family_that_meets_all_five_obligations(
        string surface,
        string focus)
    {
        // 24 hues by 5 saturations by 5 lightnesses: 600 colours per surface, which covers the wheel
        // densely enough that a hue where the walk fails cannot hide between two samples, and is
        // still a test that answers in under a second.
        foreach (var colour in Wheel())
        {
            var tones = AccentPalette.Derive(colour, surface, focus);

            Assert.True(
                AccentPalette.Contrast(tones.Accent, surface) >= 3.0,
                $"{colour} on {surface}: the accent reads {AccentPalette.Contrast(tones.Accent, surface):F2}:1 "
                    + "against the page and needs 3:1.");
            Assert.True(
                AccentPalette.Contrast(tones.Text, tones.Accent) >= 4.5,
                $"{colour} on {surface}: what sits on the accent reads "
                    + $"{AccentPalette.Contrast(tones.Text, tones.Accent):F2}:1 and needs 4.5:1.");
            Assert.True(
                AccentPalette.Contrast(tones.Ink, tones.Subtle) >= 4.5,
                $"{colour} on {surface}: the ink on its own wash reads "
                    + $"{AccentPalette.Contrast(tones.Ink, tones.Subtle):F2}:1 and needs 4.5:1.");
            Assert.True(
                AccentPalette.Contrast(tones.Ink, surface) >= 4.5,
                $"{colour} on {surface}: the ink on the page reads "
                    + $"{AccentPalette.Contrast(tones.Ink, surface):F2}:1 and needs 4.5:1.");
            Assert.False(
                string.Equals(tones.Accent, focus, StringComparison.OrdinalIgnoreCase),
                $"{colour} on {surface}: the accent landed on the focus ring's own colour, so the "
                    + "mark and the keyboard's position became one signal.");
        }
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void The_six_the_prototype_offers_are_derived_by_the_same_policy(string surface, string focus)
    {
        Assert.Equal(6, AccentPalette.Presets.Count);
        foreach (var preset in AccentPalette.Presets)
        {
            Assert.True(AccentPalette.IsAccent(preset));
            var tones = AccentPalette.Derive(preset, surface, focus);
            Assert.True(AccentPalette.Contrast(tones.Accent, surface) >= 3.0);
            Assert.True(AccentPalette.Contrast(tones.Ink, surface) >= 4.5);
        }
    }

    [Fact]
    public void An_accent_that_already_reads_is_left_where_the_person_put_it()
    {
        // The default is the prototype's own #1769AA on the light page, and it meets 3:1 as it
        // stands: the walk has to return the colour untouched rather than move it for tidiness.
        var tones = AccentPalette.Derive("#1769AA", "#FBFCFE", "#005A9C");
        Assert.Equal("#1769AA", tones.Accent);

        // The same colour reads on the dark page too — 3.44:1 — so it is left alone there as well.
        // What does not survive the move is a darker pick: #0B4A78 is the light theme's own ink, and
        // on the dark page it has to be lightened, which is the point of deriving rather than
        // storing four hand-picked values.
        Assert.Equal("#1769AA", AccentPalette.Derive("#1769AA", "#08090C", "#7CC4FF").Accent);

        var dark = AccentPalette.Derive("#0B4A78", "#08090C", "#7CC4FF");
        Assert.NotEqual("#0B4A78", dark.Accent);
        Assert.True(AccentPalette.Contrast(dark.Accent, "#08090C") >= 3.0);
        Assert.True(AccentPalette.Contrast("#0B4A78", "#08090C") < 3.0);
    }

    [Fact]
    public void Grey_and_the_two_ends_of_the_scale_are_colours_too()
    {
        // Saturation zero has no hue at all, and black and white are where the walk would run out
        // of room. All three are things a picker hands over.
        foreach (var colour in new[] { "#808080", "#000000", "#FFFFFF" })
        {
            foreach (var (surface, focus) in new[] { ("#FBFCFE", "#005A9C"), ("#08090C", "#7CC4FF") })
            {
                var tones = AccentPalette.Derive(colour, surface, focus);
                Assert.True(AccentPalette.Contrast(tones.Accent, surface) >= 3.0);
                Assert.True(AccentPalette.Contrast(tones.Text, tones.Accent) >= 4.5);
                Assert.True(AccentPalette.Contrast(tones.Ink, tones.Subtle) >= 4.5);
                Assert.True(AccentPalette.Contrast(tones.Ink, surface) >= 4.5);
            }
        }
    }

    [Fact]
    public void Anything_that_is_not_an_opaque_colour_is_refused_rather_than_guessed_at()
    {
        Assert.False(AccentPalette.IsAccent(null));
        Assert.False(AccentPalette.IsAccent(string.Empty));
        Assert.False(AccentPalette.IsAccent("1769AA"));
        Assert.False(AccentPalette.IsAccent("#1769A"));
        Assert.False(AccentPalette.IsAccent("#CC1769AA"));
        Assert.False(AccentPalette.IsAccent("#ZZZZZZ"));
        Assert.True(AccentPalette.IsAccent("#1769AA"));
        Assert.True(AccentPalette.IsAccent("#1769aa"));
        _ = Assert.Throws<ArgumentException>(() => AccentPalette.Derive("nope", "#FBFCFE", "#005A9C"));
        _ = Assert.Throws<ArgumentException>(() => AccentPalette.Derive("#1769AA", "nope", "#005A9C"));
        _ = Assert.Throws<ArgumentException>(() => AccentPalette.Derive("#1769AA", "#FBFCFE", "nope"));
    }

    [Fact]
    public void The_accent_is_moved_off_the_focus_ring_when_a_pick_lands_on_it()
    {
        // Picking exactly the light theme's focus ring: it reads against the page, so nothing but
        // the collision would move it.
        var tones = AccentPalette.Derive("#005A9C", "#FBFCFE", "#005A9C");
        Assert.NotEqual("#005A9C", tones.Accent);
        Assert.True(AccentPalette.Contrast(tones.Accent, "#FBFCFE") >= 3.0);
    }

    private static IEnumerable<string> Wheel()
    {
        for (var hue = 0; hue < 360; hue += 15)
        {
            for (var saturation = 0; saturation <= 100; saturation += 25)
            {
                for (var lightness = 10; lightness <= 90; lightness += 20)
                {
                    yield return FromHsl(hue / 360.0, saturation / 100.0, lightness / 100.0);
                }
            }
        }
    }

    private static string FromHsl(double hue, double saturation, double lightness)
    {
        var second = lightness < 0.5
            ? lightness * (1 + saturation)
            : lightness + saturation - (lightness * saturation);
        var first = (2 * lightness) - second;
        var red = Channel(first, second, hue + (1.0 / 3));
        var green = Channel(first, second, hue);
        var blue = Channel(first, second, hue - (1.0 / 3));
        return $"#{red:X2}{green:X2}{blue:X2}";

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
