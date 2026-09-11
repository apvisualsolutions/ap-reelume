// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// Every destination opens its page where the prototype opens it: 32 px from the rail and 28 under
/// the title bar, and the library's covers start where its title starts.
/// </summary>
/// <remarks>
/// <para>
/// The prototype writes the page once, `padding:28px 32px 48px` on the single scrolling container
/// its pages share, and seven of its pages measured on 2026-09-11 all open at x = 96, which is 32
/// past its 64 px rail. This tree wrote Margin="48" on each destination instead, and the library's
/// covers carried their own 8 px gutter on top: 56 px from the rail where the prototype has 32.
/// </para>
/// <para>
/// Measured on the shell itself with no data context, which leaves every destination visible at
/// once. That is layout and not ink, so what it holds is the arrangement a regression would break;
/// what a person sees was counted in pixels on captures of the application, and is in the evidence.
/// </para>
/// </remarks>
public sealed class PageMarginTests
{
    [AvaloniaFact]
    public void Every_destination_opens_its_page_32_px_from_the_rail_and_28_under_the_bar()
    {
        using var scope = new Scope();

        var pages = scope.View.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .Where(viewer => Grid.GetRow(viewer) == 1 && Grid.GetColumn(viewer) == 1)
            .Select(viewer => viewer.Content)
            .OfType<StackPanel>()
            .ToArray();

        // Library, editor, review, duplicates and courses. Home has no page of its own around the
        // hero, which bleeds, and Settings is a grid of its own; both are held below by what they
        // draw rather than by this shape.
        Assert.True(pages.Length >= 5, $"only {pages.Length} destination pages were found.");
        Assert.All(pages, page =>
        {
            var origin = scope.Offset(page);
            Assert.Equal(scope.RailRight + 32, origin.X, 1);
            Assert.Equal(scope.ContentTop + 28, origin.Y, 1);
        });

        var settingsTitle = scope.View.GetVisualDescendants().OfType<TextBlock>().First(
            block => block.Text == Scope.Resource("NavigationSettings"));
        Assert.Equal(scope.RailRight + 32, scope.Offset(settingsTitle).X, 1);
    }

    /// <summary>
    /// The prototype's tile is `padding:8px; margin:-8px`, and this is the same arrangement: the grid
    /// starts one gutter before the title, because the cover sits one gutter inside its card.
    /// </summary>
    [AvaloniaFact]
    public void The_library_grid_reaches_out_by_one_gutter_so_the_first_cover_meets_the_title()
    {
        using var scope = new Scope();
        var library = scope.View.GetVisualDescendants().OfType<LibraryView>().First();
        var title = library.GetVisualDescendants().OfType<TextBlock>().First(
            block => block.Text == Scope.Resource("LibraryTitle"));
        var grid = library.GetVisualDescendants().OfType<ScrollViewer>().Single(
            viewer => viewer.Name == "LibraryGridSurface");
        var gutter = Assert.IsType<Thickness>(Scope.ResourceValue("PosterCardPadding")).Left;

        Assert.True(gutter > 0, "the card padding resolved to nothing, so this would prove nothing.");
        Assert.Equal(scope.RailRight + 32, scope.Offset(title).X, 1);

        // A cover sits one gutter inside its card, so the grid starts one gutter before the title.
        Assert.Equal(scope.Offset(title).X - gutter, scope.Offset(grid).X, 1);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Window _window;

        internal Scope()
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
            View = new ShellView();
            _window = new Window { Width = 1500, Height = 1000, Content = View };
            _window.Show();
            Dispatcher.UIThread.RunJobs();

            var rail = View.GetVisualDescendants().OfType<Border>().Single(
                border => border.Name == "NavigationRailSurface");
            RailRight = Offset(rail).X + rail.Bounds.Width;
            ContentTop = Offset(rail).Y;
        }

        internal ShellView View { get; }

        /// <summary>Where the rail ends, which is where every page is measured from.</summary>
        internal double RailRight { get; }

        /// <summary>The top of the row the rail and the pages share, under the title bar.</summary>
        internal double ContentTop { get; }

        internal static string Resource(string key) => Assert.IsType<string>(ResourceValue(key));

        internal static object ResourceValue(string key)
        {
            Assert.True(
                Avalonia.Application.Current!.TryFindResource(key, out var value),
                $"{key} is not declared, so nothing can paint it.");
            return value!;
        }

        internal Point Offset(Visual visual) =>
            visual.TranslatePoint(default, View) ?? throw new InvalidOperationException(
                $"{visual.GetType().Name} is not under the shell it was looked for in.");

        public void Dispose() => _window.Close();
    }
}
