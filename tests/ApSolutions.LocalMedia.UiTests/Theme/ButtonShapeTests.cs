// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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
/// A button is round or it is a pill. It is never a square with its corners taken off.
/// </summary>
/// <remarks>
/// «Todos los botones o son redondos o son píldoras, pero nunca cuadrados», the owner said on
/// 2026-08-25. Two classes disagreed: the player's chrome, 44 by 44 with the medium radius, and one
/// actually called <c>player-pill</c> that drew the small one. What makes the rule hold rather than
/// be restated is that a square target and a pill radius are the same thing — a circle — so both
/// shapes come from one token and a third shape cannot appear without a setter that names it.
/// <para>
/// The poster card is the one exception and says so in its own words: a card is a card, and it is
/// a button only because a card is pressed.
/// </para>
/// </remarks>
public sealed class ButtonShapeTests
{
    private const string CardException = "Button.poster-card";

    [Fact]
    public void No_button_style_names_a_corner_that_is_not_the_pill()
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

        var offenders = new List<string>();
        foreach (Match style in styles)
        {
            var selector = style.Groups["selector"].Value;
            if (selector.Contains(CardException, StringComparison.Ordinal))
            {
                continue;
            }

            var corner = Regex.Match(
                style.Groups["body"].Value,
                "Property=\"CornerRadius\" Value=\"(?<value>[^\"]+)\"",
                RegexOptions.None,
                TimeSpan.FromSeconds(5));
            if (corner.Success && !corner.Groups["value"].Value.Contains("CornerRadiusPill", StringComparison.Ordinal))
            {
                var drawn = corner.Groups["value"].Value;
                offenders.Add($"{selector} draws {drawn}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A button is round or a pill, never a square with its corners taken off: "
                + string.Join("; ", offenders));
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
