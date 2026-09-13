// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

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

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// The vertical rhythm of a card, counted in pixels against the prototype's own capture.
/// </summary>
/// <remarks>
/// <para>
/// The prototype was measured on 2026-09-12 with headless Chrome at both widths and all three
/// densities, in the DOM and in pixels (<c>docs/evidence/stable/audit-poster-card-rhythm.md</c>).
/// Four distances came out of it, and only the last one moves with the density:
/// </para>
/// <list type="bullet">
/// <item>the filter row to the first cover: <b>17</b>, at every width and density;</item>
/// <item>the cover to the title's line box: <b>10</b>, the tile's own row gap;</item>
/// <item>the three lines, stacked: line boxes of 20,25 and 17,25, the last one 3 px lower;</item>
/// <item>the last line to the next cover: the grid's <b>row gap plus 2</b> — 14, 20 and 28.</item>
/// </list>
/// <para>
/// Both instruments are read. The boxes are the prototype's design, and the ink is what a person
/// sees: this tree drew every box right on 2026-09-11 and was still 5 px taller per row, because
/// the spacing between the lines was a token nobody had compared with anything.
/// </para>
/// </remarks>
public sealed class PosterCardRhythmTests
{
    /// <summary>
    /// Darker than this in all three channels is ink, and the page's own background is not.
    /// </summary>
    /// <remarks>
    /// Two hundred and not the 110 the covers are counted with: the two quiet lines under a title
    /// are <c>TextSecondaryBrush</c>, 90,102,117, and their blue channel is over 110 — with that
    /// threshold the scan found a cover, a title and nothing else, which is exactly the shape of a
    /// test that measures a band it cannot see. The page behind them is 245,247,250.
    /// </remarks>
    private const int Threshold = 200;

    /// <summary>The ink gaps the prototype's capture holds, at 1600 px and the comfortable density.</summary>
    /// <remarks>
    /// Cover to title 15, title to meta 5, meta to status 8, status to the next cover 22. They shift
    /// a pixel between widths — the text block's subpixel phase follows the cover's fractional
    /// height — which is why each is compared with a pixel of slack and the boxes are compared
    /// exactly.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(1500d, 8, 233, 15, 6, 8)]
    [InlineData(1600d, 9, 221, 15, 5, 8)]
    public async Task The_three_lines_under_a_cover_stand_where_the_prototype_puts_them(
        double width,
        int columns,
        int coverHeight,
        int toTitle,
        int titleToMeta,
        int metaToStatus)
    {
        using var scope = await Scope.LibraryAsync(width);

        var cards = scope.Cards();
        Assert.True(cards.Count > columns, $"only {cards.Count} cards were built, so there is no second row to measure.");

        var first = cards[0];
        var cover = scope.Cover(first);
        Assert.Equal(coverHeight, cover.Height, 0);

        // The boxes: the prototype's tile, line for line.
        var lines = scope.Lines(first);
        Assert.Equal(3, lines.Count);
        Assert.Equal(10d, lines[0].Y - (cover.Y + cover.Height), 2);
        Assert.Equal(20.25, lines[0].Height, 2);
        Assert.Equal(0d, lines[1].Y - (lines[0].Y + lines[0].Height), 2);
        Assert.Equal(17.25, lines[1].Height, 2);
        Assert.Equal(3d, lines[2].Y - (lines[1].Y + lines[1].Height), 2);
        Assert.Equal(17.25, lines[2].Height, 2);

        // The ink: the same tile as a person sees it.
        var band = scope.Ink((int)cover.X + 4, (int)(cover.X + cover.Width) - 4, (int)cover.Y, (int)cover.Y + 400);
        Assert.True(band.Count >= 5, $"only {band.Count} bands of ink were found under the first cover; there should be a cover, three lines and the next cover.");
        Assert.Equal(coverHeight, band[0].End - band[0].Start + 1);

        AssertGap(toTitle, band[1].Start - band[0].End - 1, "the cover and the title");
        AssertGap(titleToMeta, band[2].Start - band[1].End - 1, "the title and the meta line");
        AssertGap(metaToStatus, band[3].Start - band[2].End - 1, "the meta line and the status");

        // The row's own step, which is the one distance here that no typeface can move: the
        // prototype's rule is cover + 69,75 + the row gap — its 10 px gap, its 57,75 of text and
        // the two transparent borders — and its capture paints 309 at this width and 321 at 1500.
        Assert.Equal(
            Math.Round(cover.Height + 69.75 + 18),
            scope.Cover(cards[columns]).Y - cover.Y,
            0);
    }

