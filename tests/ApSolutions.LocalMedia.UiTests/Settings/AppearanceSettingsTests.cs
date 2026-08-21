// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The appearance page: three theme pills and not five, rows that wrap, and a notice that says
/// whether reduced motion is on rather than that the application respects it.
/// </summary>
/// <remarks>
/// <para>
/// <b>§4 asks for five theme buttons and the answer is three</b>, which is not a measurement against
/// the document but a decision the tree had already taken and written down. <c>ThemePreference.cs</c>
/// says it above the two high-contrast variants: they are a state read from Windows, not a fourth and
/// fifth choice somebody picks. An application offering its own high-contrast picker either ignores
/// the system setting or duplicates it. This asserts the three so the decision cannot drift back.
/// </para>
/// <para>
/// <b>The rows still wrap, for the other reason.</b> §4's stated one — five will not fit in 620 — is
/// void with three: measured on 2026-08-21, the pills total <b>263 px in Spanish and 241 in English</b>
/// inside a 620-wide column, with 357 to spare. What earns the change is the shape itself: a
/// horizontal <c>StackPanel</c> offers its children infinite width and draws them where they fall,
/// which is how this repository has put a control outside the window eight times. These labels are
/// translated, so their length is not ours to fix.
/// </para>
/// </remarks>
public sealed class AppearanceSettingsTests
{
    /// <summary>
    /// Three theme pills, because high contrast is read from Windows rather than picked.
    /// </summary>
    [AvaloniaFact]
    public void The_page_offers_one_pill_per_theme_preference_and_no_more()
    {
        var (window, view) = Show("es-ES");

        var themes = Enum.GetValues<ThemePreference>();
        Assert.Equal(3, themes.Length);

        var named = themes.Select(preference => Resource("Theme" + preference)).ToArray();
        var pills = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("theme-option"))
            .Select(button => Avalonia.Automation.AutomationProperties.GetName(button))
            .ToArray();

        foreach (var word in named)
        {
            Assert.Contains(word, pills, StringComparer.Ordinal);
        }

        // The two languages are pills of the same class, so the count is the themes plus those two.
        Assert.Equal(themes.Length + 2, pills.Length);
        window.Close();
    }

    /// <summary>
    /// Neither row of pills is a horizontal stack, and none of them leaves its column.
    /// </summary>
    /// <remarks>
    /// Both halves, because they say different things: the panel is what stops a future label
    /// spilling, and the geometry is what says today's labels do not. Measured in both languages,
    /// since the length of these words is decided by a translator and not here.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("es-ES")]
    [InlineData("en-US")]
    public void The_pills_wrap_instead_of_stacking_and_stay_inside_their_column(string culture)
    {
        var (window, view) = Show(culture);

        var stacked = view.GetVisualDescendants()
            .OfType<StackPanel>()
            .Where(panel => panel.Orientation == Orientation.Horizontal
                && panel.GetVisualChildren().OfType<Button>().Any(button => button.Classes.Contains("theme-option")))
            .ToArray();
        Assert.Empty(stacked);

        var rows = view.GetVisualDescendants()
            .OfType<WrapPanel>()
            .Where(panel => panel.GetVisualChildren().OfType<Button>().Any(button => button.Classes.Contains("theme-option")))
            .ToArray();
        Assert.Equal(2, rows.Length);

        foreach (var row in rows)
        {
            foreach (var pill in row.GetVisualChildren().OfType<Button>())
            {
                var corner = pill.TranslatePoint(new Point(pill.Bounds.Width, 0), row);
                Assert.True(
                    corner is { } point && point.X <= row.Bounds.Width,
                    $"a pill ends at {corner} in a {row.Bounds.Width:F0}-wide row in {culture}.");
            }
        }

        window.Close();
    }

    /// <summary>
    /// The reduced-motion notice says which of the two states Windows is in.
    /// </summary>
    /// <remarks>
    /// It used to say "AP Reelume respects the Windows reduced-motion preference" whether or not the
    /// preference was on — a sentence about the application's intentions rather than about the
    /// machine. The answer was already in the tree: <c>IThemeService.AnimationsEnabled</c> is
    /// <c>!IReducedMotionService.IsEnabled</c>, so the page holds the service that knows and said
    /// nothing with it. Asserted in both directions: a notice that always says "on" would satisfy
    /// half of this.
    /// </remarks>
    [AvaloniaFact]
    public void The_reduced_motion_notice_says_which_state_windows_is_in()
    {
        var (movingWindow, moving) = Show("es-ES", animations: true);
        var whenMoving = Notice(moving);
        movingWindow.Close();

        var (stillWindow, still) = Show("es-ES", animations: false);
        var whenStill = Notice(still);
        stillWindow.Close();

        Assert.Equal(Resource("ReducedMotionNotice"), whenMoving);
        Assert.Equal(Resource("ReducedMotionActiveNotice"), whenStill);
        Assert.NotEqual(whenMoving, whenStill);
    }

    /// <summary>
    /// Both sentences exist in both languages, and neither survived translation by not being translated.
    /// </summary>
    [AvaloniaFact]
    public void Both_sentences_are_written_in_both_languages()
    {
        var byLanguage = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var culture in new[] { "es-ES", "en-US" })
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(culture));
            byLanguage[culture] = [Resource("ReducedMotionNotice"), Resource("ReducedMotionActiveNotice")];
        }

        Assert.NotEqual(byLanguage["es-ES"][0], byLanguage["en-US"][0]);
        Assert.NotEqual(byLanguage["es-ES"][1], byLanguage["en-US"][1]);
    }

    private static string Notice(AppearanceSettingsView view)
    {
        var surface = Assert.Single(
            view.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "ReducedMotionSurface");
        var block = Assert.Single(surface.GetVisualDescendants().OfType<TextBlock>());
        return block.Text ?? string.Empty;
    }

    private static (Window Window, AppearanceSettingsView View) Show(string culture, bool animations = true)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(culture));

        var view = new AppearanceSettingsView
        {
            DataContext = new AppearanceSettingsViewModel(new StubTheme(animations)),
        };
        var window = new Window { Width = 900, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static string Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        return Assert.IsType<string>(value);
    }

    private sealed class StubTheme(bool animations) : IThemeService
    {
        public ThemePreference CurrentPreference => ThemePreference.System;

        public ThemeVariant PlayerThemeVariant => ThemeVariant.Dark;

        public bool AnimationsEnabled { get; } = animations;

        public TimeSpan MotionDuration => AnimationsEnabled ? TimeSpan.FromMilliseconds(150) : TimeSpan.Zero;

        public void Apply(ThemePreference preference)
        {
        }

        public bool TryApplyBackdrop(Window window) => false;
    }
}
