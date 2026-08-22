// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Recovery;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Recovery;

/// <summary>
/// The screen somebody sees when their library will not open: two paths and one thing that went wrong.
/// </summary>
/// <remarks>
/// <para>
/// It was painting the failure on <c>AccentSubtleBrush</c> — the surface this tree uses for "here is
/// something to consider" — while being the one screen in the application that exists because
/// something broke. §4 gives it <c>DangerSurfaceBrush</c>, and it is right: the detail is not a note,
/// it is the reason the application could not start.
/// </para>
/// <para>
/// The two paths are what somebody reads to go and find their backup by hand, so they are fixed-width
/// and they wrap rather than being cut. <b>Measured on 2026-08-22 with a 105-character UNC path in the
/// 720 column:</b> <c>Wrap</c> and <c>WrapWithOverflow</c> lay out identically — 686 x 33, ending at
/// x=776 in a 900 window — because a backslash is a break opportunity, so §4's worry about overflow
/// does not materialise here. <c>WrapWithOverflow</c> is what the row asks for and what it gets; this
/// is written down so nobody later "fixes" it back believing it makes a difference.
/// </para>
/// </remarks>
public sealed class DatabaseRecoveryLayoutTests
{
    private const string LongUncPath =
        @"\\almacen-de-casa\peliculas\copias\ap-reelume\2026-08-22\biblioteca-completa-antes-de-la-migracion.db";

    /// <summary>
    /// The thing that went wrong is painted as something that went wrong.
    /// </summary>
    [AvaloniaFact]
    public void The_failure_detail_wears_the_danger_surface_and_not_the_accent()
    {
        var (window, view) = Show();

        var surface = Assert.Single(
            view.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "RecoveryFailureSurface");
        Assert.Equal(ThemeColour("DangerSurfaceBrush"), Colour(surface.Background));
        Assert.Equal(ThemeColour("DangerBorderBrush"), Colour(surface.BorderBrush));
        Assert.NotEqual(ThemeColour("DangerSurfaceBrush"), ThemeColour("AccentSubtleBrush"));

        window.Close();
    }

    /// <summary>
    /// Both paths are fixed-width, wrap, and stay inside the narrowest window the application allows.
    /// </summary>
    /// <remarks>
    /// The geometry half is the point of the measurement: this is the screen §4 calls the worst case,
    /// so it is fed a real UNC path rather than a short one and asked where its right edge lands.
    /// </remarks>
    [AvaloniaFact]
    public void Both_paths_are_fixed_width_and_stay_inside_the_narrowest_window()
    {
        var (window, view) = Show(width: 900);
        var mono = Assert.IsType<FontFamily>(Resource("FontFamilyMono"));

        var paths = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => (block.Text ?? string.Empty).Contains("almacen-de-casa", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, paths.Length);
        Assert.All(paths, block =>
        {
            Assert.Equal(mono.Name, block.FontFamily.Name);
            Assert.Equal(TextWrapping.WrapWithOverflow, block.TextWrapping);

            var right = block.TranslatePoint(new Point(block.Bounds.Width, 0), window);
            Assert.True(
                right is { } point && point.X <= window.Bounds.Width,
                $"a path ends at {right} in a {window.Bounds.Width:F0}-wide window.");
        });

        window.Close();
    }

    /// <summary>
    /// The two actions wrap, and the screen owns a heading of its own.
    /// </summary>
    /// <remarks>
    /// A level one, because this is not a section of anything: it is its own window, shown when the
    /// database will not open, and NEXT-SESSION records that the shell deliberately has no route to it.
    /// </remarks>
    [AvaloniaFact]
    public void The_actions_wrap_and_the_screen_owns_its_heading()
    {
        var (window, view) = Show();

        Assert.DoesNotContain(
            view.GetVisualDescendants().OfType<StackPanel>(),
            panel => panel.Orientation == Orientation.Horizontal
                && panel.GetVisualChildren().OfType<Button>().Count() > 1);

        var heading = Assert.Single(
            view.GetVisualDescendants().OfType<TextBlock>(),
            block => (int)AutomationProperties.GetHeadingLevel(block) > 0);
        Assert.Equal(1, (int)AutomationProperties.GetHeadingLevel(heading));

        window.Close();
    }

    private static (Window Window, DatabaseRecoveryView View) Show(double width = 1000)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var view = new DatabaseRecoveryView
        {
            DataContext = new DatabaseRecoveryViewModel(
                LongUncPath,
                LongUncPath,
                "SQLite error 11: database disk image is malformed"),
        };
        var window = new Window { Width = width, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static Color Colour(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static Color ThemeColour(string key)
    {
        var application = Avalonia.Application.Current!;
        Assert.True(
            application.TryGetResource(key, application.ActualThemeVariant, out var value),
            $"{key} is not declared in this theme, so nothing can paint it.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    private static object Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        Assert.NotNull(value);
        return value!;
    }
}