    /// <summary>
    /// Every density keeps the filters 17 px above the grid and steps its rows by the prototype's
    /// own gap.
    /// </summary>
    /// <remarks>
    /// Both hold at every density in the prototype, because its tile keeps its 8 px of padding and
    /// only the grid's gap changes (<c>:3549</c>). This tree padded the tile with the density gutter
    /// instead, so the first cover sat 21 px under the filters at the comfortable density and 29 at
    /// the roomy one, and its rows stepped 11, 19 and 35 where the prototype steps 14, 20 and 28
    /// past the text.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(4d, 12d)]
    [InlineData(8d, 18d)]
    [InlineData(16d, 26d)]
    public async Task Every_density_keeps_the_filters_seventeen_px_above_the_grid(double gutter, double rowGap)
    {
        using var scope = await Scope.LibraryAsync(1600, gutter, rowGap);

        var cards = scope.Cards();
        var columns = scope.Columns();
        Assert.True(cards.Count > columns, $"only {cards.Count} cards were built in {columns} columns, so there is no second row.");

        var cover = scope.Cover(cards[0]);
        var filters = scope.Filters();
        Assert.Equal(17d, cover.Y - (filters.Y + filters.Height), 0);

        Assert.Equal(
            Math.Round(cover.Height + 69.75 + rowGap),
            scope.Cover(cards[columns]).Y - cover.Y,
            0);
    }

    /// <summary>
    /// One of the prototype's ink gaps, with the slack two typefaces need.
    /// </summary>
    /// <remarks>
    /// Two pixels, and both of them are measured rather than guessed. The prototype's own gaps move
    /// a pixel between widths and densities — the text block's subpixel phase follows the cover's
    /// fractional height, and at twice the device scale the same distances read 15,5, 5,0 and 8,5 —
    /// and the second pixel is the typeface: the prototype is drawn in Segoe UI Variable Text and
    /// this application in Segoe UI, whose vertical metrics are identical (both measure 17,96 tall
    /// with a baseline at 14,57 at 13,5 px, measured on 2026-09-12) but whose ink is not: the same
    /// «Vidrio Templado» inks 13 rows here and 15 there. The line BOXES are asserted exactly, above,
    /// and the family is registered as its own piece of work.
    ///
    /// What this guards is 4 px and more — the 8 px gutter where 10 belongs, the 8 px of Space8
    /// between three lines the prototype stacks — so two pixels of slack cannot swallow one.
    /// </remarks>
    private static void AssertGap(int expected, int measured, string between) =>
        Assert.True(
            Math.Abs(measured - expected) <= 2,
            $"{between} there are {measured} px of page and the prototype leaves {expected}.");

    private sealed class Scope : IDisposable
    {
        private readonly Window _window;

        private Scope(Window window, ShellView view)
        {
            _window = window;
            View = view;
        }

        internal ShellView View { get; }

        internal static async Task<Scope> LibraryAsync(double width, double gutter = 8, double rowGap = 18)
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

            var navigation = new NavigationService();
            var catalogue = new LibraryViewModel(new OnePage(24));
            await catalogue.LoadAsync(TestContext.Current.CancellationToken);
            var view = new ShellView
            {
                DataContext = new ShellViewModel(navigation, new ShellSurfaces { Library = catalogue }),
            };
            var window = new Window { Width = width, Height = 1200, Content = view };
            window.Show();
            navigation.Navigate(AppRoute.Library);

