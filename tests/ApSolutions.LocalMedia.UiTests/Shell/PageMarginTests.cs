// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Runtime.InteropServices;

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
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

    /// <summary>
    /// The library's last cover ends one border inside the page's right margin, the mirror of the
    /// first, at the two widths the design is measured at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The prototype stretches its tracks to fill the row, so its covers stop 32 px from the window
    /// on the right exactly as they start 32 px from the rail on the left. This application laid out
    /// fixed cards until 2026-09-11 and stopped about 160 px short at 1600.
    /// </para>
    /// <para>
    /// On the shell and not on a view mounted alone, so the grid is as wide as the page really makes
    /// it. The page's right edge is read off the header's primary action, which closes the title row
    /// against it, rather than assumed — and asserted to be 32 in from the window before anything is
    /// measured against it.
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(1500d, 8)]
    [InlineData(1600d, 9)]
    public async Task The_library_grid_fills_its_row_to_the_pages_right_margin(double width, int columns)
    {
        using var scope = new Scope(width);
        var library = scope.View.GetVisualDescendants().OfType<LibraryView>().First();
        var catalogue = new LibraryViewModel(new OnePage(12));
        await catalogue.LoadAsync(TestContext.Current.CancellationToken);
        library.DataContext = catalogue;
        scope.Settle();

        var title = library.GetVisualDescendants().OfType<TextBlock>().First(
            block => block.Text == Scope.Resource("LibraryTitle"));
        // The details cards mount their own primary actions, hidden while the grid is browsed.
        var action = library.GetVisualDescendants().OfType<Button>().Single(
            button => button.Classes.Contains("primary-action") && button.IsEffectivelyVisible);
        var border = Assert.IsType<Thickness>(Scope.ResourceValue("PosterCardBorderThickness")).Left;
        var covers = library.GetVisualDescendants().OfType<PosterCardView>().ToArray();
        var top = covers.Min(cover => scope.Offset(cover).Y);
        var row = covers
            .Where(cover => Math.Abs(scope.Offset(cover).Y - top) < 0.5)
            .OrderBy(cover => scope.Offset(cover).X)
            .ToArray();
        var pageRight = scope.Offset(action).X + action.Bounds.Width;

        Assert.Equal(width - 32, pageRight, 1);
        Assert.Equal(columns, catalogue.Columns);
        Assert.Equal(columns, row.Length);
        Assert.Equal(scope.Offset(title).X + border, scope.Offset(row[0]).X, 1);
        Assert.Equal(pageRight - border, scope.Offset(row[^1]).X + row[^1].Bounds.Width, 1);
    }

    /// <summary>
    /// Counted in pixels, the covers of a full row sit the same distance inside the page on both
    /// sides, and every one of them is the height the grid gave it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The test above measures the arrangement; this counts what is drawn, because a pixel past an
    /// edge is exactly what an arrangement can hide. The scene paints its own colours — solid black
    /// covers with a black hairline, no art, no shadow — so one threshold tells a cover from the page
    /// in any theme, and the edges found are the covers' outer edges: 32 of page margin and the
    /// tile's 1 px border, 33 on each side. The prototype measures 34 because its hairline is light
    /// and the threshold it was counted with trims it; the symmetry is the same.
    /// </para>
    /// <para>
    /// A blank frame would pass a comparison of two -1s, so the scan has to find one run of ink per
    /// column before anything is compared. The height is read down a column 12 px inside each
    /// cover's right edge: past its rounded corner, and clear of the kind chip in the other one.
    /// </para>
    /// <para>
    /// Unlike the tests above, the shell has a model and has navigated to the library: with none,
    /// every destination is visible at once and the frame is five pages drawn over each other —
    /// which the arrangement does not mind and a scan of pixels does. Its first run found two covers.
    /// </para>
    /// <para>
    /// The widths are the sequence the prototype draws, measured on its own capture the same day:
    /// alternating at 1500, one wide cover in every three at 1600. Any sharing out of the spare
    /// pixels closes the row; only the browser's puts the wide ones where it does.
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(1500d, 8, 233, new[] { 156, 155, 156, 155, 156, 155, 156, 155 })]
    [InlineData(1600d, 9, 221, new[] { 147, 148, 147, 147, 148, 147, 147, 148, 147 })]
    public async Task Counted_in_pixels_the_covers_sit_33_px_inside_the_page_on_both_sides(
        double width,
        int columns,
        int coverHeight,
        int[] widths)
    {
        var navigation = new NavigationService();
        var catalogue = new LibraryViewModel(new OnePage(12));
        await catalogue.LoadAsync(TestContext.Current.CancellationToken);
        using var scope = new Scope(width, new ShellViewModel(navigation, new ShellSurfaces { Library = catalogue }));
        navigation.Navigate(AppRoute.Library);
        scope.Window.Resources["ControlFillBrush"] = Brushes.Black;
        scope.Window.Resources["ShellHairlineBrush"] = Brushes.Black;
        scope.Window.Resources["PosterInitialsBrush"] = Brushes.Black;
        scope.Window.Resources["PosterArtOpacity"] = 0d;
        scope.Window.Resources["ElevationShadow"] = default(BoxShadows);
        scope.Settle();
        var library = scope.View.GetVisualDescendants().OfType<LibraryView>().First();
        Assert.Same(catalogue, library.DataContext);

        var first = library.GetVisualDescendants().OfType<PosterCardView>().First();
        var middle = (int)(scope.Offset(first).Y + (coverHeight / 2.0));
        using var frame = scope.Window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("the headless backend returned no frame.");
        using var buffer = frame.Lock();
        var pixels = new byte[buffer.RowBytes * frame.PixelSize.Height];
        Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);

        var runs = InkRuns(pixels, buffer.RowBytes, middle, (int)scope.RailRight + 1, (int)width - 10);
        Assert.True(
            runs.Count == columns,
            $"at {width} px the row across the covers holds {runs.Count} runs of ink and the grid laid out {columns}.");

        Assert.Equal(33, runs[0].Start - scope.RailRight, 0);
        Assert.Equal(33, width - (runs[^1].End + 1), 0);
        Assert.Equal(widths, runs.Select(run => run.End - run.Start + 1));
        Assert.All(runs.Zip(runs.Skip(1)), pair => Assert.Equal(18, pair.Second.Start - pair.First.End - 1));
        Assert.All(runs, run =>
        {
            // Past the cover's 10 px corner, which took two pixels off the top and the bottom of a
            // column read 4 px in; and clear of the kind chip, which sits in the other corner.
            var column = run.End - 12;
            var top = middle;
            var bottom = middle;
            while (IsInk(pixels, buffer.RowBytes, column, top - 1))
            {
                top--;
            }

            while (IsInk(pixels, buffer.RowBytes, column, bottom + 1))
            {
                bottom++;
            }

            Assert.Equal(coverHeight, bottom - top + 1);
        });
    }

    /// <summary>The runs of ink along one row of the frame, between two columns.</summary>
    private static List<(int Start, int End)> InkRuns(byte[] pixels, int rowBytes, int row, int from, int to)
    {
        var runs = new List<(int Start, int End)>();
        var start = -1;
        for (var x = from; x < to; x++)
        {
            var ink = IsInk(pixels, rowBytes, x, row);
            if (ink && start < 0)
            {
                start = x;
            }
            else if (!ink && start >= 0)
            {
                runs.Add((start, x - 1));
                start = -1;
            }
        }

        return runs;
    }

    /// <summary>Darker than 110 in all three channels, which nothing on the page is but a cover.</summary>
    private static bool IsInk(byte[] pixels, int rowBytes, int x, int y)
    {
        var i = (y * rowBytes) + (x * 4);
        return pixels[i] < 110 && pixels[i + 1] < 110 && pixels[i + 2] < 110;
    }

    private sealed class Scope : IDisposable
    {
        private readonly Window _window;

        internal Scope(double width = 1500, ShellViewModel? model = null)
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
            View = model is null ? new ShellView() : new ShellView { DataContext = model };
            _window = new Window { Width = width, Height = 1000, Content = View };
            _window.Show();
            Dispatcher.UIThread.RunJobs();

            var rail = View.GetVisualDescendants().OfType<Border>().Single(
                border => border.Name == "NavigationRailSurface");
            RailRight = Offset(rail).X + rail.Bounds.Width;
            ContentTop = Offset(rail).Y;
        }

        internal ShellView View { get; }

        internal Window Window => _window;

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
