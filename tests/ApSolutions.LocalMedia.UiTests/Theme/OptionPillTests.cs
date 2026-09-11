// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Language;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Presentation.Theme;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// An option pill says which one is chosen the way the prototype says it: with its own edge, its own
/// fill and its weight, and no circle beside the word.
/// </summary>
/// <remarks>
/// <para>
/// Until 2026-09-11 every pill in this tree carried a <c>●</c> or a <c>○</c> before its label, and the
/// owner read it for what it looked like: «aparece el selector del radial». The prototype never draws
/// one — not in the library's kind pills, not in the theme row, not in the player's column pills, and
/// not in either high contrast mode, where it marks the chosen pill with a border the others do not
/// have. That is the same answer the accent swatches got when the owner objected to the same circle
/// there: a border is a shape, and a shape survives both high contrast dictionaries.
/// </para>
/// <para>
/// <b>And in eighteen of the twenty-seven the circle was not a second signal but the only one.</b> The
/// theme, density, rounding and language rows, the three kinds on the first run and the two halves of
/// the add-folder dialog never bound the chosen style at all, so taking the circle away without the
/// second test below would have left them with no way to say which is in force.
/// </para>
/// </remarks>
public sealed class OptionPillTests
{
    /// <summary>The count the tree draws today, and the floor this has to find before it judges.</summary>
    private const int PillsInTheTree = 27;

    [AvaloniaFact]
    public void No_option_pill_paints_a_circle_beside_its_label()
    {
        using var scope = new Scope(
            new ShellView(),
            new AppearanceSettingsView(),
            new RootOnboardingView(),
            new AddRootDialogView());

        var pills = scope.Pills();
        Assert.True(
            pills.Length >= PillsInTheTree,
            $"only {pills.Length} option pills were found and the tree draws {PillsInTheTree}, so an "
                + "empty answer below would prove nothing.");

        var circles = pills
            .Where(pill => pill.GetVisualDescendants().OfType<TextBlock>().Any(IsCircle))
            .Select(pill => Automation(pill))
            .ToArray();
        Assert.Empty(circles);
    }

    /// <summary>
    /// Each row of the appearance page lights exactly the pill that is in force, and moves with it.
    /// </summary>
    /// <remarks>
    /// The pills are told apart by the value they send rather than by their words, so the test reads
    /// the same in both languages and a translation cannot pass it by accident.
    /// </remarks>
    [AvaloniaFact]
    public void Every_appearance_row_paints_the_one_in_force_and_only_that_one()
    {
        var theme = new HeldTheme(ThemePreference.Light);
        var language = new HeldLanguage("es");
        var appearance = new HeldAppearance(new AppearanceOptions() with
        {
            Density = InterfaceDensity.Roomy,
            Rounding = CornerRounding.Sharp,
        });
        var page = new AppearanceSettingsViewModel(theme, language, appearance);
        using var scope = new Scope(new AppearanceSettingsView { DataContext = page });

        Assert.Equal(
            Ordered(ThemePreference.Light, InterfaceDensity.Roomy, CornerRounding.Sharp, "es"),
            scope.Chosen());

        page.ApplyThemeCommand.Execute(ThemePreference.HighContrastDark);
        page.ApplyDensityCommand.Execute(InterfaceDensity.Compact);
        page.ApplyRoundingCommand.Execute(CornerRounding.VeryRound);
        page.ApplyLanguageCommand.Execute("en");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            Ordered(ThemePreference.HighContrastDark, InterfaceDensity.Compact, CornerRounding.VeryRound, "en"),
            scope.Chosen());
    }

    private static bool IsCircle(TextBlock block) =>
        block.Classes.Contains("state-glyph") || block.Text is "●" or "○";

    private static string Automation(Button pill) =>
        Avalonia.Automation.AutomationProperties.GetName(pill) ?? pill.CommandParameter?.ToString() ?? "?";

    private static string[] Ordered(params object[] values) =>
        [.. values.Select(value => value.ToString() ?? string.Empty).Order(StringComparer.Ordinal)];

    private sealed class Scope : IDisposable
    {
        private readonly Window[] _windows;
        private readonly Control[] _views;

        internal Scope(params Control[] views)
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
            _views = views;
            _windows = [.. views.Select(view =>
            {
                var window = new Window { Width = 1280, Height = 900, Content = view };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                return window;
            })];
        }

        /// <summary>Every option pill in the mounted views: both the page's and the player's.</summary>
        internal Button[] Pills() =>
        [
            .. _views.SelectMany(view => view.GetVisualDescendants().OfType<Button>())
                .Where(button => button.Classes.Contains("theme-option") || button.Classes.Contains("player-pill")),
        ];

        /// <summary>The value each lit pill sends, sorted, so a row is judged by what it chooses.</summary>
        internal string[] Chosen() =>
        [
            .. Pills()
                .Where(pill => pill.Classes.Contains("selected"))
                .Select(pill => pill.CommandParameter?.ToString() ?? string.Empty)
                .Order(StringComparer.Ordinal),
        ];

        public void Dispose()
        {
            foreach (var window in _windows)
            {
                window.Close();
            }
        }
    }

    private sealed class HeldTheme(ThemePreference preference) : IThemeService
    {
        public ThemePreference CurrentPreference { get; private set; } = preference;

        public ThemeVariant PlayerThemeVariant => ThemeVariant.Dark;

        public bool AnimationsEnabled => true;

        public TimeSpan MotionDuration => TimeSpan.FromMilliseconds(150);

        public void Apply(ThemePreference preference) => CurrentPreference = preference;

        public bool TryApplyBackdrop(Window window) => false;
    }

    private sealed class HeldLanguage(string current) : ILanguageService
    {
        public string Current { get; private set; } = current;

        public void Apply(string language) => Current = language;
    }

    private sealed class HeldAppearance(AppearanceOptions options) : IAppearanceService
    {
        public AppearanceOptions Current { get; private set; } = options;

        public bool WantsBackdrop => false;

        public void Apply(AppearanceOptions options) => Current = options;

        public void Reapply()
        {
        }
    }
}
