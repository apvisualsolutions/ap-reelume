// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Library;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// The library grid: it reflows with the window, its cards stretch to fill the row, and it
/// virtualises.
/// </summary>
/// <remarks>
/// <para>
/// The grid is the prototype's <c>repeat(auto-fill, minmax(148px, 1fr))</c>, and it arrived in two
/// halves. The reflow came on 2026-08-22; the stretch on 2026-09-11, when fixed cards of 148 left
/// 160 px empty at 1600 where the prototype fills the row with nine. §4 once asked for a minimum of
/// 180, and the prototype's code, which is what the tree follows, says 148.
/// </para>
/// <para>
/// The reflow had been recorded as a discrepancy on 2026-08-20, because "nothing in Avalonia 12.1.1
/// reflows and virtualises at once". That is true of the panels and false of the problem. Measured
/// over ten thousand cards in a 1600 x 1000 window, in Release, on 2026-08-22:
/// </para>
/// <list type="table">
/// <item><term><c>WrapPanel</c></term><description>4559 ms, 10 000 live cards</description></item>
/// <item><term>rows of nine in a <c>VirtualizingStackPanel</c></term><description>6 ms, 36</description></item>
/// </list>
/// <para>
/// 760x the time and 278x the live controls. What was missing was never a control Avalonia lacks — it
/// was grouping the items before handing them to the one it has. <c>ItemsRepeater</c> and
/// <c>WrapLayout</c> were the shape being looked for and <b>neither exists in this tree</b>: the
/// <c>Avalonia.Controls.ItemsRepeater</c> package stops at 12.0.0 against a solution pinned to
/// 12.1.1, and <c>WrapLayout</c> is not Avalonia's at all.
/// </para>
/// <para>
/// The virtualisation is asserted on <b>live controls</b> rather than on elapsed time: a timing
/// threshold on a shared runner is a flake, and the count is the thing that actually decides whether
/// ten thousand titles are survivable.
/// </para>
/// <para>
/// This replaces <c>LibraryNavigationTests</c>'s <c>The_library_realises_a_handful_of_rows_out_of_ten_thousand</c>,
/// which measured the same thing about the one-column list and named the fix in its own remarks —
/// "group the items into rows in the view model and let the panel virtualise rows". It reached for a
/// <c>ListBox</c> that no longer exists; what it protected is protected here, over the real card.
/// </para>
/// </remarks>
public sealed class LibraryGridTests
{
    /// <summary>
    /// A column costs the cover's minimum and a gutter on each side: 148 + 2 × 8 = 164.
    /// </summary>
    /// <remarks>
    /// 1476 and 1475 are nine columns and one pixel short of them; 1488 and 1388 are the surface at
    /// 1600 and 1500 px, where the prototype lays out nine and eight. The other two densities move
    /// the step to 156 and 180, and the widest cover the Appearance page offers moves it to 216.
    /// On Avalonia's thread although it is arithmetic: touching <see cref="LibraryView"/> registers
    /// its styled properties, and this suite gives every test its own application.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(1476, 148, 8, 9)]
    [InlineData(1475, 148, 8, 8)]
    [InlineData(1488, 148, 8, 9)]
    [InlineData(1388, 148, 8, 8)]
    [InlineData(1560, 148, 4, 10)]
    [InlineData(1559, 148, 4, 9)]
    [InlineData(1440, 148, 16, 8)]
    [InlineData(1439, 148, 16, 7)]
    [InlineData(1488, 200, 8, 6)]
    [InlineData(164, 148, 8, 1)]
    [InlineData(100, 148, 8, 1)]
    [InlineData(0, 148, 8, 1)]
    public void A_column_costs_the_cover_minimum_and_a_gutter_on_each_side(
        double available,
        double cover,
        double gutter,
        int expected) =>
        Assert.Equal(expected, LibraryView.ColumnsThatFit(available, cover, gutter));

