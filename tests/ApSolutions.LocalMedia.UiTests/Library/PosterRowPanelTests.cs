// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Globalization;

using ApSolutions.LocalMedia.Presentation.Library;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// One row of the library grid: equal cells, every edge on a device pixel, and a short row in the
/// full row's cells.
/// </summary>
/// <remarks>
/// <see cref="LibraryGridTests"/> measures the grid the application draws; this measures the row on
/// its own, including the cases the grid never hands it — which is where a panel that only ever
/// passes through one path keeps its other branches honest.
/// </remarks>
public sealed class PosterRowPanelTests
{
    /// <summary>
    /// Eight tracks of 173.5 snap the way a browser snaps them: half away from zero.
    /// </summary>
    /// <remarks>
    /// These are the edges a browser draws for the prototype's grid at 1500 px. Half to even — what
    /// <c>LayoutHelper.RoundLayoutValue</c> does — would put the third and the seventh a pixel to the
    /// left, and every cover after them with it. On Avalonia's thread although it is arithmetic:
    /// the panel's static constructor registers a property, and this suite gives every test its own
    /// application.
    /// </remarks>
    [AvaloniaFact]
    public void Eight_tracks_of_173_and_a_half_snap_the_way_a_browser_snaps_them()
    {
        var edges = Enumerable.Range(0, 9).Select(index => PosterRowPanel.CellEdge(index, 8, 1388, 1.0));

        Assert.Equal([0d, 174, 347, 521, 694, 868, 1041, 1215, 1388], edges);
    }

    /// <summary>
    /// At every scale the edges land on whole device pixels, start at 0, end at the width, and no
    /// cell is more than one device pixel off another.
    /// </summary>
    /// <remarks>
    /// The widths are this grid's surface at 1600 and 1500 px, and at the 900 px minimum; the
    /// scales are the ones Windows offers between 100 and 175 %. Each width times its scale is a
    /// whole number of device pixels, which is what a laid-out surface always is.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(1488, 9, 1.0)]
    [InlineData(1388, 8, 1.0)]
    [InlineData(1488, 9, 1.25)]
    [InlineData(1388, 8, 1.25)]
    [InlineData(1488, 9, 1.5)]
    [InlineData(1388, 8, 1.5)]
    [InlineData(1488, 9, 1.75)]
    [InlineData(1388, 8, 1.75)]
    [InlineData(788, 4, 1.25)]
    [InlineData(1000, 7, 1.5)]
    public void The_cell_edges_tile_the_row_on_device_pixels(double width, int cells, double scale)
    {
        var device = Enumerable.Range(0, cells + 1)
            .Select(index => PosterRowPanel.CellEdge(index, cells, width, scale) * scale)
            .ToArray();

        Assert.Equal(0, device[0], 6);
        Assert.Equal(width * scale, device[^1], 6);
        Assert.All(device, edge => Assert.Equal(Math.Round(edge), edge, 6));

        var widths = device.Zip(device.Skip(1), (left, right) => right - left).ToArray();
        Assert.True(
            widths.Max() - widths.Min() <= 1 + 1e-6,
            $"at {width} px and {scale:0.00}x the cells run from {widths.Min()} to {widths.Max()} device pixels.");
    }

