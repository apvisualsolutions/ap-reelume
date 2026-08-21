// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// Settings is one scrolling page with seven sections on it, and this is about the page rather than
/// about any of them.
/// </summary>
/// <remarks>
/// <para>
/// Measured on the assembled shell on 2026-08-21, which is the half a per-view table cannot see:
/// <b>four sections claimed heading level 1 and three claimed level 2</b>, on the same page, so a
/// reader jumping by heading found four top-level landmarks inside one destination. And the four sat
/// at <b>x=454</b> against the other three at <b>x=296</b> — a 158 px step down the middle of a page
/// whose sections are peers.
/// </para>
/// <para>
/// §4 describes that geometry as "the same skeleton" and names three views for it. Assembled, the
/// reading is different: <b>the skeleton belongs to every section, and the level-1 heading belongs to
/// the page</b>, which had none at all. The destination is already called "Settings" in the navigation
/// rail, so the page's own heading is that same string rather than a new one.
/// </para>
/// </remarks>
public sealed class SettingsPageStructureTests
{
    /// <summary>
    /// One heading owns the page and every section sits under it.
    /// </summary>
    /// <remarks>
    /// The count is asserted as exactly one rather than at least one: "at least one" is what four of
    /// them satisfied while being wrong.
    /// </remarks>
    [AvaloniaFact]
    public void The_settings_page_has_one_level_one_heading_and_the_sections_sit_under_it()
    {
        var (window, shell) = Show();

        var headings = SettingsHeadings(shell);
        Assert.NotEmpty(headings);

        var top = headings.Where(entry => entry.Level == 1).ToArray();
        Assert.Single(top);
        Assert.Equal(Resource("NavigationSettings"), top[0].Text);

        // Every section title is a level two, and nothing inside a section claims level one.
        var sections = headings.Where(entry => entry.Owner is not null).ToArray();
        Assert.NotEmpty(sections);
        Assert.DoesNotContain(1, sections.Select(entry => entry.Level));

        window.Close();
    }

    /// <summary>
    /// Every section starts at the same place, because they are peers on one page.
    /// </summary>
    /// <remarks>
    /// The titles are compared and not the panels, because a title is where the eye lands. A page
    /// whose sections step sideways reads as several pages glued together, which is exactly what four
    /// of them did after being given a page's geometry.
    /// </remarks>
    [AvaloniaFact]
    public void Every_section_of_the_page_starts_at_the_same_place()
    {
        var (window, shell) = Show();

        // Level two and not every heading: a section's start is its own title, and a heading nested
        // inside one - the diagnostics preview sits on a surface of its own - is indented on purpose.
        var lefts = SettingsHeadings(shell)
            .Where(entry => entry.Owner is not null && entry.Level == 2)
            .Select(entry => Math.Round(entry.Left, 0))
            .Distinct()
            .ToArray();

        Assert.Single(lefts);
        window.Close();
    }

    private static (int Level, string Text, double Left, string? Owner)[] SettingsHeadings(ShellView shell) =>
        [.. shell.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => (
                Level: (int)AutomationProperties.GetHeadingLevel(block),
                Text: block.Text ?? string.Empty,
                Left: block.TranslatePoint(new Point(0, 0), shell)?.X ?? double.NaN,
                Owner: block.GetVisualAncestors()
                    .OfType<UserControl>()
                    .FirstOrDefault(owner => owner.GetType().Name.EndsWith("SettingsView", StringComparison.Ordinal))
                    ?.GetType().Name))
            .Where(entry => entry.Level > 0)
            .Where(entry => entry.Owner is not null || entry.Text == Resource("NavigationSettings"))];

    /// <summary>
    /// The shell without a view model, which is what puts the settings pane and all seven on screen.
    /// </summary>
    private static (Window Window, ShellView Shell) Show()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var shell = new ShellView();
        var window = new Window { Width = 1280, Height = 900, Content = shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, shell);
    }

    private static string Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        return Assert.IsType<string>(value);
    }
}
