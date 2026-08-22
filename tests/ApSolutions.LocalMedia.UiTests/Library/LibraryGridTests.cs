// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Library;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// The library grid: it reflows with the window and it virtualises.
/// </summary>
/// <remarks>
/// <para>
/// §4 asks for "a fluid grid with a minimum of 180 px per card", and on 2026-08-20 that was recorded
/// as a discrepancy rather than built, because "nothing in Avalonia 12.1.1 reflows and virtualises at
/// once". That is true of the panels and false of the problem. Measured over ten thousand cards in a
/// 1600 x 1000 window, in Release, on 2026-08-22:
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
    /// <summary>The card is 148 and its button pads 8 on each side, so the step is 164.</summary>
    [Theory]
    [InlineData(1352, 8)]
    [InlineData(900, 5)]
    [InlineData(164, 1)]
    [InlineData(100, 1)]
    [InlineData(0, 1)]
    public void The_column_count_is_how_many_padded_cards_fit(double available, int expected) =>
        Assert.Equal(expected, LibraryView.ColumnsThatFit(available, 148, 8));

    /// <summary>The card's width comes from the theme, and from nowhere else.</summary>
    /// <remarks>
    /// Both answers are asserted because both happen: mounted in the application the grid divides by
    /// the token the card paints itself with, and asked with no host at all it takes the fallback
    /// rather than dividing by zero. The token is asserted to be <b>the same number the card is
    /// drawn at</b>, which is the whole point of it being a token.
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

    private sealed class SinglePageQueryService(CatalogPage page) : ICatalogQueryService
    {
        public Task<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(page);
    }
}
