// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Every button draws the corner the prototype draws.
/// </summary>
/// <remarks>
/// <b>This file asserted the opposite until 2026-09-01</b>, and the story is kept because it is what
/// the rule now guards against. «Todos los botones o son redondos o son píldoras, pero nunca
/// cuadrados», the owner said on 2026-08-25, and two classes were changed to obey it: the player's
/// chrome and one actually called <c>player-pill</c>. Asked about a third — the lesson row, which
/// the design draws at 7 — the owner withdrew the rule outright: <i>«esa afirmación mía era
/// equivocada, los botones deben ser al igual que todos los elementos de la app, idénticos al 100 %
/// al prototipo»</i>.
/// <para>
/// Measured against the design the same day, the withdrawn rule had moved both classes <b>away</b>
/// from it: <c>pbtn</c>, the player's icon buttons, is <c>borderRadius: 8</c> and had been made a
/// circle; <c>pbtnAudio</c> and its four siblings are <c>borderRadius: 4</c> and had been made
/// pills. A rule stated from memory beat a design nobody re-read, which is this repository's own
/// characteristic defect wearing a different hat.
/// </para>
/// <para>
/// So what is asserted now is the correspondence itself, and in two halves that cannot both go stale
/// the same way: the tree draws what the table says, and the table says what the design draws.
/// </para>
/// </remarks>
public sealed class ButtonShapeTests
{
    /// <summary>
    /// Each button class that names a corner, the prototype control it draws, and how that control
    /// is found in the design.
    /// </summary>
    /// <remarks>
    /// Paired by the number the design writes rather than by the token, because the scale is 4, 8
    /// and the pill while the design also draws 7 — a radius no token carries, and one that rounding
    /// to 4 or 8 would quietly turn into a shape the design does not draw.
    /// <para>
    /// The pattern travels with the pairing instead of being derived from the control's name,
    /// because the three are written three different ways: <c>pbtn</c> is an object literal,
    /// <c>btnPri</c> is an <c>Object.assign</c> over a base, and the lesson row has no name at all —
    /// the panel builds its rows inline, so it is found by its neighbourhood. A single clever
    /// pattern over all three is a pattern that matches the wrong thing the day one of them moves.
    /// </para>
    /// </remarks>
    private static readonly (string Selector, string Control, int Radius, string Pattern)[] Pairings =
    [
        ("Button, ToggleButton", "btnPri", 999,
            @"const btnPri = Object\.assign\([^;]*?borderRadius: (?<radius>[0-9]+)"),
        ("Button.player-chrome", "pbtn", 8,
            @"\bpbtn: \{[^}]*?borderRadius: (?<radius>[0-9]+)"),
        ("Button.player-pill", "pbtnLessons", 4,
            @"\bpbtnLessons: \{[^}]*?borderRadius: (?<radius>[0-9]+)"),
        ("Button.lesson-row", "the lesson row", 7,
            @"minHeight: 34, padding: '6px 10px', borderRadius: (?<radius>[0-9]+)"),
    ];