    /// <summary>The card's width comes from the theme, and from nowhere else.</summary>
    /// <remarks>
    /// Both answers are asserted because both happen: mounted in the application a token is read
    /// from the theme, and asked with no host at all it takes the fallback rather than dividing by
    /// zero. The token is asserted to be <b>the same number a card outside the grid is drawn at</b>,
    /// which is the whole point of it being a token: Home's rails paint it, and the grid takes it as
    /// its narrowest column. That the grid follows it when it moves is held by the two tests that
    /// choose a larger cover and a roomier density.
    /// </remarks>
    [AvaloniaFact]
    public void The_step_comes_from_the_theme_and_falls_back_only_without_one()
    {
        Assert.NotNull(Avalonia.Application.Current);
        var view = new LibraryView();
        var window = new Window { Width = 900, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(148, LibraryView.Scalar(view, "PosterCardWidth", -1));
        Assert.Equal(8, LibraryView.Scalar(view, "Space8", -1));
        Assert.Equal(1, LibraryView.Scalar(view, "PosterCardBorderThickness", -1));
        Assert.Equal(-1, LibraryView.Scalar(view, "PosterCornerRadius", -1));
        Assert.Equal(-1, LibraryView.Scalar(view, "NoSuchToken", -1));
        Assert.Equal(-1, LibraryView.Scalar(null, "PosterCardWidth", -1));

        var token = LibraryView.Scalar(view, "PosterCardWidth", -1);
        var card = new PosterCardView();
        window.Content = card;
        Dispatcher.UIThread.RunJobs();
        card.Measure(new Size(900, 600));
        Assert.Equal(token, card.DesiredSize.Width);

        window.Close();
    }

    /// <summary>A view with nobody behind it is asked its size and answers nothing.</summary>
    /// <remarks>
    /// <c>ViewOverflowTests</c> mounts all fifty-one views with no data context, so this path runs on
    /// every one of them; without the guard the grid would reach for a model that is not there the
    /// first time the window is measured.
    /// </remarks>
    [AvaloniaFact]
    public void A_grid_with_no_model_behind_it_survives_being_measured()
    {
        Assert.NotNull(Avalonia.Application.Current);
        var view = new LibraryView();
        var window = new Window { Width = 1352, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Width = 900;
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(view.DataContext);
        window.Close();
    }

    /// <summary>
    /// A grid that was hidden when the window was sized still counts its columns when it appears.
    /// </summary>
    /// <remarks>
    /// This is how the library is actually mounted: the shell opens on Home, so <c>LibraryView</c> is
    /// in the tree with <c>IsVisible</c> false and is never measured. If the column count only ever
    /// came from a resize, somebody who opened the library and never touched the window would see one
    /// column of cards — the state the model starts in — and nothing in this suite would have said so,
    /// because every other test here sets the data context before showing the window.
    /// </remarks>
    [AvaloniaFact]
    public async Task A_grid_that_appears_after_the_window_was_sized_still_counts_its_columns()
    {
        var viewModel = await BrowseAsync(24);
        var view = new LibraryView { IsVisible = false };
        var window = new Window { Width = 1352, Height = 1000, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, viewModel.Columns);

        view.DataContext = viewModel;
        view.IsVisible = true;
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(8, viewModel.Columns);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_grid_reflows_with_the_window_and_never_draws_past_its_edge()
    {
        var viewModel = await BrowseAsync(24);

        foreach (var (width, columns) in new[] { (1352d, 8), (900d, 5) })
        {
            var view = new LibraryView { DataContext = viewModel };
            var window = new Window { Width = width, Height = 1000, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.InvalidateMeasure();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(columns, viewModel.Columns);
            Assert.Equal(columns, viewModel.Rows[0].Count);

            var offside = view.GetVisualDescendants()
                .OfType<PosterCardView>()
                .Select(card => card.TranslatePoint(new Point(card.Bounds.Width, 0), window))
                .Where(right => right is { } point && point.X > width)
                .ToArray();
            Assert.True(
                offside.Length == 0,
                $"{offside.Length} of the cards are drawn past the right edge at {width} px.");

            window.Close();
        }
    }

    /// <summary>
    /// The grid counts the prototype's columns: as many tiles of the chosen cover and its gutter as
    /// fit, right at the edge where one more does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The prototype's grid is <c>repeat(auto-fill, minmax(148px, 1fr))</c> with 16 px between
    /// columns, and its tile is <c>border:1px; padding:8px; margin:-8px</c>: the 148 is the tile,
    /// border included, and the cover inside it is two pixels narrower. So a column costs 148 + 2 × 8
    /// = 164 of this surface, which spans the gutter the tile reaches out by on either side.
    /// </para>
    /// <para>
    /// Until 2026-09-11 the card was a fixed 148 with its border outside, and the grid divided by
    /// 166 because that was what a card measured: at 1600 px it laid out eight and left 160 px of
    /// nothing where the prototype stretches nine to fill the row. The widths here are 164 × N and
    /// one pixel short of it, so each pair sits exactly on the edge the two rules disagree about.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public async Task The_grid_counts_the_prototypes_columns_at_the_edge_of_each()
    {
        var viewModel = await BrowseAsync(40);
        var view = new LibraryView { DataContext = viewModel };
        var window = new Window { Width = 1352, Height = 1000, Content = view };
        window.Show();
        Settle(window);
        var surface = Surface(view);

        foreach (var (width, expected) in new[]
        {
            (1476d, 9), (1475d, 8), (1312d, 8), (1311d, 7), (820d, 5), (819d, 4),
        })
        {
            window.Width = width;
            Settle(window);

            // The surface is the window, gutter taken and given back; if it were not, the widths
            // above would be measuring a different edge from the one they name.
            Assert.Equal(width, surface.Bounds.Width, 3);
            Assert.True(
                viewModel.Columns == expected,
                $"at {width} px the grid counted {viewModel.Columns} columns, and the prototype's "
                    + $"auto-fill lays out {expected}.");
        }

        window.Close();
    }

    /// <summary>
    /// The cards stretch to fill the row the way the prototype's tracks do, and the last row keeps
    /// their width instead of sharing the row out among its few.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 1488 and 1388 are this surface at 1600 and 1500 px. The prototype divides them into nine
    /// tracks of 165.33 and eight of 173.5, and a browser snaps every edge to a pixel, so its cells
    /// come out 165 or 166 and 173 or 174 wide and the row still ends exactly at the edge.
    /// </para>
    /// <para>
    /// The cover is the cell less a gutter and a border on each side, 2 × 8 + 2 × 1 = 18, and its
    /// height is one and a half times its width. It is one height for the whole grid, 221 and 233,
    /// because the browser takes it from the unsnapped track rather than from each cell.
    /// </para>
    /// <para>
    /// And where each cell starts is asserted as the browser draws it, not only that the row closes:
    /// any sharing out of the spare pixels closes the row, and rounding the edges half to even —
    /// Avalonia's own <c>RoundLayoutValue</c> — moves the fourth and the eighth cover at 1500 a pixel
    /// left without opening a gap. The gate audit of 2026-09-11 found this row asserting only widths.
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(1488d, 9, 165d, 166d, 221d, new[] { 0d, 165, 331, 496, 661, 827, 992, 1157, 1323 })]
    [InlineData(1388d, 8, 173d, 174d, 233d, new[] { 0d, 174, 347, 521, 694, 868, 1041, 1215 })]
    public async Task The_cards_fill_the_row_the_way_the_prototypes_tracks_do(
        double width,
        int columns,
        double narrow,
        double wide,
        double coverHeight,
        double[] starts)
    {
        var viewModel = await BrowseAsync(12);
        var view = new LibraryView { DataContext = viewModel };
        var window = new Window { Width = width, Height = 1000, Content = view };
        window.Show();
        Settle(window);
        var surface = Surface(view);
        var rows = Rows(view, surface);

        Assert.Equal(columns, viewModel.Columns);
        Assert.True(
            rows.Length == 2,
            $"twelve titles laid out {rows.Length} rows at {width} px, so no partial row is under test.");
        var full = rows[0];
        Assert.Equal(columns, full.Length);
        Assert.Equal(starts, full.Select(entry => Math.Round(entry.Origin.X, 3)));

        for (var i = 0; i < full.Length; i++)
        {
            var cell = full[i].Card.Bounds.Width;
            Assert.True(
                cell == narrow || cell == wide,
                $"card {i} measured {cell} px at {width}, and a snapped track is {narrow} or {wide}.");

            var end = full[i].Origin.X + cell;
            var next = i + 1 < full.Length ? full[i + 1].Origin.X : surface.Bounds.Width;
            Assert.True(
                Math.Abs(end - next) < 0.01,
                $"at {width} px card {i} ends at {end} and what follows it starts at {next}.");
        }

        // Auto-fill keeps a track's width: the partial row sits in the first row's own cells.
        var partial = rows[1];
        for (var i = 0; i < partial.Length; i++)
        {
            Assert.Equal(full[i].Origin.X, partial[i].Origin.X, 2);
            Assert.Equal(full[i].Card.Bounds.Width, partial[i].Card.Bounds.Width, 2);
        }

        foreach (var (card, _) in full.Concat(partial))
        {
            var cover = Cover(card);
            Assert.Equal(card.Bounds.Width - 18, cover.Bounds.Width, 2);
            Assert.Equal(coverHeight, cover.Bounds.Height, 2);
        }

        window.Close();
    }

    /// <summary>
    /// At 125, 150 and 175 % the cards still tile the row edge to edge, every edge on a whole device
    /// pixel.
    /// </summary>
    /// <remarks>
    /// The arithmetic is swept in <see cref="PosterRowPanelTests"/>; this is the same row laid out by
    /// the real layout, which rounds a child's position to the nearest device pixel and its size up
    /// to the next one. A width a hair over its pixel would come out one wider here and nowhere else.
    /// And the cover's height lands on the device pixel the browser would round 221 × scale to — 276,
    /// 332 and 387 —, which is the one place the view's own reading of the scale shows: taken as 1,
    /// the layout rounds 276.25 up to 277.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(1.25, 276)]
    [InlineData(1.5, 332)]
    [InlineData(1.75, 387)]
    public async Task The_cards_tile_the_row_on_device_pixels_at_every_scale(double scale, int coverPixels)
    {
        var viewModel = await BrowseAsync(12);
        var view = new LibraryView { DataContext = viewModel };
        var window = new Window { Width = 1488, Height = 1000, Content = view };
        window.SetRenderScaling(scale);
        window.Show();
        Settle(window);
        var surface = Surface(view);
        var row = Rows(view, surface)[0];

        Assert.Equal(9, row.Length);
        var edges = row.Select(entry => entry.Origin.X)
            .Append(row[^1].Origin.X + row[^1].Card.Bounds.Width)
            .Select(edge => edge * scale)
            .ToArray();
        Assert.Equal(0, edges[0], 3);
        Assert.Equal(surface.Bounds.Width * scale, edges[^1], 3);
        Assert.All(edges, edge => Assert.Equal(Math.Round(edge), edge, 3));
        for (var i = 0; i < row.Length - 1; i++)
        {
            Assert.Equal(row[i].Origin.X + row[i].Card.Bounds.Width, row[i + 1].Origin.X, 3);
        }

        Assert.All(row, entry => Assert.Equal(coverPixels, Cover(entry.Card).Bounds.Height * scale, 3));
        window.Close();
    }

    /// <summary>
    /// Choosing a larger cover on the Appearance page reflows the grid with no resize to prompt it.
    /// </summary>
    /// <remarks>
    /// With fixed cards a new cover size changed every card, and the resize that followed recounted.
    /// A fluid card takes its width from its cell and its height from this view, so a new minimum
    /// changes no size by itself and nothing is resized: without a count that follows the tokens,
    /// a 176 px minimum would leave nine columns of 147. 1488 over 176 + 16 is 7.75, so seven, and
    /// seven cells of 212.57 leave covers of 194.57, one and a half times which is 292 — asserted on
    /// the covers, because a count that moved and a height that did not would leave seven columns of
    /// the nine-column height. That nothing was resized is asserted too: it is what the test is about.
    /// </remarks>
    [AvaloniaFact]
    public async Task Choosing_a_larger_cover_reflows_the_grid_with_no_resize()
    {
        using var tokens = new TokenScope("PosterCardWidth", "PosterCardHeight");
        var viewModel = await BrowseAsync(12);
        var view = new LibraryView { DataContext = viewModel };
        var window = new Window { Width = 1488, Height = 1000, Content = view };
        window.Show();
        Settle(window);
        Assert.Equal(9, viewModel.Columns);
        var resizes = 0;
        Surface(view).SizeChanged += (_, _) => resizes++;

        tokens.Write("PosterCardWidth", 176d);
        tokens.Write("PosterCardHeight", 264d);
        Settle(window);

        Assert.Equal(0, resizes);
        Assert.Equal(7, viewModel.Columns);
        var row = Rows(view, Surface(view))[0];
        Assert.Equal(7, row.Length);
        Assert.All(row, entry =>
        {
            Assert.Equal(entry.Card.Bounds.Width - 18, Cover(entry.Card).Bounds.Width, 2);
            Assert.Equal(292, Cover(entry.Card).Bounds.Height, 2);
        });
        window.Close();
    }

    /// <summary>
    /// Choosing the roomy density reflows the grid with no resize to prompt it either.
    /// </summary>
    /// <remarks>
    /// The gutter is taken in and given back around the grid, so a view mounted on its own keeps the
    /// same surface width at any density and is never resized by one. 1488 over 148 + 2 × 16 is 8.27,
    /// so eight cells of 186, and a cover of 186 − 32 − 2 = 152 is 228 tall: the gutter enters the
    /// height too, which a height written as «the cell less 18» would miss.
    /// </remarks>
    [AvaloniaFact]
    public async Task Choosing_the_roomy_density_reflows_the_grid_with_no_resize()
    {
        using var tokens = new TokenScope("DensityGutter", "PosterCardPadding", "PosterGutterX", "NegativePosterGutterX");
        var viewModel = await BrowseAsync(12);
        var view = new LibraryView { DataContext = viewModel };
        var window = new Window { Width = 1488, Height = 1000, Content = view };
        window.Show();
        Settle(window);
        Assert.Equal(9, viewModel.Columns);
        var surface = Surface(view).Bounds.Width;
        var resizes = 0;
        Surface(view).SizeChanged += (_, _) => resizes++;

        tokens.Write("DensityGutter", 16d);
        tokens.Write("PosterCardPadding", new Thickness(16));
        tokens.Write("PosterGutterX", new Thickness(16, 0, 16, 0));
        tokens.Write("NegativePosterGutterX", new Thickness(-16, 0, -16, 0));
        Settle(window);

        Assert.Equal(0, resizes);
        Assert.Equal(surface, Surface(view).Bounds.Width);
        Assert.Equal(8, viewModel.Columns);
        Assert.All(Rows(view, Surface(view))[0], entry =>
        {
            Assert.Equal(entry.Card.Bounds.Width - 34, Cover(entry.Card).Bounds.Width, 2);
            Assert.Equal(228, Cover(entry.Card).Bounds.Height, 2);
        });
        window.Close();
    }

    /// <summary>
    /// A resize that leaves the cover's height alone does not tell every card it changed, and one
    /// that moves it does.
    /// </summary>
    /// <remarks>
    /// In the shell this surface is as tall as its rows, so it is resized whenever a page of titles
    /// arrives or a filter narrows the grid, at the same width. A resource written again is a
    /// resource changed, whatever its value, and every card under the surface would hear about it.
    /// The resizes after it are the control, and there are two because what triggers the write has
    /// to be the height and not the count: 1488 to 1476 keeps nine columns and moves the cover from
    /// 221 to 219, and only then 1388 moves both, to eight and 233.
    /// </remarks>
    [AvaloniaFact]
    public async Task A_resize_that_leaves_the_cover_alone_does_not_rewrite_it()
    {
        var viewModel = await BrowseAsync(40);
        var view = new LibraryView { DataContext = viewModel };
        var window = new Window { Width = 1488, Height = 1000, Content = view };
        window.Show();
        Settle(window);
        var surface = Surface(view);
        var before = surface.Bounds.Height;
        var notices = 0;
        surface.ResourcesChanged += (_, _) => notices++;

        window.Height = 800;
        Settle(window);
        Assert.True(surface.Bounds.Height < before, $"the surface stayed {before} px tall, so nothing was resized.");
        Assert.Equal(0, notices);

        window.Width = 1476;
        Settle(window);
        Assert.Equal(9, viewModel.Columns);
        Assert.True(notices > 0, "a resize that kept nine columns and moved the cover wrote nothing.");
        Assert.All(Rows(view, surface)[0], entry => Assert.Equal(219, Cover(entry.Card).Bounds.Height, 2));

        window.Width = 1388;
        Settle(window);
        Assert.Equal(8, viewModel.Columns);
        Assert.All(Rows(view, surface)[0], entry => Assert.Equal(233, Cover(entry.Card).Bounds.Height, 2));
        window.Close();
    }

    /// <summary>A card the keyboard lands on keeps its cover's width, so nothing in its row moves.</summary>
    /// <remarks>
    /// <para>
    /// Every button thickens its border to 2 px under the keyboard (<c>Button:focus-visible</c>), and
    /// on a card the brush stays transparent — the ring a person sees is the focus adorner, which
    /// <c>FocusRingTests</c> holds. On a card that thickening drew nothing and did one thing: it grew
    /// the focused card by 2 px and pushed every card after it along the row.
    /// </para>
    /// <para>
    /// Since the grid became fluid a card always fills its cell, so its own width can no longer show
    /// that: the same two pixels now come out of the cover inside it. This measured the button until
    /// 2026-09-11 and would have stayed green with the border back at two.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public async Task A_card_the_keyboard_lands_on_keeps_its_cover_width()
    {
        var viewModel = await BrowseAsync(8);
        var view = new LibraryView { DataContext = viewModel };
        var window = new Window { Width = 1352, Height = 1000, Content = view };
        window.Show();
        Settle(window);
        var card = Cards(view).First();
        var resting = Cover(card).Bounds.Width;
        Assert.True(resting > 100, $"a cover measured {resting} px wide, so this would compare nothing.");

        Assert.True(card.Focus(NavigationMethod.Tab), "the card refused keyboard focus, so nothing was proven.");
        Settle(window);

        Assert.True(
            card.Classes.Contains(":focus-visible"),
            "focus arrived without :focus-visible, so the state under test never happened.");
        Assert.Equal(resting, Cover(card).Bounds.Width);
        window.Close();
    }

    /// <summary>
    /// A cover is one and a half times what its cell leaves it, on a device pixel.
    /// </summary>
    /// <remarks>
    /// The cell less the gutter and the border on each side, times 1.5. 1485 over nine leaves 147,
    /// whose half-and-a-half is 220.5: a browser rounds that away from zero, to 221, and half to even
    /// would say 220. At 150 % the 233.25 of 1500 px is 349.875 device pixels, so 350 of them. A
    /// surface not yet measured asks for a negative height, and gets none. The last three move the
    /// gutter and the border, which a fixed «less 18» would get wrong: 186 − 32 − 2, 156 − 8 − 2 and
    /// 165.33 − 16 − 4, times 1.5.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(1488, 9, 8, 1, 1.0, 221)]
    [InlineData(1388, 8, 8, 1, 1.0, 233)]
    [InlineData(1485, 9, 8, 1, 1.0, 221)]
    [InlineData(1388, 8, 8, 1, 1.5, 350 / 1.5)]
    [InlineData(0, 1, 8, 1, 1.0, 0)]
    [InlineData(1488, 8, 16, 1, 1.0, 228)]
    [InlineData(1560, 10, 4, 1, 1.0, 219)]
    [InlineData(1488, 9, 8, 2, 1.0, 218)]
    public void A_cover_is_one_and_a_half_times_what_its_cell_leaves_it(
        double available,
        int columns,
        double gutter,
        double border,
        double scale,
        double expected) =>
        Assert.Equal(expected, LibraryView.CoverHeight(available, columns, gutter, border, scale), 6);

    /// <summary>
    /// Ten thousand titles keep a screenful of controls alive, not ten thousand.
    /// </summary>
    /// <remarks>
    /// The ceiling is 400 rather than the 36 that was measured: what matters is that the number is
    /// bounded by the viewport instead of by the catalogue, and pinning it to the exact count would
    /// turn a scroll-buffer change in Avalonia into a red with nothing wrong behind it. Ten thousand
    /// against four hundred is not a threshold anybody has to tune.
    /// </remarks>
    [AvaloniaFact]
    public async Task Ten_thousand_titles_do_not_become_ten_thousand_controls()
    {
        var viewModel = await BrowseAsync(10_000);
        var view = new LibraryView { DataContext = viewModel };
        var window = new Window { Width = 1600, Height = 1000, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        var live = view.GetVisualDescendants().OfType<PosterCardView>().Count();
        Assert.True(
            live is > 0 and < 400,
            $"{live} cards are alive for ten thousand titles, so the grid is not virtualising.");

        window.Close();
    }

    private static IEnumerable<Button> Cards(LibraryView view) =>
        view.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("poster-card"));

    private static ScrollViewer Surface(LibraryView view) =>
        view.GetVisualDescendants().OfType<ScrollViewer>().Single(viewer => viewer.Name == "LibraryGridSurface");

    /// <summary>The cards laid out, row by row from the top, each row from the left.</summary>
    private static (Button Card, Point Origin)[][] Rows(LibraryView view, Visual surface) =>
        [.. Cards(view)
            .Select(card => (Card: card, Origin: card.TranslatePoint(default, surface)!.Value))
            .GroupBy(entry => Math.Round(entry.Origin.Y))
            .OrderBy(row => row.Key)
            .Select(row => row.OrderBy(entry => entry.Origin.X).ToArray())];

    /// <summary>The cover of a card: the rounded frame around its art, hairline included.</summary>
    private static Border Cover(Visual card) =>
        card.GetVisualDescendants().OfType<PosterArtView>().Single().GetVisualAncestors().OfType<Border>().First();

    /// <summary>
    /// A resize moves the count, the count regroups the rows, and the rows need their own pass.
    /// </summary>
    private static void Settle(Window window)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.InvalidateMeasure();
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static async Task<LibraryViewModel> BrowseAsync(int count)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

        var items = Enumerable
            .Range(0, count)
            .Select(index => new CatalogItem(
                new TitleId(Guid.Parse($"00000000-0000-0000-0000-{index:D12}")),
                CatalogTitleKind.Movie,
                string.Create(CultureInfo.InvariantCulture, $"Título Número {index}"),
                2000 + (index % 25),
                true,
                false,
                false,
                DateTimeOffset.UnixEpoch,
                null))
            .ToArray();

        var viewModel = new LibraryViewModel(new SinglePageQueryService(new CatalogPage(items, null)));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        return viewModel;
    }

    /// <summary>
    /// Writes appearance tokens where the Appearance page writes them, and puts back what was there.
    /// </summary>
    /// <remarks>
    /// Not what keeps tests apart: this suite gives every headless test its own application, measured
    /// in <c>ShellSurfaceIsolationTests</c>. It puts the tokens back so each test reads as its own
    /// undo, and stays right if that isolation level ever changes. Written through the current
    /// dictionary rather than one held from construction, because <c>App.ApplyLanguage</c> replaces
    /// it.
    /// </remarks>
    private sealed class TokenScope : IDisposable
    {
        private readonly Avalonia.Application _application;
        private readonly string[] _keys;
        private readonly Dictionary<string, object?> _before = [];

        public TokenScope(params string[] keys)
        {
            Assert.NotNull(Avalonia.Application.Current);
            _application = Avalonia.Application.Current!;
            _keys = keys;
            foreach (var key in keys)
            {
                if (_application.Resources.TryGetValue(key, out var value))
                {
                    _before[key] = value;
                }
            }
        }

        public void Write(string key, object value) => _application.Resources[key] = value;

        public void Dispose()
        {
            foreach (var key in _keys)
            {
                _ = _application.Resources.Remove(key);
                if (_before.TryGetValue(key, out var value))
                {
                    _application.Resources[key] = value;
                }
            }
        }
    }

    private sealed class SinglePageQueryService(CatalogPage page) : ICatalogQueryService
    {
        public Task<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(page);
    }
}
