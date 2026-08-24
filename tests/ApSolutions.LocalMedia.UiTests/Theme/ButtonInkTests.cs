// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Where a button's label sits inside the button, which is not where anybody assumed.
/// </summary>
/// <remarks>
/// <para>
/// The owner saw it before any gate did: the words inside the pills are not vertically centred. They
/// are not, and the cause is a default nobody wrote. <c>ContentControl.VerticalContentAlignment</c>
/// starts at <c>Stretch</c>, this repository's own <c>Button</c> style sets a height, a radius and a
/// padding and never touches the alignment, and a stretched <c>TextBlock</c> fills its whole box and
/// draws its line at the <b>top</b> of it. Measured on 2026-08-24 in a 36 px button: the label's box
/// came out 34 px tall — the button minus its border — with the text riding at the top of it, so the
/// ink sat about seven pixels above the centre of every pill in the application.
/// </para>
/// <para>
/// The prototype's own rule is one line of CSS on every button it draws:
/// <c>display:inline-flex; align-items:center; justify-content:center</c>. This is that line, asserted.
/// </para>
/// <para>
/// It measures the <b>box</b> and not the glyphs: a font's ascent and descent are not symmetric, so
/// pixel-perfect optical centring is a different question from whether the control centres its
/// content at all. What went wrong here is the second, and the second is what a gate can hold.
/// </para>
/// </remarks>
public sealed class ButtonInkTests
{
    public static TheoryData<string> Pills() => ["", "primary-action", "player-chrome"];

    [AvaloniaTheory]
    [MemberData(nameof(Pills))]
    public void A_buttons_label_is_centred_in_it(string className)
    {
        var button = new Button { Content = "Favorite", Height = 44 };
        if (className.Length > 0)
        {
            button.Classes.Add(className);
        }

        var window = new Window { Width = 320, Height = 160, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var label = button.GetVisualDescendants().OfType<TextBlock>().Single();
            var offset = ((Visual)label).TranslatePoint(default, button)!.Value;
            var above = offset.Y;
            var below = button.Bounds.Height - offset.Y - label.Bounds.Height;

            // One pixel, because a pixel cannot be split: a 19 px line in a 44 px button leaves 25 px
            // to share, and 25 is odd — measured, 13 above and 12 below. A tolerance of half a pixel
            // would be a gate that fails on arithmetic rather than on the thing it watches.
            Assert.True(
                Math.Abs(above - below) <= 1.0,
                $"'{className}': the label sits {above:F2} px below the top and {below:F2} px above the "
                    + "bottom of its button, so it is not centred in it.");

            // And the gaps have to be real. A stretched label is centred too — trivially, because its
            // box IS the button — so equal gaps of one pixel each would pass a check that only
            // compared them, and pass it while the text rides at the top of that box. One line of
            // body type in a 44 px button leaves about twelve pixels on each side.
            Assert.True(
                above >= 4 && below >= 4,
                $"'{className}': the label's box leaves {above:F2} px above and {below:F2} px below in a "
                    + $"{button.Bounds.Height:F0} px button, which is a box the size of the button rather "
                    + "than the size of a line of text.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Pills))]
    public void A_buttons_label_is_the_size_of_its_line_and_not_of_the_button(string className)
    {
        var button = new Button { Content = "Favorite", Height = 44 };
        if (className.Length > 0)
        {
            button.Classes.Add(className);
        }

        var window = new Window { Width = 320, Height = 160, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var label = button.GetVisualDescendants().OfType<TextBlock>().Single();

            // A stretched label is the defect itself: its box grows to the button and the text draws
            // at the top of that box. One line of 14 pt type is nowhere near 42 px tall.
            Assert.True(
                label.Bounds.Height < button.Bounds.Height - 8,
                $"'{className}': the label's box is {label.Bounds.Height:F2} px tall inside a "
                    + $"{button.Bounds.Height:F2} px button, so it is being stretched rather than placed.");
        }
        finally
        {
            window.Close();
        }
    }
}
