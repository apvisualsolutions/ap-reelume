// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ApSolutions.LocalMedia.Presentation.Library;

public sealed partial class PosterCardView : UserControl
{
    /// <summary>
    /// How far past the chip the blurred artwork reaches, which is three times the sigma below.
    /// </summary>
    /// <remarks>
    /// A Gaussian at three sigma has spent 99,7 % of its weight, so a box of artwork this much
    /// larger than the chip carries everything the blur can reach into it. It is the negative margin
    /// on ChipBackdrop, and the pill clips the result back to the chip.
    /// </remarks>
    public const double ChipBackdropBleed = 24;

    public PosterCardView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Points the chip's blurred back at the artwork underneath it, at the size the word gives it.
    /// </summary>
    /// <remarks>
    /// The chip's own box, taken out of the artwork and put back where it came from, tiled
    /// <c>FlipXY</c>: the filter specification says a backdrop blur reads the backdrop clipped to
    /// the element's border box and blurs it with <c>edgeMode="mirror"</c>, and a mirrored tile of
    /// that box is exactly that. Without the mirror the blur would pull in the transparent nothing
    /// outside the box and lighten the chip's edges.
    ///
    /// It is written here rather than bound because the chip is as wide as its word, and the word
    /// changes with the language and with the kind.
    /// </remarks>
    private void OnKindChipSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // The chip's own corner inside the artwork, which is its place in the panel they share: no
        // TranslatePoint and no guard around it, because a handler of SizeChanged runs after the
        // arrange that raised it and both of them are in the tree. A guard nothing can take is a
        // branch nothing can cover, which is the shape eng/check-coverage.ps1 keeps catching.
        var corner = KindChip.Bounds.Position;
        var size = e.NewSize;

        ChipBackdrop.Margin = new Thickness(-ChipBackdropBleed);
        ChipBackdrop.Background = new VisualBrush
        {
            Visual = CoverArt,
            SourceRect = new RelativeRect(corner.X, corner.Y, size.Width, size.Height, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(
                ChipBackdropBleed,
                ChipBackdropBleed,
                size.Width,
                size.Height,
                RelativeUnit.Absolute),
            TileMode = TileMode.FlipXY,
            Stretch = Stretch.None,
        };
    }
}
