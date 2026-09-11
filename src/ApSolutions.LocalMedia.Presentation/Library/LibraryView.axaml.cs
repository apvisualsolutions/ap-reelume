// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Commands;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ApSolutions.LocalMedia.Presentation.Library;

public sealed partial class LibraryView : UserControl
{
    /// <summary>The narrowest a cover may be, which is the size chosen on the Appearance page.</summary>
    /// <remarks>
    /// Bound in the markup to <c>PosterCardWidth</c>, and resolved here, above the grid's surface,
    /// where that token still means the chosen size and not the automatic width the cards are given.
    /// </remarks>
    public static readonly StyledProperty<double> MinimumCoverProperty =
        AvaloniaProperty.Register<LibraryView, double>(nameof(MinimumCover), 148);

    /// <summary>Half the room between two covers, which is what the density row chooses.</summary>
    public static readonly StyledProperty<double> GutterProperty =
        AvaloniaProperty.Register<LibraryView, double>(nameof(Gutter), 8);

    /// <summary>The cover height last written for this grid, so an unchanged one is not rewritten.</summary>
    private double _coverHeight = double.NaN;

    public LibraryView()
    {
        InitializeComponent();
    }

    /// <summary>The chosen cover size, as the theme has it where this view stands.</summary>
    /// <remarks>
    /// Read-only here because only the markup's binding writes it, and it does so through the
    /// property itself: a setter nothing called was a line no test could reach.
    /// </remarks>
    public double MinimumCover => GetValue(MinimumCoverProperty);

    /// <summary>The chosen density's gutter, as the theme has it where this view stands.</summary>
    public double Gutter => GetValue(GutterProperty);

    /// <summary>
    /// A new cover size or density recounts the grid, because nothing else would.
    /// </summary>
    /// <remarks>
    /// With fixed cards a new size changed every card and the resize that followed recounted. A
    /// fluid card takes its width from its cell and its height from this view, so a new minimum
    /// changes no size by itself, and a new density takes a gutter in and gives it back around the
    /// surface. Two properties rather than every resource change, because the Appearance page writes
    /// a dozen tokens at a time and the property system only reports the ones that moved.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MinimumCoverProperty || change.Property == GutterProperty)
        {
            Recount();
        }
    }

    private void OnGridSurfaceSizeChanged(object? sender, SizeChangedEventArgs e) => Recount();

    /// <summary>
    /// Tells the model how many columns fit across, and the cards how tall a cover is in them —
    /// the only two pixels in this whole grid.
    /// </summary>
    /// <remarks>
    /// The border is read from the theme rather than written here, and the cover and the gutter
    /// arrive through their properties, so no number lives in two places. The height is only
    /// written when it changes, because a resource written again notifies every card under it
    /// whether or not it moved.
    /// </remarks>
    private void Recount()
    {
        if (DataContext is not LibraryViewModel viewModel)
        {
            return;
        }

        var available = LibraryGridSurface.Bounds.Width;
        var columns = ColumnsThatFit(available, MinimumCover, Gutter);
        viewModel.Columns = columns;

        var height = CoverHeight(
            available,
            columns,
            Gutter,
            Scalar(this, "PosterCardBorderThickness", 1),
            LayoutHelper.GetLayoutScale(LibraryGridSurface));
        if (height != _coverHeight)
        {
            _coverHeight = height;
            LibraryGridSurface.Resources["PosterCardHeight"] = height;
        }
    }

    /// <summary>
    /// How tall a cover is in this grid: one and a half times the width its cell leaves it, on a
    /// device pixel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cell is the width shared out equally; the cover is the cell less a gutter and a border on
    /// each side, which is the prototype's tile with <c>padding:8px; border:1px</c>. It is one height
    /// for the whole grid, taken from the unsnapped cell the way a browser takes an
    /// <c>aspect-ratio</c> from the unsnapped track, even though the cells themselves come out a
    /// pixel apart.
    /// </para>
    /// <para>
    /// Half away from zero, like the cell edges in <see cref="PosterRowPanel"/>; and never below
    /// zero, which is what a surface not yet measured would otherwise ask for.
    /// </para>
    /// </remarks>
    public static double CoverHeight(double available, int columns, double gutter, double border, double scale) =>
        Math.Max(
            0,
            Math.Round(((available / columns) - (gutter * 2) - (border * 2)) * 1.5 * scale, MidpointRounding.AwayFromZero)
                / scale);

    /// <summary>
    /// How many columns the prototype's grid lays out across a width: as many tiles of the chosen
    /// cover and its gutter as fit, every one at least that wide.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The prototype writes <c>repeat(auto-fill, minmax(148px, 1fr))</c>, and its tile is
    /// <c>border:1px; padding:8px; margin:-8px</c>: the 148 is the tile with its border inside, and
    /// the cover is two pixels narrower. So the border is not counted here — it is inside the
    /// minimum — and the gutter is, twice, because this surface spans the one the first and the last
    /// tile reach out by. A column costs 148 + 2 × 8 = 164.
    /// </para>
    /// <para>
    /// Until 2026-09-11 the card was a fixed 148 with its border outside it, and this divided by the
    /// 166 a card measured: at 1600 px, the width the design is drawn at, it laid out eight and left
    /// 160 px of nothing where the prototype stretches nine to fill the row.
    /// </para>
    /// </remarks>
    public static int ColumnsThatFit(double available, double minimumCover, double gutter) =>
        Math.Max(1, (int)(available / (minimumCover + (gutter * 2))));

    /// <summary>
    /// A scalar token's value, or the fallback when there is no theme around to ask.
    /// </summary>
    /// <remarks>
    /// Static and taking its host, so both answers can be asked for: a control outside any
    /// application takes the fallback, and one inside gets the token. Written as a private helper it
    /// had a branch nothing in this repository could reach, which is the shape
    /// <c>eng/check-coverage.ps1</c> keeps catching — and the answer to those is to make the branch
    /// reachable or delete it, never to write it an impossible test. A thickness answers with its
    /// left side: the one read here, the card's border, is the same on all four, and reading the
    /// Thickness the card's style spends keeps it one number instead of two that could disagree.
    /// </remarks>
    public static double Scalar(Control? host, string key, double fallback) =>
        host is not null && host.TryFindResource(key, host.ActualThemeVariant, out var value)
            ? value switch
            {
                double number => number,
                Thickness side => side.Left,
                _ => fallback,
            }
            : fallback;

    /// <summary>Opens the title a card stands for.</summary>
    /// <remarks>
    /// Casts rather than a test of each piece: this handler hangs off the card's button in this
    /// view's own template, whose data is a catalogue item, and a card only exists while the view
    /// shows a library. The three guards it carried until 2026-09-11 were branches nothing could
    /// take — the coverage gate had half of them never run — and the answer to those is to delete
    /// them. The casts happen inside the guard, so the day one fails it fails there, exactly as
    /// quietly as the guards did, and not on the interface thread.
    /// </remarks>
    private void OnCatalogItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _ = e;
        GuardedEvent.Run(() => ((LibraryViewModel)DataContext!).OpenDetailsAsync(
            (CatalogItemViewModel)((Control)sender!).DataContext!));
    }
}
