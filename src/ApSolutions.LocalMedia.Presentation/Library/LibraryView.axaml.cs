// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Commands;
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
            Scalar(this, "Space8", 8));
    }

    /// <summary>
    /// How many cards fit across a width, counting the padding each card carries.
    /// </summary>
    /// <remarks>
    /// The padding counts twice because it is on both sides of every card, and that is the whole
    /// gap: one card's right padding plus the next one's left. Counting the card alone put eight
    /// columns into 1352 px on 2026-08-22 and drew the eighth 72 px past the edge.
    /// </remarks>
    public static int ColumnsThatFit(double available, double cardWidth, double cardPadding) =>
        Math.Max(1, (int)(available / (cardWidth + (cardPadding * 2))));

    /// <summary>
    /// A scalar token's value, or the fallback when there is no theme around to ask.
    /// </summary>
    /// <remarks>
    /// Static and taking its host, so both answers can be asked for: a control outside any
    /// application takes the fallback, and one inside gets the token. Written as a private helper it
    /// had a branch nothing in this repository could reach, which is the shape
    /// <c>eng/check-coverage.ps1</c> keeps catching — and the answer to those is to make the branch
    /// reachable or delete it, never to write it an impossible test.
    /// </remarks>
    public static double Scalar(Control? host, string key, double fallback) =>
        host is not null
        && host.TryFindResource(key, host.ActualThemeVariant, out var value)
        && value is double number
            ? number
            : fallback;

    private void OnCatalogItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _ = e;
        if (DataContext is LibraryViewModel viewModel &&
            sender is Button { DataContext: CatalogItemViewModel item })
        {
            GuardedEvent.Run(() => viewModel.OpenDetailsAsync(item));
        }
    }
}
