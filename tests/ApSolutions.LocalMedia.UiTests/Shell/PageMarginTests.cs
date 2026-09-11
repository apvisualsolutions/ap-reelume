// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Home;
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
        Assert.Equal(5, pages.Length);
        Assert.All(pages, page =>
        {
            var origin = scope.Offset(page);
            Assert.Equal(scope.RailRight + 32, origin.X, 1);
            Assert.Equal(scope.ContentTop + 28, origin.Y, 1);
        });

        var settingsTitle = scope.View.GetVisualDescendants().OfType<TextBlock>().First(
            block => block.Text == Scope.Resource("NavigationSettings"));
        Assert.Equal(scope.RailRight + 32, scope.Offset(settingsTitle).X, 1);

        // Home's rows, which the shape above cannot see: its page is a HomeView and not a panel. The
        // comment above said Home was held below and nothing held it, measured by a gate audit on
        // 2026-09-11 that put the rows back at 24 with every test green.
        var home = scope.View.GetVisualDescendants().OfType<HomeView>().Single();
        var rows = home.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.Name is "InProgress" or "RecentlyAdded")
            .ToArray();
        Assert.Equal(2, rows.Length);
        Assert.All(rows, row => Assert.Equal(scope.RailRight + 32, scope.Offset(row).X, 1));

        // And the welcome the shell shows before a library exists, which is a grid of its own.
        var welcome = scope.View.GetVisualDescendants().OfType<TextBlock>().Single(
            block => block.Text == Scope.Resource("WelcomeTitle"));
        Assert.Equal(scope.RailRight + 32, scope.Offset(welcome).X, 1);
        Assert.Equal(scope.ContentTop + 28, scope.Offset(welcome).Y, 1);
    }

    /// <summary>
    /// The prototype's tile is `border:1px; padding:8px; margin:-8px`, and this is the same
    /// arrangement: the grid starts one gutter before the title, and the first cover lands one border
    /// inside the title's line, where the prototype's does.
    /// </summary>
    /// <remarks>
    /// Measured on a real cover. Until 2026-09-11 this ran with no library behind the page, so there
    /// were no cards and it measured only the grid's surface: a gate audit that day moved every cover
    /// 8 px to the right of the title — the very defect this page's margin work removed — and it stayed
    /// green.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_library_grid_reaches_out_by_one_gutter_so_the_first_cover_meets_the_title()
    {
        using var scope = new Scope();
        var library = scope.View.GetVisualDescendants().OfType<LibraryView>().First();
        var catalogue = new LibraryViewModel(new OnePage(12));
        await catalogue.LoadAsync(TestContext.Current.CancellationToken);
        library.DataContext = catalogue;
        scope.Settle();

        var title = library.GetVisualDescendants().OfType<TextBlock>().First(
            block => block.Text == Scope.Resource("LibraryTitle"));
        var grid = library.GetVisualDescendants().OfType<ScrollViewer>().Single(
            viewer => viewer.Name == "LibraryGridSurface");
        var cover = library.GetVisualDescendants().OfType<PosterCardView>().FirstOrDefault();
        var gutter = Assert.IsType<Thickness>(Scope.ResourceValue("PosterCardPadding")).Left;
        var border = Assert.IsType<Thickness>(Scope.ResourceValue("PosterCardBorderThickness")).Left;

        Assert.True(gutter > 0, "the card padding resolved to nothing, so this would prove nothing.");
        Assert.True(cover is not null, "the library laid out no cards, so there is no cover to measure.");
        Assert.Equal(scope.RailRight + 32, scope.Offset(title).X, 1);

        // A cover sits one gutter inside its card, so the grid starts one gutter before the title...
        Assert.Equal(scope.Offset(title).X - gutter, scope.Offset(grid).X, 1);

        // ...and the card's own border puts the cover one pixel inside the title's line.
        Assert.Equal(scope.Offset(title).X + border, scope.Offset(cover!).X, 1);
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

        /// <summary>A data context arrives, the grid counts its columns, and the rows need a pass.</summary>
        internal void Settle()
        {
            for (var pass = 0; pass < 3; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                _window.InvalidateMeasure();
            }

            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => _window.Close();
    }

    private sealed class OnePage(int count) : ICatalogQueryService
    {
        public Task<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CatalogPage(
                [.. Enumerable.Range(0, count).Select(index => new CatalogItem(
                    new TitleId(Guid.Parse($"00000000-0000-0000-0000-{index:D12}")),
                    CatalogTitleKind.Movie,
                    string.Create(CultureInfo.InvariantCulture, $"Título {index}"),
                    2000 + index,
                    true,
                    false,
                    false,
                    DateTimeOffset.UnixEpoch,
                    null))],
                null));
    }
}
