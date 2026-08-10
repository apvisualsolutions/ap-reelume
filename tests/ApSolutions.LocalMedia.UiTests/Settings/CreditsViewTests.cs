// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.About;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

// Avalonia's shape and the file-system helper share a name, and this file needs both.
using VectorShape = Avalonia.Controls.Shapes.Path;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The credits carry TMDB's logo, and it has to reach the screen rather than the file.
/// </summary>
/// <remarks>
/// <c>TmdbLogoTests</c> proves the geometry in the view is the one TMDB publishes; that is a text
/// comparison and it would pass just as happily on a path Avalonia cannot parse. This renders the
/// surface and measures what came out, because a shape that fails to parse draws at zero width and
/// looks, in every other check, exactly like a shape that works.
/// </remarks>
public sealed class CreditsViewTests
{
    /// <summary>What the view declares, and therefore what the drawn mark has to come back as.</summary>
    private const double DeclaredHeight = 16;

    /// <summary>What the navigation rail draws the product's own name at.</summary>
    private const double ProductNameFontSize = 24;

    [AvaloniaFact]
    public void The_tmdb_mark_reaches_the_screen_with_the_size_it_declares()
    {
        var view = Build();
        var logo = Assert.Single(view.GetVisualDescendants().OfType<VectorShape>());

        Assert.Equal(DeclaredHeight, logo.Bounds.Height, precision: 1);
        Assert.True(
            logo.Bounds.Width > logo.Bounds.Height,
            $"The mark drew {logo.Bounds.Width} by {logo.Bounds.Height}; a geometry Avalonia could "
                + "not parse comes back empty and this is what tells the two apart.");
    }

    /// <summary>
    /// The mark identifies where the metadata comes from. Assistive technology has to be able to say
    /// so, and nothing in this surface is clickable — it is an attribution, not an invitation.
    /// </summary>
    [AvaloniaFact]
    public void The_mark_is_announced_and_nothing_here_is_clickable()
    {
        var view = Build();
        var logo = Assert.Single(view.GetVisualDescendants().OfType<VectorShape>());

        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(logo)));
        Assert.Empty(view.GetVisualDescendants().OfType<Button>());
    }

    /// <summary>
    /// TMDB asks for their mark to be less prominent than the product's own. The rail draws the
    /// product name; this compares what the two actually rendered rather than the numbers declared.
    /// </summary>
    [AvaloniaFact]
    public void The_mark_is_drawn_smaller_than_the_name_of_the_product()
    {
        var credits = Build();
        var logo = Assert.Single(credits.GetVisualDescendants().OfType<VectorShape>());
        var productName = new TextBlock
        {
            FontSize = ProductNameFontSize,
            FontWeight = FontWeight.SemiBold,
            Text = "AP Reelume",
        };
        var probe = new Window { Width = 400, Height = 200, Content = productName };
        probe.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(
            logo.Bounds.Height < productName.Bounds.Height,
            $"The TMDB mark drew {logo.Bounds.Height} high against a product name of "
                + $"{productName.Bounds.Height}, which is not less prominent.");
        probe.Close();
    }

    [AvaloniaFact]
    public void The_credits_are_captured_in_both_languages()
    {
        Assert.NotNull(Avalonia.Application.Current);
        var captures = Path.Combine(GetRepositoryRoot(), "artifacts", "ui-captures", "TMDB-logo");
        _ = Directory.CreateDirectory(captures);

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(cultureName));
            var window = new Window { Width = 640, Height = 220, Content = new CreditsView() };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(
                Path.Combine(captures, $"credits-{cultureName}.png"),
                PngBitmapEncoderOptions.Default);
            window.Close();
        }
    }

    private static CreditsView Build()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var view = new CreditsView();
        var window = new Window { Width = 640, Height = 220, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