    [Fact]
    public void Every_paired_button_draws_the_corner_the_prototype_draws()
    {
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Theme/DesignTokens.axaml"));
        var styles = Regex.Matches(
            markup,
            "<Style Selector=\"(?<selector>[^\"]*Button[^\"]*)\">(?<body>.*?)</Style>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        // Anti-blindness floor: a pattern that matched nothing would pass by measuring nothing.
        Assert.True(styles.Count >= 8, $"only {styles.Count} button styles were read; this reads the wrong file.");

        var radii = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["{DynamicResource CornerRadiusSmall}"] = 4,
            ["{DynamicResource CornerRadiusMedium}"] = 8,
            ["{DynamicResource CornerRadiusPill}"] = 999,
        };

        var offenders = new List<string>();
        foreach (var (selector, control, expected, _) in Pairings)
        {
            // A selector can be declared more than once — player-chrome is written twice, once for
            // its padding beside the swatch and once for its shape — so the corner is looked for
            // across every block that names it rather than in whichever one comes first. Taking the
            // first was this test's own first red, and it accused the tree of drawing nothing.
            var blocks = styles
                .Where(candidate => candidate.Groups["selector"].Value == selector)
                .ToArray();
            if (blocks.Length == 0)
            {
                offenders.Add($"{selector} is paired with {control} and no longer exists");
                continue;
            }

            var corners = blocks
                .Select(block => Regex.Match(
                    block.Groups["body"].Value,
                    "Property=\"CornerRadius\" Value=\"(?<value>[^\"]+)\"",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(5)))
                .Where(match => match.Success)
                .ToArray();
            if (corners.Length == 0)
            {
                offenders.Add($"{selector} names no corner, and {control} draws {expected}");
                continue;
            }

            // Two blocks naming two different corners is a class arguing with itself, and whichever
            // one the renderer picks is not something a reader of either block can predict.
            if (corners.Select(match => match.Groups["value"].Value).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                offenders.Add($"{selector} names more than one corner");
                continue;
            }

            var written = corners[0].Groups["value"].Value;
            var drawn = radii.TryGetValue(written, out var token)
                ? token
                : int.TryParse(written, NumberStyles.Integer, CultureInfo.InvariantCulture, out var literal)
                    ? literal
                    : -1;
            if (drawn != expected)
            {
                offenders.Add($"{selector} draws {written}, and {control} draws {expected}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A button draws the corner its prototype control draws: " + string.Join("; ", offenders));
    }

    /// <summary>
    /// The radii the table above claims are the ones the design actually writes.
    /// </summary>
    /// <remarks>
    /// Without this half the table would be a second set of numbers copied by hand, which is exactly
    /// how the withdrawn rule survived a week: it read like a decision and nobody re-read the design
    /// behind it. Here the design is the source, so a pairing that drifts from it fails on the number
    /// rather than certifying itself.
    /// </remarks>
    [Fact]
    public void The_pairings_name_the_radius_the_design_writes()
    {
        var design = File.ReadAllText(RepositoryLayout.PathFromRoot("design/AP Reelume.dc.html"));

        foreach (var (selector, control, expected, pattern) in Pairings)
        {
            var match = Regex.Match(design, pattern, RegexOptions.None, TimeSpan.FromSeconds(5));

            Assert.True(
                match.Success,
                $"the design no longer draws {control}, so {selector} is paired with nothing.");
            Assert.Equal(
                expected,
                int.Parse(match.Groups["radius"].Value, CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// The corner is measured on the screen and not read off the property.
    /// </summary>
    /// <remarks>
    /// A radius larger than half the side is clamped when it is drawn, so the number alone says
    /// nothing about the shape: 999 would satisfy any comparison while the renderer decided what it
    /// actually painted. What is asserted is that the button's own fill is absent from its corner
    /// and present at its centre, which is what «round» means to somebody looking at it.
    /// </remarks>
    [AvaloniaTheory]
    // A target as wide as it is tall: the pill radius makes it a circle.
    [InlineData(44d, 44d)]
    [InlineData(28d, 28d)]
    // And one carrying a word: the same token makes it a pill.
    [InlineData(160d, 36d)]
    public void The_same_token_draws_a_circle_and_a_pill_from_the_shape_of_the_target(
        double width,
        double height)
    {
        var button = new Button
        {
            Content = string.Empty,
            Width = width,
            Height = height,
            Background = Avalonia.Media.Brushes.Red,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        var window = new Window
        {
            Width = width + 40,
            Height = height + 40,
            Background = Avalonia.Media.Brushes.White,
            Padding = new Thickness(0),
            Content = button,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The corner against the centre rather than against a colour written down here: what the
        // channels are called in the captured buffer is the renderer's business, and this is about
        // whether the button's own fill reaches its corner.
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var centre = Pixel(frame!, (int)(width / 2), (int)(height / 2));
        var corner = Pixel(frame!, 1, 1);
        Assert.NotEqual(centre, corner);
        window.Close();
    }

    private static (byte First, byte Second, byte Third) Pixel(
        Avalonia.Media.Imaging.WriteableBitmap frame,
        int x,
        int y)
    {
        using var buffer = frame.Lock();
        var column = Math.Clamp(x, 0, buffer.Size.Width - 1);
        var row = Math.Clamp(y, 0, buffer.Size.Height - 1);
        var pixel = new byte[4];
        System.Runtime.InteropServices.Marshal.Copy(
            buffer.Address + (row * buffer.RowBytes) + (column * 4),
            pixel,
            0,
            4);
        return (pixel[0], pixel[1], pixel[2]);
    }
}
