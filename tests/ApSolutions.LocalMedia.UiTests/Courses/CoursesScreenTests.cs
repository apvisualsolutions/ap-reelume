// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Globalization;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Courses;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Courses;

/// <summary>
/// The courses grid draws the numbers the prototype draws for it, and puts the offer where the
/// prototype puts it.
/// </summary>
/// <remarks>
/// The third view measured against the prototype and the first whose shape was a decision rather than a
/// measurement: the prototype keeps «Marcar una carpeta como curso…» inside the empty box while
/// there is nothing, and drops it to the foot in the plain button once there is something. The tree
/// had it at the head and accented in both states, with a reason written against exactly that. The
/// prototype was chosen on 2026-09-03, so the reason is gone from the markup rather than left
/// there contradicting what the file does.
/// <para>
/// <b>One thing the design draws is deliberately absent, and it is not an oversight.</b> Each card
/// there opens with a 16:9 picture — the prototype's generated gradient, which stands in for artwork
/// it cannot ship. A course is detected from a folder and never looked up, so that panel would be a
/// placeholder for ever unless the picture came from the video itself.
/// <b>Whether it can is now measured and the answer is yes</b> — a frame in 137 ms and about
/// 460 ms per file with a seek — but whether this application should open files it was never asked
/// to play is a product decision and is still open. Nothing here
/// asserts the panel's absence, because an assertion would make that look settled.
/// </para>
/// </remarks>
public sealed partial class CoursesScreenTests
{

    /// <summary>The screen the view builds draws them.</summary>
    [AvaloniaFact]
    public void The_screen_the_view_builds_draws_them()
    {
        var view = new CoursesView();
        var window = new Window { Width = 1200, Height = 900, Content = view };

        double page, grid, emptyTitle;
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            page = Wearing<StackPanel>(view, "courses-page").Spacing;
            grid = Wearing<WrapPanel>(view, "course-grid").ItemSpacing;
            emptyTitle = Wearing<TextBlock>(view, "empty-title").FontSize;
        }
        finally
        {
            // Closed in a finally, always: a failure between Show and Close leaves the window open
            // and the harness reports it against an unrelated test that never ran.
            window.Close();
        }

        Assert.Equal(18, page);
        Assert.Equal(14, grid);
        Assert.Equal(16, emptyTitle);

        // The card lives in a DataTemplate, so nothing builds it without a course to build it from —
        // and a measurement over an empty grid would report whatever a default Border draws. What is
        // asserted instead is that the template ASKS for the class; that the class draws 12, and that
        // 12 is the design's, is SurfaceCornerTests' half.
        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Courses/CoursesView.axaml"));
        Assert.Contains("Classes=\"course-card\"", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The empty box's edge is dashed, which is what the design draws for a place something is going
    /// to go.
    /// </summary>
    /// <remarks>
    /// A <c>Border</c> in Avalonia has no dash of its own, so the edge is a <c>Rectangle</c> behind
    /// the content. That is a workaround, and a workaround nobody asserts is a workaround somebody
    /// quietly replaces with a plain border that looks nearly right.
    /// </remarks>
    [AvaloniaFact]
    public void The_empty_state_is_dashed()
    {
        var view = new CoursesView();
        var window = new Window { Width = 1200, Height = 900, Content = view };

        int dashes;
        double corner;
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var edge = view.GetVisualDescendants()
                .OfType<Rectangle>()
                .Where(shape => shape.StrokeDashArray is { Count: > 0 })
                .ToArray();

            dashes = edge.Length;
            corner = edge.Length > 0 ? edge[0].RadiusX : -1;
        }
        finally
        {
            window.Close();
        }

        Assert.True(dashes > 0, "nothing on this screen dashes its edge, so the empty state is a plain box again.");
        Assert.Equal(8, corner);
    }

    /// <summary>
    /// The offer sits inside the empty state and again at the foot, and only the first one is
    /// accented.
    /// </summary>
    /// <remarks>
    /// <b>Two buttons and not one moved.</b> The prototype shows the offer in both states — inside
    /// the box while there is nothing, plain at the foot once there is — so a test that only checked
    /// it had left the header would pass over a screen that had lost it in one of the two states.
    /// <para>
    /// The accent is asserted as exactly one because <c>LeadingActionTests</c> holds the whole tree
    /// to that, and this is the view where it would be easiest to end up with two: the design accents
    /// each card's own button as well, and a grid where every card shouts has no leading action at
    /// all.
    /// </para>
    /// <para>
    /// <b>And the autonomous walk cannot see this, which is measured rather than assumed.</b> Both
    /// buttons carry the same accessible name — they are one offer in two places — so the walk's
    /// inventory folds them into a single identity: 247 declarations in 241 identities after this
    /// change, against 246 in 241 before it. Pressing either one marks both covered, so losing one of
    /// the two would leave that gate exactly as green as it is now.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_offer_is_in_both_states_and_accented_in_one()
    {
        var application = Avalonia.Application.Current!;
        var view = new CoursesView();
        var window = new Window { Width = 1200, Height = 900, Content = view };

        int offers, accented;
        try
        {
            // The language is applied first, and that is not ceremony: the strings live in language
            // dictionaries that nothing has merged until this runs, so both buttons would resolve
            // their name to nothing and the census below would agree with itself over two blanks.
            App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var label = Resource("CoursesMarkFolderAction");
            Assert.False(string.IsNullOrWhiteSpace(label), "CoursesMarkFolderAction did not resolve.");

            var buttons = view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => string.Equals(
                    Avalonia.Automation.AutomationProperties.GetName(button),
                    label,
                    StringComparison.Ordinal))
                .ToArray();

            offers = buttons.Length;
            accented = buttons.Count(button => button.Classes.Contains("primary-action"));
        }
        finally
        {
            window.Close();
            App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));
        }

        Assert.Equal(2, offers);
        Assert.Equal(1, accented);
    }

    private static string Resource(string key) =>
        Avalonia.Application.Current is { } application
            // Null variant, and that is not the oversight it looks like: the brushes live in theme
            // dictionaries and need the running variant, but the strings live in language
            // dictionaries and resolve under none — asking for a variant returns nothing at all.
            && application.TryGetResource(key, null, out var value)
            ? value as string ?? string.Empty
            : string.Empty;

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
