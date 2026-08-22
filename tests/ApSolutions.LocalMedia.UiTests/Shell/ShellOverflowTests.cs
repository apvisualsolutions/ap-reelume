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

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// Nothing the shell mounts is drawn past the right edge of the window it is mounted in.
/// </summary>
/// <remarks>
/// <para>
/// This is the half <c>ViewOverflowTests</c> says out loud it cannot see: it mounts each view alone
/// in a 900 px window, so it catches a view too wide by itself and not one that is only too wide once
/// nested. Home's library block was exactly that — measured in the running application at 1600 x
/// 1000 on 2026-08-22, its border never closed on the right — and no gate said a word, through eight
/// green tranches and two screenshots.
/// </para>
/// <para>
/// It is measured against the <b>assembled</b> shell, with a data context, which is what makes it the
/// other half. A tolerance of one pixel absorbs the rounding a scaled layout does; anything wider is
/// a control somebody cannot reach.
/// </para>
/// </remarks>
public sealed class ShellOverflowTests
{
    /// <summary>
    /// The bar the window extends into and the row the shell draws there are the same 44.
    /// </summary>
    /// <remarks>
    /// Two halves of one number, in two languages: the window asks the platform for the height and
    /// the shell paints a row into it. A row shorter than the extended area leaves the system's
    /// caption hanging over the content; a taller one puts the brand below where the buttons are
    /// drawn. Neither shows up in any other gate, and both were only ever going to be found by
    /// opening the application.
    /// </remarks>
    [AvaloniaFact]
    public void The_title_bar_row_is_the_height_the_window_extends_into()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

        var shell = new ShellView();
        var window = new Window { Width = 1200, Height = 800, Content = shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var root = shell.GetVisualDescendants().OfType<Grid>().First(grid => grid.RowDefinitions.Count > 0);
        Assert.Equal(App.TitleBarHeight, root.RowDefinitions[0].Height.Value);
        Assert.True(root.RowDefinitions[0].Height.IsAbsolute);

        // And the chrome the window is given actually carries that number.
        App.ApplyDesignedChrome(window);
        Assert.True(window.ExtendClientAreaToDecorationsHint);
        Assert.Equal(App.TitleBarHeight, window.ExtendClientAreaTitleBarHeightHint);
        Assert.Throws<ArgumentNullException>(() => App.ApplyDesignedChrome(null!));

        window.Close();
    }

    [AvaloniaFact]
    public void No_surface_the_shell_mounts_is_drawn_past_the_right_edge()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

        foreach (var width in new[] { 900d, 1600d })
        {
            var shell = new ShellView();
            var window = new Window { Width = width, Height = 1000, Content = shell };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.InvalidateMeasure();
            Dispatcher.UIThread.RunJobs();

            var offside = shell.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control.IsEffectivelyVisible && control.Bounds.Width > 0)
                .Select(control => (control, right: control.TranslatePoint(
                    new Point(control.Bounds.Width, 0),
                    window)?.X ?? 0))
                .Where(pair => pair.right > width + 1)
                .Select(pair => $"{pair.control.GetType().Name} ends at {pair.right:F0}")
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                offside.Length == 0,
                $"at {width} px the shell draws past its own right edge:\n  "
                    + string.Join("\n  ", offside));

            window.Close();
        }
    }
}