    /// <summary>
    /// Every cell width survives Avalonia's own rounding, so no card is arranged a pixel wider than
    /// its cell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A child is arranged at the width its panel hands it, and <c>Layoutable.ArrangeCore</c> then
    /// rounds that width UP to the device pixel (<c>LayoutHelper.RoundLayoutSizeUp</c>). A width a
    /// hair over its pixel — floating point, not geometry — would grow by a whole one, overlap the
    /// cell after it, and at the end of the row reach past the surface.
    /// </para>
    /// <para>
    /// That upstream behaviour is what this panel is built around, so it is swept rather than
    /// assumed: three thousand surface widths, one to twelve columns, five scales.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Every_cell_width_survives_the_rounding_avalonia_applies_on_arrange()
    {
        var failures = new List<string>();
        foreach (var scale in new[] { 1.0, 1.25, 1.5, 1.75, 2.0 })
        {
            for (var device = 1000; device <= 4000; device++)
            {
                var width = device / scale;
                for (var cells = 1; cells <= 12; cells++)
                {
                    for (var index = 0; index < cells; index++)
                    {
                        var cell = PosterRowPanel.CellEdge(index + 1, cells, width, scale)
                            - PosterRowPanel.CellEdge(index, cells, width, scale);
                        var arranged = LayoutHelper.RoundLayoutSizeUp(new Size(cell, 0), scale).Width;
                        if (Math.Abs(arranged - cell) > 1e-6)
                        {
                            failures.Add(string.Create(
                                CultureInfo.InvariantCulture,
                                $"{device} device px / {cells} at {scale}x: cell {index} is {cell}, arranged {arranged}"));
                        }
                    }
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            $"{failures.Count} cells would be arranged wider than they are, first: {string.Join("; ", failures.Take(3))}");
    }

    /// <summary>A short row keeps the full row's cells instead of sharing the row among fewer.</summary>
    [AvaloniaFact]
    public void A_short_row_keeps_the_full_rows_cells()
    {
        var (window, panel) = Row(400, 4, Card(), Card());

        Assert.Equal([0d, 100], panel.Children.Select(child => child.Bounds.X));
        Assert.All(panel.Children, child => Assert.Equal(100, child.Bounds.Width));
        window.Close();
    }

    /// <summary>
    /// A row handed more cards than its count squeezes them into the row rather than drawing one
    /// past the edge.
    /// </summary>
    /// <remarks>
    /// The grid never does this, but a count and its rows arrive in two notifications, and a row
    /// caught between them is laid out once in the wrong order.
    /// </remarks>
    [AvaloniaFact]
    public void A_row_handed_more_cards_than_its_count_keeps_them_inside()
    {
        var (window, panel) = Row(300, 2, Card(), Card(), Card());

        Assert.Equal([0d, 100, 200], panel.Children.Select(child => child.Bounds.X));
        Assert.Equal(300, panel.Children[^1].Bounds.Right);
        window.Close();
    }

    /// <summary>A row with nothing in it is as wide as its grid and has no height.</summary>
    [AvaloniaFact]
    public void A_row_with_nothing_in_it_is_as_wide_as_its_grid_and_has_no_height()
    {
        var (window, panel) = Row(300, 3);

        Assert.Equal(new Size(300, 0), panel.DesiredSize);
        window.Close();
    }

    /// <summary>
    /// Asked without a width to share, a row lays its cards out at their own widths, side by side.
    /// </summary>
    /// <remarks>
    /// The grid always gives its rows a width. A panel asked with an infinite one still has to answer
    /// with a finite size — the layout refuses anything else — and side by side is what the
    /// StackPanel this replaced answered.
    /// </remarks>
    [AvaloniaFact]
    public void A_row_asked_without_a_width_lays_its_cards_at_their_own()
    {
        var panel = new PosterRowPanel
        {
            Children =
            {
                new Border { Width = 50, Height = 20 },
                new Border { Width = 60, Height = 30 },
                new Border { Width = 70, Height = 10 },
            },
        };

        panel.Measure(Size.Infinity);

        Assert.Equal(new Size(180, 30), panel.DesiredSize);
    }

    /// <summary>The count is inherited: written once on the grid, read by every row inside it.</summary>
    [AvaloniaFact]
    public void The_count_is_inherited_from_the_grid()
    {
        var (window, panel) = Row(400, 4, Card());

        Assert.Equal(4, PosterRowPanel.GetColumns(panel));
        Assert.Equal(100, panel.Children[0].Bounds.Width);
        window.Close();
    }

    private static Border Card() => new() { Height = 50 };

    /// <summary>
    /// A row inside a grid of <paramref name="width"/>, with the count written on the grid rather
    /// than on the row, which is how the library writes it.
    /// </summary>
    private static (Window Window, PosterRowPanel Panel) Row(double width, int columns, params Control[] cards)
    {
        Assert.NotNull(Avalonia.Application.Current);
        var panel = new PosterRowPanel();
        foreach (var card in cards)
        {
            panel.Children.Add(card);
        }

        var grid = new Border
        {
            Width = width,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = panel,
        };
        PosterRowPanel.SetColumns(grid, columns);

        var window = new Window { Width = width + 100, Height = 400, Content = grid };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, panel);
    }
}
