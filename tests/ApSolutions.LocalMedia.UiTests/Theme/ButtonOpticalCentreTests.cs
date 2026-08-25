// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Where the <b>ink</b> sits inside a button, which is not the same question as where its box sits.
/// </summary>
/// <remarks>
/// <para>
/// <c>ButtonInkTests</c> measures the box and says so: a font's ascent and descent are not symmetric,
/// so a label box centred to the pixel can still draw its letters low. The owner saw exactly that
/// after the box was centred — "sigues teniendo problemas con alineado vertical del texto de los
/// botones en general" — and this is the half that gate deliberately left out.
/// </para>
/// <para>
/// What is measured is the run of ink: from the top of a capital to the bottom of a descender. Its
/// middle has to be the button's middle, because that is what an eye compares against the two edges.
/// Nothing here renders anything — the numbers come from the font's own metrics, so the answer does
/// not depend on a screenshot or on which machine ran it.
/// </para>
/// </remarks>
public sealed class ButtonOpticalCentreTests
{
    [AvaloniaFact]
    public void The_ink_of_a_label_is_centred_in_its_button_and_not_only_its_box()
    {
        // A word with both a capital and a descender, which is what makes the run of ink the full
        // height a person compares: «Reproducir» has neither, «Guardar el informe» has both.
        var button = new Button { Content = "Guardar el informe", Height = 44 };
        var window = new Window { Width = 400, Height = 200, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var label = button.GetVisualDescendants().OfType<TextBlock>().Single();
            var box = ((Visual)label).TranslatePoint(default, button)!.Value.Y;
            var metrics = new Typeface(label.FontFamily, label.FontStyle, label.FontWeight)
                .GlyphTypeface;
            var scale = label.FontSize / metrics.Metrics.DesignEmHeight;

            // Ascent is negative in this font's units and descent positive, which is the convention
            // the design em uses: both are turned into distances from the baseline here.
            var ascent = -metrics.Metrics.Ascent * scale;
            var descent = metrics.Metrics.Descent * scale;
            var capital = metrics.Metrics.IsFixedPitch
                ? ascent
                : ascent * 0.72;

            var baseline = box + ascent;
            var inkTop = baseline - capital;
            var inkBottom = baseline + descent;
            var inkCentre = (inkTop + inkBottom) / 2;
            var buttonCentre = button.Bounds.Height / 2;

            Assert.True(
                Math.Abs(inkCentre - buttonCentre) <= 1.0,
                $"the run of ink is centred at {inkCentre:F2} in a {button.Bounds.Height:F0} px "
                    + $"button whose middle is {buttonCentre:F2}, so the label sits "
                    + $"{inkCentre - buttonCentre:F2} px {(inkCentre > buttonCentre ? "low" : "high")}. "
                    + $"The box starts at {box:F2}, the baseline is at {baseline:F2}, ascent "
                    + $"{ascent:F2}, descent {descent:F2}.");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// And the glyph beside the word, which is the half the button's own padding could never fix.
    /// </summary>
    /// <remarks>
    /// <para>
    /// «Los botones de toda la app siguen sin alinearse perfectamente en vertical», reported on
    /// 2026-08-25 after the compensation was already in. It was in the wrong place: five pixels of
    /// bottom <b>padding on the button</b> move the whole content, so a row of a glyph and a word
    /// travels together and the two stay exactly as far apart as they were. What it changed was
    /// where the row sits; what it never touched was how its two pieces relate.
    /// </para>
    /// <para>
    /// A glyph is centred by its geometry and has no baseline; a word draws its ink 2.43 px below
    /// the middle of its own box. So the compensation belongs on the label alone, and this is the
    /// measurement that says so: the glyph's middle, the word's ink, and the button's middle, all
    /// three within a pixel of each other. Nothing renders — the numbers come from the layout and
    /// from the font's own metrics.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_glyph_beside_a_word_sits_on_the_same_middle_as_its_ink()
    {
        var glyph = new Avalonia.Controls.Shapes.Path
        {
            Classes = { "icon", "size-16" },
            Data = StreamGeometry.Parse("M5.5 12.4 10 16.9 18.6 7.6"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var label = new TextBlock
        {
            Text = "Guardar el informe",
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(glyph);
        row.Children.Add(label);

        var button = new Button { Content = row, Height = 44 };
        var window = new Window { Width = 400, Height = 200, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var glyphTop = ((Visual)glyph).TranslatePoint(default, button)!.Value.Y;
            var glyphCentre = glyphTop + (glyph.Bounds.Height / 2);
            var inkCentre = InkCentre(label, button);
            var buttonCentre = button.Bounds.Height / 2;

            Assert.True(
                Math.Abs(inkCentre - glyphCentre) <= 1.0,
                $"the glyph's middle is at {glyphCentre:F2} and the word's ink at {inkCentre:F2}, "
                    + $"so they are {inkCentre - glyphCentre:F2} px apart inside one button.");
            Assert.True(
                Math.Abs(glyphCentre - buttonCentre) <= 1.0,
                $"the glyph's middle is at {glyphCentre:F2} in a {button.Bounds.Height:F0} px button "
                    + $"whose middle is {buttonCentre:F2}, so the shape sits "
                    + $"{glyphCentre - buttonCentre:F2} px {(glyphCentre > buttonCentre ? "low" : "high")}.");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// And the same for a drop-down, which carries the same words on the same baseline.
    /// </summary>
    /// <remarks>
    /// «Los desplegables tienen el mismo problema de alineación vertical que tenían los botones»,
    /// reported on 2026-08-25 after the buttons were fixed. It is the same font and the same
    /// asymmetry, so it is the same five pixels — measured here rather than trusted, because the
    /// drop-down centres its label inside a template of its own rather than through a padding.
    /// </remarks>
    [AvaloniaFact]
    public void The_ink_of_a_drop_downs_label_is_centred_in_it_too()
    {
        var picker = new ComboBox
        {
            Classes = { "filter-pill" },
            Height = 44,
            Width = 260,
            ItemsSource = new[] { "Guardar el informe" },
            SelectedIndex = 0,
        };
        var window = new Window { Width = 400, Height = 200, Content = picker };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var label = picker.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(block => block.Text == "Guardar el informe");
            var inkCentre = InkCentre(label, picker);
            var pickerCentre = picker.Bounds.Height / 2;

            Assert.True(
                Math.Abs(inkCentre - pickerCentre) <= 1.0,
                $"the run of ink is centred at {inkCentre:F2} in a {picker.Bounds.Height:F0} px "
                    + $"drop-down whose middle is {pickerCentre:F2}, so the label sits "
                    + $"{inkCentre - pickerCentre:F2} px {(inkCentre > pickerCentre ? "low" : "high")}.");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The middle of a run of ink — the top of a capital to the foot of a descender — in the
    /// coordinates of the control around it.
    /// </summary>
    /// <remarks>
    /// It comes from the font's metrics rather than from a rendered picture, so the answer does not
    /// depend on which machine ran it. The cap height is taken as 72 % of the ascent for a
    /// proportional face, which is the ratio the two Segoe UI Variable faces this application ships
    /// with actually draw.
    /// </remarks>
    private static double InkCentre(TextBlock label, Visual within)
    {
        var box = ((Visual)label).TranslatePoint(default, within)!.Value.Y;
        var metrics = new Typeface(label.FontFamily, label.FontStyle, label.FontWeight).GlyphTypeface;
        var scale = label.FontSize / metrics.Metrics.DesignEmHeight;
        var ascent = -metrics.Metrics.Ascent * scale;
        var descent = metrics.Metrics.Descent * scale;
        var capital = metrics.Metrics.IsFixedPitch ? ascent : ascent * 0.72;
        var baseline = box + ascent;
        return ((baseline - capital) + (baseline + descent)) / 2;
    }
}
