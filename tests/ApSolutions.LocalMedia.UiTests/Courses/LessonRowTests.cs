// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Globalization;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.Presentation.Courses;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Courses;

/// <summary>
/// A course's lesson row draws the numbers the prototype draws for it, and draws them on one line.
/// </summary>
/// <remarks>
/// The second view measured against the prototype, written the way the first was: the tree draws what the table
/// says, and the table says what the design draws.
/// <para>
/// <b>What measuring first said, on 2026-09-03.</b> The design builds this row out of the same
/// <c>rowBox</c> the settings rows are built from — same padding, same corner, same hairline — plus
/// four things of its own. The tree had written its own box instead, at the medium token rather than
/// at 10, and had stacked the two buttons <b>under</b> the row rather than at the right end of it,
/// which made every lesson roughly twice as tall as the design's.
/// </para>
/// <para>
/// <b>And the shared box had to be renamed before it could be worn.</b> It was called
/// <c>setting-row</c>, which was true of all thirty-one of its sites and would have stopped being
/// true the moment this row took it — so a second caller would have written a duplicate of the same
/// numbers instead. That is <c>card-eyebrow</c>'s defect exactly, one file over.
/// </para>
/// </remarks>
public sealed partial class LessonRowTests
{

    /// <summary>The row the view builds draws them, measured on the controls it builds.</summary>
    /// <remarks>
    /// Built rather than read off the token file, for the reason the thread card's twin gives: a
    /// class nobody wears draws nothing and passes every reading of the file it lives in.
    /// </remarks>
    [AvaloniaFact]
    public void The_row_the_view_builds_draws_them()
    {
        var view = new LessonRowView();
        var window = new Window { Width = 900, Height = 300, Content = view };

        double corner, padDown, padAcross, glyphWidth, glyphSize, numberWidth, chipSize, barWidth, barHeight;
        int numberWeight, chipWeight;
        double chipPadAcross, chipPadDown;
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var box = Wearing<Border>(view, "row-box");
            var glyph = Wearing<TextBlock>(view, "lesson-glyph");
            var number = Wearing<TextBlock>(view, "lesson-number");
            var chip = Wearing<Border>(view, "chip-accent");
            var chipText = chip.GetVisualDescendants().OfType<TextBlock>().First();
            var bar = Wearing<ProgressBar>(view, "lesson-bar");

            corner = box.CornerRadius.TopLeft;
            padAcross = box.Padding.Left;
            padDown = box.Padding.Top;
            glyphWidth = glyph.Width;
            glyphSize = glyph.FontSize;
            numberWidth = number.Width;
            numberWeight = (int)number.FontWeight;
            chipPadAcross = chip.Padding.Left;
            chipPadDown = chip.Padding.Top;
            chipSize = chipText.FontSize;
            chipWeight = (int)chipText.FontWeight;
            barWidth = bar.MaxWidth;
            barHeight = bar.Height;
        }
        finally
        {
            // Closed in a finally, always: a failed assertion between Show and Close leaves the
            // window open and breaks the harness's per-test isolation, which surfaces as a cleanup
            // failure naming some unrelated test that never ran.
            window.Close();
        }

        // The shared box, which is the whole reason this row stopped writing a corner of its own.
        Assert.Equal(10, corner);
        Assert.Equal(16, padAcross);
        Assert.Equal(13, padDown);

        Assert.Equal(16, glyphWidth);
        Assert.Equal(12, glyphSize);
        Assert.Equal(34, numberWidth);
        Assert.Equal(700, numberWeight);
        Assert.Equal(10, chipPadAcross);
        Assert.Equal(3, chipPadDown);
        Assert.Equal(11.5, chipSize);
        Assert.Equal(600, chipWeight);
        Assert.Equal(220, barWidth);
        Assert.Equal(3, barHeight);
    }

    /// <summary>
    /// The row is one line: the two buttons sit beside the lesson rather than under it.
    /// </summary>
    /// <remarks>
    /// <b>Asserted as height rather than as structure.</b> A test that checked the buttons were in a
    /// fourth grid column would pass over any layout that happened to declare one, and what went
    /// wrong here was not a column — it was that a list of twenty lessons was twice as tall as the
    /// design's. So the row is measured against the height it could not be if the buttons were
    /// stacked: the design's own box is 13 above and below a 30 px button, which is 56, and anything
    /// approaching twice that is the shape this replaced.
    /// </remarks>
    [AvaloniaFact]
    public void The_row_is_one_line()
    {
        var view = new LessonRowView();

        // Inside a stack rather than as the window's own content: a control handed straight to a
        // Window stretches to it, so the height read back would be the window's and every ceiling
        // written against it would be measuring the scene instead of the row.
        var stack = new StackPanel { Children = { view } };
        var window = new Window { Width = 900, Height = 300, Content = stack };

        double height;
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            height = Wearing<Border>(view, "row-box").Bounds.Height;
        }
        finally
        {
            window.Close();
        }

        // Anti-blindness floor: a row that laid out to nothing would satisfy any ceiling.
        Assert.True(height > 0, "the row laid out to no height at all, so this measured nothing.");

        Assert.True(
            height < 90,
            $"the row is {height} px tall. The design draws one line — a 30 px button inside 13 px of "
            + "padding either side — so this is the stacked shape it replaced coming back.");
    }

    private static T Wearing<T>(Visual root, string styleClass)
        where T : Control
    {
        var found = root.GetVisualDescendants()
            .OfType<T>()
            .Where(control => control.Classes.Contains(styleClass))
            .ToArray();

        Assert.True(
            found.Length > 0,
            $"no {typeof(T).Name} in this view wears «{styleClass}», so this measured nothing.");
        return found[0];
    }
}
