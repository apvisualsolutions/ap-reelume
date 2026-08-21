// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Settings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The three settings pages §4 calls "the same skeleton", which they were not.
/// </summary>
/// <remarks>
/// <para>
/// Measured on 2026-08-21: none of the three had a container padding, and none had the 620 the
/// document asks controls to sit inside, while the appearance section had both. Sections of one page
/// that start in different places read as several pages glued together — the sort of thing nobody
/// notices one section at a time and everybody notices scrolling past them.
/// </para>
/// <para>
/// And measuring found a defect the row did not know about. <c>ScanSettingsView</c>'s spinner carried
/// <c>ScanSettingsFallbackMinutes</c> — "Recovery interval in minutes" — as its accessible name and
/// <b>nothing painted it</b>: a screen reader heard the words and a person looking at the screen saw a
/// bare number box. The string existed. So the assertion here is general rather than about that one
/// control: <b>a value control in these pages paints the words it announces</b>.
/// </para>
/// </remarks>
public sealed class SettingsSkeletonTests
{
    /// <summary>What §4 asks of the skeleton, and what the appearance page already had.</summary>
    private const double ColumnWidth = 620;

    private const double SurfacePadding = 32;

    /// <summary>
    /// The four title at one size, and the size comes from the token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A section's heading and not a page's.</b> This first asked for level one at
    /// <c>FontSizeTitle</c>, because §4 says "title 28" and each of these reads like a page on its
    /// own. Assembled, they are not: all seven are stacked in one <c>ScrollViewer</c>, so four of them
    /// claiming level one put four top-level landmarks inside one destination, and giving them a
    /// page's geometry stepped them 158 px away from the other three. The page owns the level one now
    /// and these own a level two — see <c>SettingsPageStructureTests</c>, which measures that.
    /// </para>
    /// <para>
    /// The resolved token is compared rather than the number 20, because a test carrying its own copy
    /// would agree with itself the day the scale moved.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_four_settings_sections_title_at_the_same_size_and_it_is_the_token()
    {
        var expected = Assert.IsType<double>(Resource("FontSizeSubtitle"));
        Assert.True(expected > 0, "FontSizeSubtitle resolved to nothing, so this proves nothing.");

        foreach (var (name, view) in Pages())
        {
            var heading = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .First(block => (int)AutomationProperties.GetHeadingLevel(block) == 2);
            Assert.Equal(expected, heading.FontSize);
            Assert.False(string.IsNullOrWhiteSpace(heading.Text), $"{name} has an empty first heading.");
            Assert.DoesNotContain(
                1,
                view.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Select(block => (int)AutomationProperties.GetHeadingLevel(block)));
        }
    }

    /// <summary>
    /// Each of the three sits on a padded surface with its controls inside one column.
    /// </summary>
    [AvaloniaFact]
    public void Each_of_the_three_gains_the_surface_and_the_column_the_fourth_had()
    {
        foreach (var (name, view) in Pages())
        {
            var surface = view.GetVisualDescendants()
                .OfType<Border>()
                .FirstOrDefault(border => border.Padding.Left == SurfacePadding);
            Assert.True(surface is not null, $"{name} has no padded surface.");

            var column = view.GetVisualDescendants()
                .OfType<StackPanel>()
                .FirstOrDefault(panel => Math.Abs(panel.MaxWidth - ColumnWidth) < 0.5);
            Assert.True(column is not null, $"{name} has no {ColumnWidth}-wide column.");
        }
    }

    /// <summary>
    /// Every page says what it is for, in words, under its title.
    /// </summary>
    /// <remarks>
    /// <c>ScanSettingsView</c> had a title and two controls and nothing between them. A page of
    /// switches with no sentence saying what turning them on does is a page somebody guesses at.
    /// </remarks>
    [AvaloniaFact]
    public void Every_page_says_what_it_is_for()
    {
        foreach (var (name, view) in Pages())
        {
            var described = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(block => block.TextWrapping == Avalonia.Media.TextWrapping.Wrap
                    && (block.Text?.Length ?? 0) > 40);
            Assert.True(described, $"{name} has no description under its title.");
        }
    }

    /// <summary>
    /// A value control paints the words it announces, instead of only saying them out loud.
    /// </summary>
    /// <remarks>
    /// Sliders and spinners carry no label of their own — unlike a checkbox, whose content is its
    /// label — so a page that gives one an accessible name and no visible text has written the words
    /// and shown them to nobody. Asserted over every one of them in these pages rather than over the
    /// one that was wrong, because the next one added would be wrong the same way.
    /// </remarks>
    [AvaloniaFact]
    public void Every_spinner_and_slider_paints_the_words_it_announces()
    {
        foreach (var (name, view) in Pages())
        {
            var painted = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(block => block.Text ?? string.Empty)
                .ToArray();

            foreach (var control in view.GetVisualDescendants()
                .OfType<Control>()
                .Where(candidate => candidate is NumericUpDown or Slider))
            {
                var announced = AutomationProperties.GetName(control);
                Assert.False(
                    string.IsNullOrWhiteSpace(announced),
                    $"{name} has a {control.GetType().Name} that announces nothing.");
                Assert.Contains(announced!, painted, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>
    /// The three, plus the page that was already right, each in a window of its own.
    /// </summary>
    /// <remarks>
    /// No data context anywhere: a binding that resolves to nothing leaves <c>IsVisible</c> at its
    /// default, which puts every branch of every page on screen at once. That is the widest each of
    /// them ever is, and the case worth measuring.
    /// </remarks>
    private static IEnumerable<(string Name, Control View)> Pages()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        foreach (var view in new Control[]
        {
            new ScanSettingsView(),
            new RecommendationSettingsView(),
            new SegmentDetectionSettingsView(),
            new AppearanceSettingsView(),
        })
        {
            var window = new Window { Width = 900, Height = 900, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            yield return (view.GetType().Name, view);
            window.Close();
        }
    }

    private static object Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        Assert.NotNull(value);
        return value!;
    }
}
