// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
}
