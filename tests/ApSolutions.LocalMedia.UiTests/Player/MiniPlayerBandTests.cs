// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.UiTests.Theme;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The mini player's chrome is a band, and what is drawn on it can be seen in all four themes.
/// </summary>
/// <remarks>
/// <para>
/// The band painted <c>ShellSurfaceBrush</c> from the day it was written while its five buttons take
/// <c>PlayerTextBrush</c> from the <c>player-chrome</c> class — and those two are the same colour in
/// the light theme, <c>#FBFCFE</c> under <c>#F8FAFC</c>. Nothing said so: every gate this repository
/// has about contrast reads the four dictionaries, and a dictionary is consistent with itself. What
/// was wrong was the <b>pairing</b>, which only exists in one view, and a view is where it has to be
/// measured.
/// </para>
/// <para>
/// It is asserted at 3:1 rather than 4.5:1 because what sits on this band is a glyph and not a
/// sentence: that is the ratio WCAG asks of a graphical object, and it is the same one the accent
/// cession in <c>docs/design/ELEMENTS.es.md</c> already argues from. The readout beside them carries
/// words and is held to 4.5:1 separately below.
/// </para>
/// </remarks>
[Collection("ThemeVariant")]
public sealed class MiniPlayerBandTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("HighContrastLight")]
    [InlineData("HighContrastDark")]
    public void The_glyphs_of_the_chrome_are_visible_against_the_band_they_sit_on(string theme)
    {
        using var scene = new Scene(Resolve(theme));

        var band = ThemeContrast.Painted(scene.Band.Background, Colors.Black);
        var ink = ThemeContrast.Painted(scene.Ink, band);
        var ratio = ThemeContrast.Ratio(ink, band);

        Assert.True(
            ratio >= 3.0,
            $"{theme} draws the mini player's glyphs at {ratio:0.00}:1 against their own band "
                + $"(ink {ink}, band {band}), so a person looking at the window sees five buttons "
                + "with nothing on them.");
    }

    /// <summary>
    /// And the words, which are words and answer to the stricter number.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("HighContrastLight")]
    [InlineData("HighContrastDark")]
    public void The_title_and_the_clock_are_readable_against_the_band(string theme)
    {
        using var scene = new Scene(Resolve(theme));

        var band = ThemeContrast.Painted(scene.Band.Background, Colors.Black);

        foreach (var (name, text) in new[]
        {
            ("MiniPlayerTitleText", scene.Title),
            ("MiniPlayerClockText", scene.Clock),
        })
        {
            var ink = ThemeContrast.Painted(text.Foreground, band);
            var ratio = ThemeContrast.Ratio(ink, band);
            Assert.True(
                ratio >= 4.5,
                $"{theme} draws {name} at {ratio:0.00}:1 against the band (ink {ink}, band {band}).");
        }
    }

    private static ThemeVariant Resolve(string name) => name switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        "HighContrastLight" => Presentation.Theme.AppThemeVariants.HighContrastLight,
        "HighContrastDark" => Presentation.Theme.AppThemeVariants.HighContrastDark,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    /// <summary>One mounted chrome in one theme, with the reset it owes.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly Window _window;

        public Scene(ThemeVariant theme)
        {
            Assert.NotNull(Avalonia.Application.Current);
            Avalonia.Application.Current!.RequestedThemeVariant = theme;
            App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

            var chrome = new MiniPlayerChromeView();
            _window = new Window { Width = 480, Height = 270, Content = chrome };
            _window.Show();
            Dispatcher.UIThread.RunJobs();

            Band = chrome.GetVisualDescendants()
                .OfType<Panel>()
                .Single(panel => panel.Name == "MiniPlayerChromeBand");
            Ink = chrome.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.Name == "MiniPlayerPlayPause")
                .Foreground;
            Title = Named(chrome, "MiniPlayerTitleText");
            Clock = Named(chrome, "MiniPlayerClockText");
        }

        public Panel Band { get; }

        public IBrush? Ink { get; }

        public TextBlock Title { get; }

        public TextBlock Clock { get; }

        public void Dispose()
        {
            _window.Close();
            Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }

        private static TextBlock Named(Control root, string name) =>
            root.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == name);
    }
}
