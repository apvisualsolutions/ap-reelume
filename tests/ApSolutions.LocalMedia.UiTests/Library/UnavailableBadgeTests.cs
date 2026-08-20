// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Xml.Linq;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// A medium that is not reachable right now is a warning, not an error, and it is said once.
/// </summary>
/// <remarks>
/// <para>
/// §4 asks for the badge to leave the accent and become <c>WarningSurfaceBrush</c> with a border and a
/// glyph: a USB drive that is unplugged is not something that went wrong, it is something that is not
/// here. The border and the glyph are what keep it from being colour alone.
/// </para>
/// <para>
/// The second test is the half that keeps it true. The same badge was copied by hand into five other
/// views, so changing the badge alone would have left the application saying the same thing two ways
/// — which is worse than saying it the old way in all six.
/// </para>
/// </remarks>
public sealed class UnavailableBadgeTests
{
    [AvaloniaFact]
    public void The_badge_is_a_warning_with_a_border_and_a_glyph_rather_than_the_accent()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

        var badge = new UnavailableBadge { DataContext = new UnavailableStub(false) };
        var window = new Window { Width = 400, Height = 200, Content = badge };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var border = Assert.Single(
            badge.GetVisualDescendants().OfType<Border>(),
            candidate => candidate.Background is ISolidColorBrush);
        Assert.Equal(
            ThemeColour("WarningSurfaceBrush"),
            Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color);
        Assert.NotEqual(ThemeColour("AccentSubtleBrush"), ThemeColour("WarningSurfaceBrush"));
        Assert.True(
            border.BorderThickness.Top > 0,
            "The badge has no border, so its only signal is the fill colour.");
        Assert.Contains(
            badge.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "⚠");

        window.Close();
    }

    /// <summary>
    /// The badge answers to any view model that says whether its medium is reachable.
    /// </summary>
    /// <remarks>
    /// It carries no <c>x:DataType</c>, because six view models mount it and a compiled binding is
    /// bound to one. What the compiler no longer checks is asserted here instead, and on the effect —
    /// present when the medium is gone, absent when it is not — which is stronger than the check that
    /// was given up.
    /// </remarks>
    [AvaloniaFact]
    public void The_badge_shows_itself_only_when_the_medium_is_out_of_reach()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

        foreach (var available in new[] { true, false })
        {
            var badge = new UnavailableBadge { DataContext = new UnavailableStub(available) };
            var window = new Window { Width = 400, Height = 200, Content = badge };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(!available, badge.IsVisible);
            window.Close();
        }
    }

    /// <summary>
    /// Nothing else in the tree draws its own version of the badge.
    /// </summary>
    /// <remarks>
    /// Read from the markup rather than from a mounted screen: a copy in a branch that needs data to
    /// appear would never be mounted here, and it is exactly those branches that drift.
    /// </remarks>
    [Fact]
    public void No_other_view_draws_its_own_unavailable_badge()
    {
        var presentation = Path.Combine(
            RepositoryLayout.Root,
            "src",
            "ApSolutions.LocalMedia.Presentation");
        var copies = new List<string>();

        foreach (var path in Directory.EnumerateFiles(presentation, "*.axaml", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(path) == "UnavailableBadge.axaml")
            {
                continue;
            }

            var homemade = XDocument.Load(path)
                .Descendants()
                .Where(element => element.Name.LocalName == "Border")
                .Where(element => element.Attribute("IsVisible")?.Value.Contains("!IsAvailable", StringComparison.Ordinal) == true)
                .Select(_ => Path.GetFileName(path));
            copies.AddRange(homemade);
        }

        Assert.Empty(copies);
    }

    private static Color ThemeColour(string key)
    {
        var application = Avalonia.Application.Current;
        Assert.NotNull(application);
        Assert.True(
            application.TryGetResource(key, application.ActualThemeVariant, out var value),
            $"{key} is not declared in this theme variant.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    private sealed record UnavailableStub(bool IsAvailable);
}
