// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Commands;
using Avalonia;
using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Library;

public sealed partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Tells the model how many cards fit across, which is the only pixel in this whole grid.
    /// </summary>
    /// <remarks>
    /// The width and the padding are read from the theme rather than written here: the card paints
    /// <c>PosterCardWidth</c> and this divides by it, and a number written in both places would
    /// disagree the first time one of them moved.
    /// </remarks>
    private void OnGridSurfaceSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is not LibraryViewModel viewModel)
        {
            return;
        }

        viewModel.Columns = ColumnsThatFit(
            e.NewSize.Width,
            Scalar(this, "PosterCardWidth", 148),
            Scalar(this, "DensityGutter", 8),
            Scalar(this, "PosterCardBorderThickness", 1));
    }

    /// <summary>
    /// How many cards fit across a width, counting what each card carries on its two sides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The padding counts twice because it is on both sides of every card, and that is the whole
    /// gap: one card's right padding plus the next one's left. Counting the card alone put eight
    /// columns into 1352 px on 2026-08-22 and drew the eighth 72 px past the edge.
    /// </para>
    /// <para>
    /// The border counts twice for the same reason, and leaving it out was the same mistake a size
    /// smaller: every card was 2 px wider than this believed, so within two pixels per column of one
    /// more card it counted a column that did not fit. At 1600 px that drew the ninth cover of every
    /// row 9 px into the page's right margin, measured on 2026-09-11.
    /// </para>
    /// </remarks>
    public static int ColumnsThatFit(double available, double cardWidth, double cardPadding, double cardBorder) =>
        Math.Max(1, (int)(available / (cardWidth + (cardPadding * 2) + (cardBorder * 2))));

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
