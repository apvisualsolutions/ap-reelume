// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// No view is wider than the narrowest window the application allows.
/// </summary>
/// <remarks>
/// <para>
/// A control drawn off the side of the window is a control nobody can press, and it has happened
/// <b>seven times</b> in this repository — always the same shape, a horizontal row of buttons carrying
/// translated words. Each time it was found by the walk, one control at a time, and the failure it
/// produced named the click rather than the layout. This asks the whole tree at once, before any of
/// them gets there.
/// </para>
/// <para>
/// Every view is mounted <b>without a data context</b>, which leaves every <c>IsVisible</c> binding
/// at its default: every branch of every view is on screen simultaneously, which is wider than the
/// application can ever be. That makes this an upper bound rather than a scene — if it fits here, it
/// fits.
/// </para>
/// <para>
/// It is measured against 900, which is <c>MinWidth</c> on the main window in <c>App.axaml.cs</c>.
/// </para>
/// <para>
/// <b>Two limitations, stated rather than hidden, because a gate that promises more than it measures
/// is worse than no gate.</b> First, a view mounted alone gets the whole 900, while inside the shell
/// it gets 900 minus whatever the shell's own chrome takes: this catches a view too wide on its own
/// and cannot catch one only too wide once nested. Second, no data context means every
/// <c>ItemsControl</c> is empty — 17 views in the tree hold one — so what is measured is each view's
/// own layout with its lists empty, not a row that turns out too wide once filled. Both halves are
/// the walk's, from the other side and with the mouse. <b>Silence here is not a certificate.</b>
/// </para>
/// <para>
/// The second limitation is narrower than it sounds, and that is measured too: the rows and cards
/// those lists hold — <c>LibraryEntryView</c>, <c>EpisodeRowView</c>, <c>CandidateCardView</c> — are
/// views in their own right and are mounted and measured here on their own.
/// </para>
/// </remarks>
public sealed class ViewOverflowTests
{
    /// <summary>The narrowest the main window can be made, from <c>App.axaml.cs</c>.</summary>
    private const double MinimumWindowWidth = 900;

    /// <summary>
    /// Views that cannot be constructed without a data context, with the reason. Named rather than
    /// skipped silently: a view that quietly stopped being measured is a view nobody is watching.
    /// </summary>
    private static readonly Dictionary<string, string> NotMountedAlone = new(StringComparer.Ordinal);

    [AvaloniaFact]
    public void No_view_is_wider_than_the_narrowest_window_the_application_allows()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var views = typeof(ShellView).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true }
                && typeof(UserControl).IsAssignableFrom(type)
                && type.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        // Anti-blindness floor: a reflection query that found nothing would pass by measuring nothing.
        Assert.True(
            views.Length >= 40,
            $"only {views.Length} views were found in the presentation assembly, so this gate is "
                + "reading the wrong thing rather than finding a small application.");

        var offside = new List<string>();
        var unmountable = new List<string>();
        var measured = 0;

        foreach (var type in views)
        {
            if (NotMountedAlone.ContainsKey(type.Name))
            {
                continue;
            }

            UserControl view;
            try
            {
                view = (UserControl)Activator.CreateInstance(type)!;
            }
            catch (Exception failure)
            {
                unmountable.Add($"{type.Name}: {failure.GetType().Name}");
                continue;
            }

            var window = new Window
            {
                Width = MinimumWindowWidth,
                Height = 700,
                Content = view,
            };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            foreach (var control in view.GetVisualDescendants().OfType<Control>())
            {
                if (control.Bounds.Width <= 0 || !control.IsEffectivelyVisible)
                {
                    continue;
                }

                measured++;
                var right = control.TranslatePoint(new Point(control.Bounds.Width, 0), window);
                if (right is not { } edge)
                {
                    continue;
                }

                if (edge.X > MinimumWindowWidth + 0.5 || edge.X < -0.5)
                {
                    offside.Add(
                        $"{type.Name}: {Describe(control)} ends at x={edge.X:F0} in a "
                            + $"{MinimumWindowWidth:F0}-wide window");
                    break;
                }
            }

            window.Close();
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(
            unmountable.Count == 0,
            "these views could not be constructed on their own, so nothing measured them:\n  "
                + string.Join("\n  ", unmountable));

        // The second anti-blindness floor, and the one that matters: finding the views proves
        // nothing if their trees measure to nothing. A layout that never ran leaves every Bounds at
        // zero, every control skipped by the guard above, and this gate green over an empty count.
        // 3543 laid-out controls across 48 views on 2026-08-20; the floor keeps a margin so a view
        // getting simpler is not a red, while a layout that stopped running is.
        Assert.True(
            measured >= 3000,
            $"only {measured} laid-out controls were measured across {views.Length} views, so the "
                + "trees are not being laid out rather than the application being small.");

        Assert.True(
            offside.Count == 0,
            $"{offside.Count} view(s) draw a control outside the narrowest window the application "
                + $"allows:\n  {string.Join("\n  ", offside)}");
    }

    private static string Describe(Control control) =>
        control.Name ?? control.GetType().Name;
}
