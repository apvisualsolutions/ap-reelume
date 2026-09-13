// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ApSolutions.LocalMedia.Presentation.Library;

/// <summary>
/// One row of the library grid: its width shared out into equal cells, every edge on a device pixel.
/// </summary>
/// <remarks>
/// <para>
/// The prototype's grid is <c>repeat(auto-fill, minmax(148px, 1fr))</c>: every track the same
/// fraction of the row, the row filled to its edge, and the browser snapping each track's edge to a
/// pixel — so nine tracks of 165.33 come out 165 or 166 wide and still end exactly at 1488.
/// </para>
/// <para>
/// <c>UniformGrid</c> is the panel this looks like, and it does something else: its source for
/// Avalonia 12.1.1 rounds the cell once and multiplies it by the index, so that same row ends at
/// 1485, and eight tracks of 173.5 round to 174 and end at 1392 — four pixels past a surface of 1388.
/// Rounding every edge instead of every width is what keeps the error from adding up. Given no
/// column count it also lays its children out as a square.
/// </para>
/// <para>
/// A row holding fewer cards than <see cref="ColumnsProperty"/> keeps the full row's cells, which is
/// what auto-fill does with the last row: its cards stay as wide as the rest instead of sharing the
/// row among fewer. The count is inherited from the grid, where the view writes it once.
/// </para>
/// </remarks>
public sealed class PosterRowPanel : Panel
{
    /// <summary>How many cells a row is divided into, inherited from the grid the row is in.</summary>
    public static readonly AttachedProperty<int> ColumnsProperty =
        AvaloniaProperty.RegisterAttached<PosterRowPanel, Control, int>("Columns", 1, inherits: true);

    static PosterRowPanel()
    {
        AffectsMeasure<PosterRowPanel>(ColumnsProperty);
    }

    public static int GetColumns(Control element) => element.GetValue(ColumnsProperty);

    public static void SetColumns(Control element, int value) => element.SetValue(ColumnsProperty, value);

    /// <summary>
    /// Where the edge before cell <paramref name="index"/> falls, snapped to a device pixel the way a
    /// browser snaps it.
    /// </summary>
    /// <remarks>
    /// Half away from zero, which is how a browser rounds a layout edge, and not the half to even of
    /// <c>LayoutHelper.RoundLayoutValue</c>: over eight tracks of 173.5 the two agree on every other
    /// edge and not on the rest, and the prototype is drawn in the browser's pixels.
    /// </remarks>
    public static double CellEdge(int index, int cells, double width, double scale) =>
        Math.Round(index * width / cells * scale, MidpointRounding.AwayFromZero) / scale;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var height = 0.0;

        if (!double.IsFinite(width))
        {
            // Nothing to share out, so each card at its own width, side by side — what the
            // StackPanel this replaced did. The grid always has a width; a panel asked without one
            // still has to answer with a size that is finite.
            var natural = 0.0;
            foreach (var child in Children)
            {
                child.Measure(availableSize);
                natural += child.DesiredSize.Width;
                height = Math.Max(height, child.DesiredSize.Height);
            }

            return new Size(natural, height);
        }

        var cells = Cells();
        var scale = LayoutHelper.GetLayoutScale(this);
        for (var i = 0; i < Children.Count; i++)
        {
            var left = CellEdge(i, cells, width, scale);
            Children[i].Measure(new Size(CellEdge(i + 1, cells, width, scale) - left, availableSize.Height));
            height = Math.Max(height, Children[i].DesiredSize.Height);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cells = Cells();
        var scale = LayoutHelper.GetLayoutScale(this);
        for (var i = 0; i < Children.Count; i++)
        {
            var left = CellEdge(i, cells, finalSize.Width, scale);
            Children[i].Arrange(new Rect(left, 0, CellEdge(i + 1, cells, finalSize.Width, scale) - left, finalSize.Height));
        }

        return finalSize;
    }

    /// <summary>Never fewer cells than cards.</summary>
    /// <remarks>
    /// The grid never hands a row more cards than its count, but the count and the rows reach the
    /// view in two notifications; a row caught between them squeezes its cards rather than drawing
    /// one past the edge. A count below one with no cards divides nothing, because nothing asks for
    /// an edge.
    /// </remarks>
    private int Cells() => Math.Max(GetValue(ColumnsProperty), Children.Count);
}