            // The scene's own colours: a black cover on the page's own background, no artwork and no
            // shadow, so a threshold finds a cover and three lines of text and nothing else.
            window.Resources["ControlFillBrush"] = Brushes.Black;
            window.Resources["ShellHairlineBrush"] = Brushes.Black;
            window.Resources["PosterInitialsBrush"] = Brushes.Black;
            window.Resources["PosterArtOpacity"] = 0d;
            window.Resources["ElevationShadow"] = default(BoxShadows);
            window.Resources["DensityGutter"] = gutter;
            window.Resources["DensityRowGap"] = rowGap;

            var scope = new Scope(window, view);
            scope.Settle();
            return scope;
        }

        internal List<PosterCardView> Cards() =>
            [.. View.GetVisualDescendants().OfType<LibraryView>().First()
                .GetVisualDescendants().OfType<PosterCardView>()];

        internal Rect Cover(PosterCardView card)
        {
            var art = card.GetVisualDescendants().OfType<PosterArtView>().First();
            var cover = art.GetVisualAncestors().OfType<Border>().First();
            return new Rect(Offset(cover), cover.Bounds.Size);
        }

        /// <summary>How many columns the grid counted, which is what the model was told.</summary>
        internal int Columns() =>
            ((LibraryViewModel)View.GetVisualDescendants().OfType<LibraryView>().First().DataContext!).Columns;

        internal Rect Filters()
        {
            var filters = View.GetVisualDescendants().OfType<WrapPanel>()
                .First(panel => panel.Name == "LibraryFilterSurface");
            return new Rect(Offset(filters), filters.Bounds.Size);
        }

        /// <summary>The three lines under a cover, in the order they are stacked.</summary>
        internal List<Rect> Lines(PosterCardView card) =>
            [.. card.GetVisualDescendants().OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible && block.Text?.Length > 0)
                .Select(block => new Rect(Offset(block), block.Bounds.Size))
                .Where(box => box.Y > Cover(card).Y + Cover(card).Height)
                .OrderBy(box => box.Y)];

        /// <summary>The bands of rows that carry ink between two columns of the frame.</summary>
        internal List<(int Start, int End)> Ink(int fromX, int toX, int fromY, int toY)
        {
            using var frame = _window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("the headless backend returned no frame.");
            using var buffer = frame.Lock();
            var pixels = new byte[buffer.RowBytes * frame.PixelSize.Height];
            Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);

            var bands = new List<(int Start, int End)>();
            var start = -1;
            for (var y = fromY; y < Math.Min(toY, frame.PixelSize.Height); y++)
            {
                var ink = false;
                for (var x = fromX; x < toX && !ink; x++)
                {
                    var index = (y * buffer.RowBytes) + (x * 4);
                    ink = pixels[index] < Threshold
                        && pixels[index + 1] < Threshold
                        && pixels[index + 2] < Threshold;
                }

                if (ink && start < 0)
                {
                    start = y;
                }
                else if (!ink && start >= 0)
                {
                    bands.Add((start, y - 1));
                    start = -1;
                }
            }

            return bands;
        }

        internal Point Offset(Visual visual) =>
            visual.TranslatePoint(default, View) ?? throw new InvalidOperationException(
                $"{visual.GetType().Name} is not under the shell it was looked for in.");

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

    /// <summary>
    /// The prototype's own tile, seeded word for word.
    /// </summary>
    /// <remarks>
    /// The words matter to the pixel here, and only here: a gap between two lines of ink is measured
    /// from the descender of the line above, so «Título 1» — which has none — reads 9 px where
    /// «Vidrio Templado» reads 5. The three lines are the prototype's demo title, its
    /// «2024 · 111 min · Suspense» and the «Sin empezar» both of them share.
    /// </remarks>
    private sealed class OnePage(int count) : ICatalogQueryService
    {
        public Task<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CatalogPage(
                [.. Enumerable.Range(0, count).Select(index => new CatalogItem(
                    new TitleId(Guid.Parse($"00000000-0000-0000-0000-{index:D12}")),
                    CatalogTitleKind.Movie,
                    "Vidrio Templado",
                    2024,
                    true,
                    false,
                    false,
                    DateTimeOffset.UnixEpoch,
                    null,
                    TimeSpan.FromMinutes(111),
                    ["Suspense"]))],
                null));
    }
}
