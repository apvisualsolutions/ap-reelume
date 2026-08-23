// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation.Library;
using Avalonia.Media;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// The colour a poster is painted with, which is arithmetic on its title and nothing else.
/// </summary>
/// <remarks>
/// The prototype's covers are four CSS gradients over one hue, with no image anywhere in the file.
/// That is what these assert is reproduced: a hue that does not move, a gradient that starts light
/// and ends dark, and a glow that fades to nothing.
/// </remarks>
public sealed class PosterArtTests
{
    /// <summary>Five titles from the prototype's own demonstration data.</summary>
    private static readonly string[] Wall =
    [
        "Vidrio Templado",
        "Constelación Menor",
        "Puerto Sombra",
        "Neón Sobre el Río",
        "El Faro de Piedra",
    ];

    /// <summary>Titles picked to stretch the hash: one letter, twenty, a symbol, a digit.</summary>
    private static readonly string[] Edges = ["a", "zzzzzzzzzzzzzzzzzzzz", "Ω", "El Faro de Piedra", "9"];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_title_that_is_not_there_takes_the_first_hue(string? title)
    {
        Assert.Equal(0, PosterArt.HueOf(title));
    }

    /// <summary>
    /// The same title is the same colour on every launch.
    /// </summary>
    /// <remarks>
    /// The reason this is asserted rather than assumed: <c>string.GetHashCode</c> has been randomised
    /// per process since .NET Core, so the obvious implementation would give a library a different
    /// set of colours every time it opened — and a colour that changes is one nobody can learn.
    /// Comparing two calls in one process cannot catch that, so what is compared is the value: 175
    /// is what the rolling hash gives this title, and it is the same number in any process.
    /// </remarks>
    [Fact]
    public void A_title_keeps_its_hue_across_processes()
    {
        Assert.Equal(PosterArt.HueOf("El Faro de Piedra"), PosterArt.HueOf("El Faro de Piedra"));
        Assert.Equal(175, PosterArt.HueOf("El Faro de Piedra"));
        Assert.Equal(11, PosterArt.HueOf("Puerto Sombra"));
    }

    [Fact]
    public void Every_hue_is_one_a_colour_wheel_has()
    {
        foreach (var title in Edges)
        {
            var hue = PosterArt.HueOf(title);
            Assert.InRange(hue, 0, 359);
        }
    }

    /// <summary>
    /// Different titles are different colours, which is the whole point of the wall.
    /// </summary>
    [Fact]
    public void A_wall_of_covers_is_a_wall_of_different_colours()
    {
        var hues = Wall.Select(PosterArt.HueOf).ToArray();

        Assert.Equal(hues.Length, hues.Distinct().Count());
    }

    /// <summary>
    /// The base gradient runs from the prototype's lighter stop to its darker one.
    /// </summary>
    [Fact]
    public void The_base_gradient_starts_lighter_than_it_ends()
    {
        var brush = Assert.IsType<LinearGradientBrush>(PosterArt.BaseOf("El Faro de Piedra"));
        Assert.Equal(2, brush.GradientStops.Count);

        var first = brush.GradientStops[0].Color;
        var second = brush.GradientStops[1].Color;
        Assert.True(
            Luminance(first) > Luminance(second),
            $"the gradient goes from {first} to {second}, which is the prototype's two stops the "
                + "wrong way round.");
        Assert.Equal(byte.MaxValue, first.A);
        Assert.Equal(byte.MaxValue, second.A);
    }

    /// <summary>
    /// The glow is half-opaque where it starts and gone where it ends.
    /// </summary>
    [Fact]
    public void The_glow_fades_to_nothing()
    {
        var brush = Assert.IsType<RadialGradientBrush>(PosterArt.GlowOf("Puerto Sombra"));
        Assert.Equal(2, brush.GradientStops.Count);
        Assert.InRange(brush.GradientStops[0].Color.A, 120, 135);
        Assert.Equal(0, brush.GradientStops[1].Color.A);
    }

    /// <summary>
    /// The HSL conversion, against values a colour picker agrees with.
    /// </summary>
    /// <remarks>
    /// Six hues, because the formula's job is the six sectors a switch would have written out, and a
    /// single one would leave five of them measured by nothing.
    /// </remarks>
    [Theory]
    [InlineData(0, 1, 0.5, 255, 0, 0)]
    [InlineData(60, 1, 0.5, 255, 255, 0)]
    [InlineData(120, 1, 0.5, 0, 255, 0)]
    [InlineData(180, 1, 0.5, 0, 255, 255)]
    [InlineData(240, 1, 0.5, 0, 0, 255)]
    [InlineData(300, 1, 0.5, 255, 0, 255)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(0, 0, 1, 255, 255, 255)]
    public void Hsl_becomes_the_colour_a_picker_agrees_with(
        double hue,
        double saturation,
        double lightness,
        byte red,
        byte green,
        byte blue)
    {
        var colour = PosterArt.FromHsl(hue, saturation, lightness, 1);
        Assert.Equal(red, colour.R);
        Assert.Equal(green, colour.G);
        Assert.Equal(blue, colour.B);
        Assert.Equal(byte.MaxValue, colour.A);
    }

    [Fact]
    public void The_converter_answers_with_the_layer_it_is_asked_for()
    {
        var converter = new PosterArtConverter();
        Assert.IsType<LinearGradientBrush>(
            converter.Convert("Astillero", typeof(IBrush), null, CultureInfo.InvariantCulture));
        Assert.IsType<LinearGradientBrush>(
            converter.Convert("Astillero", typeof(IBrush), "base", CultureInfo.InvariantCulture));
        Assert.IsType<RadialGradientBrush>(
            converter.Convert("Astillero", typeof(IBrush), "glow", CultureInfo.InvariantCulture));
        Assert.IsType<LinearGradientBrush>(
            converter.Convert(42, typeof(IBrush), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void The_converter_refuses_to_run_backwards()
    {
        var converter = new PosterArtConverter();
        Assert.Throws<NotSupportedException>(
            () => converter.ConvertBack(null, typeof(string), null, CultureInfo.InvariantCulture));
    }

    /// <summary>Relative luminance, enough to tell one stop from the other.</summary>
    private static double Luminance(Color colour) =>
        (0.2126 * colour.R) + (0.7152 * colour.G) + (0.0722 * colour.B);
}
