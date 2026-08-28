// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// A view taller than the window it lives in has to be reachable by scrolling.
/// </summary>
/// <remarks>
/// <para>
/// The width axis of «secciones cortadas» was ruled out on 2026-08-28 and measured: none of the 48
/// views exceeds the 836 px the shell gives them. This is the third of the four hypotheses that
/// survived it — that "cut off" is <b>vertical</b> — and it is the one the numbers already pointed
/// at: Settings measures 1,797 px tall against a window whose minimum height is 600.
/// </para>
/// <para>
/// What is asserted is not that a view is short. It is that a tall one is inside something that
/// scrolls: the shell puts its destinations in <c>ScrollViewer</c>s, and a view mounted anywhere
/// else — an overlay, a dialog, a panel — with more content than room is content nobody can reach.
/// That is what "cut off" means when it is not about the width.
/// </para>
/// <para>
/// <b>Same two limitations as the width gates, and for the same reason.</b> No data context, so
/// every branch is on screen at once and every <c>ItemsControl</c> is empty: this measures the tall
/// end of each view's own layout, not a list that grows. Silence here is not a certificate either.
/// </para>
/// </remarks>
public sealed class ViewHeightTests
{
    /// <summary>The shortest the main window can be made, from <c>App.axaml.cs</c>.</summary>
    private const double MinimumWindowHeight = 600;

    private const double MinimumWindowWidth = 900;

    [AvaloniaTheory]
    [InlineData("es-ES")]
    [InlineData("en-US")]
    public void Every_view_taller_than_the_window_is_somewhere_that_scrolls(string language)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(language));

        var views = typeof(ShellView).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true }
                && typeof(UserControl).IsAssignableFrom(type)
                && type.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        // The same anti-blindness floor the width gates carry: a reflection query that found nothing
        // would pass this by measuring nothing at all.
        Assert.True(
            views.Length >= 40,
            $"only {views.Length} views were found, so this gate is reading the wrong thing.");

        var tall = new List<string>();
        var measured = 0;

        foreach (var type in views)
        {
            // Inside a scroller, which is both how the shell mounts them and the only way to read a
            // view's own height. Two earlier drafts read nothing: as a window's direct content the
            // view is stretched to the window, so every height was exactly 600; and calling Measure
            // by hand on an already-arranged tree does not recompute DesiredSize. The floor below is
            // what said so, twice.
            var view = (UserControl)Activator.CreateInstance(type)!;
            var window = new Window
            {
                Width = MinimumWindowWidth,
                Height = MinimumWindowHeight,
                Content = new ScrollViewer { Content = view },
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            if (view.Bounds.Height > MinimumWindowHeight + 0.5)
            {
                measured++;
                tall.Add($"{type.Name}: {view.Bounds.Height:F0} px");
            }

            window.Close();
            Dispatcher.UIThread.RunJobs();
        }

        // Every one of them is mounted by the shell inside a ScrollViewer, and that is the assertion:
        // the list of tall views is allowed to grow, the list of tall views the shell does not scroll
        // is not.
        // ShellView is the one view this cannot ask about, and excluding it is not a loophole: it is
        // what holds the scrollers rather than something held by one, and its 8,775 px is every
        // destination's markup at once with no data context to hide any of it.
        var unreachable = tall
            .Select(entry => entry.Split(':')[0])
            .Where(name => name != nameof(ShellView) && !IsScrolledByTheShell(name))
            .ToArray();

        Assert.True(
            unreachable.Length == 0,
            $"{unreachable.Length} view(s) are taller than the shortest window the application allows "
                + "and the shell does not put them anywhere that scrolls, so their bottom is content "
                + $"nobody can reach:\n  {string.Join("\n  ", unreachable)}\n"
                + $"(the tall ones, for the record: {string.Join(", ", tall)})");

        // And the floor that keeps this from going blind the other way: if nothing measured tall,
        // either the layouts stopped running or the window stopped being 600.
        Assert.True(
            measured >= 1,
            "no view measured taller than the window, which after Settings reached 1,797 px means "
                + "the trees are not being laid out rather than the application having got shorter.");
    }

    /// <summary>
    /// Whether the shell mounts this view inside something that scrolls.
    /// </summary>
    /// <remarks>
    /// Read off the shell's own tree rather than from a list written here: a view moved out of a
    /// scroller tomorrow is caught by this gate the day it moves, and a list would have to be
    /// remembered. What is looked for is the view's type appearing under a <c>ScrollViewer</c> in the
    /// markup the shell declares — mounted views only, which is what "the shell puts it" means.
    /// </remarks>
    private static bool IsScrolledByTheShell(string viewName)
    {
        var shell = new ShellView();
        var window = new Window { Width = MinimumWindowWidth, Height = MinimumWindowHeight, Content = shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var found = shell.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .SelectMany(scroller => scroller.GetVisualDescendants())
            .Any(descendant => descendant.GetType().Name == viewName);

        window.Close();
        Dispatcher.UIThread.RunJobs();
        return found;
    }
}
