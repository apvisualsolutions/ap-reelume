// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Globalization;

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Language;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Presentation.Theme;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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
    /// <summary>
    /// Every option pill the shell draws: the library's three kinds, the appearance page's thirteen,
    /// the first run's three kinds and the add dialog's two halves, the player's six column pills and
    /// the audio output's three layouts.
    /// </summary>
    /// <remarks>
    /// An exact count and not a floor. This test started with a floor of 27 against the 30 the shell
    /// holds, and a gate audit on 2026-09-11 measured what that slack let through: a pill hidden by a
    /// binding's fallback still counted towards the floor, with a circle inside it that nothing looked
    /// at. A pill added to the tree now fails here until somebody counts it, which is the point.
    /// </remarks>
    private const int PillsInTheShell = 30;

    [AvaloniaFact]
    public void No_option_pill_paints_a_circle_beside_its_label()
    {
        using var scope = new Scope(new ShellView());

        var pills = scope.Pills();
        Assert.Equal(PillsInTheShell, pills.Length);

        // Every one on screen and wearing its word, which is what lets "nothing inside it is a circle"
        // mean something: a pill that is not realised has no children to look at, and would pass.
        var unseen = pills
            .Where(pill => !pill.IsEffectivelyVisible || Label(pill) is null)
            .Select(Automation)
            .ToArray();
        Assert.Empty(unseen);

        // A circle drawn as a shape — an Ellipse, a Path — and one written as a character. The same
        // audit put a 10 px Ellipse beside «Claro» and the character-only version of this test stayed
        // green. A pill is a word: the prototype's `<button aria-pressed>` with nothing beside it.
        var circles = pills
            .Where(pill => pill.GetVisualDescendants().Any(child => child is Shape)
                || pill.GetVisualDescendants().OfType<TextBlock>().Any(IsCircle))
            .Select(Automation)
            .ToArray();
        Assert.Empty(circles);
    }

    /// <summary>
    /// Each row of the appearance page lights exactly the pill that is in force, and moves with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pills are told apart by the value they send rather than by their words, so the test reads
    /// the same in both languages and a translation cannot pass it by accident.
    /// </para>
    /// <para>
    /// It starts where a new installation starts — the system's theme, comfortable, soft — and then
    /// puts every value of every row in force once, so each of the thirteen is seen lit and the rest
    /// of its row is seen dark while it is. Until 2026-09-11 it went from one hand-picked state to
    /// another, which lit eight of the thirteen: the first-run three among the five it never tried,
    /// and a flag that answered for the wrong theme passed.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Every_appearance_row_paints_the_one_in_force_and_only_that_one()
    {
        var theme = new HeldTheme(ThemePreference.System);
        var language = new HeldLanguage("es");
        var appearance = new HeldAppearance(new AppearanceOptions());
        var page = new AppearanceSettingsViewModel(theme, language, appearance);
        using var scope = new Scope(new AppearanceSettingsView { DataContext = page });

        // Three rows since 2026-09-13, where there were four: the language left this page for a
        // destination of its own (UX-010) and is measured below, on the view it moved to. Measured
        // where it went rather than dropped, because a list that only got shorter would read the
        // same whether the pills moved or stopped painting.
        object[] inForce = [ThemePreference.System, InterfaceDensity.Comfortable, CornerRounding.Soft];
        Assert.Equal(Ordered(inForce), scope.Chosen());

        foreach (var value in Enum.GetValues<ThemePreference>())
        {
            page.ApplyThemeCommand.Execute(value);
            inForce[0] = value;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Ordered(inForce), scope.Chosen());
        }

        foreach (var value in Enum.GetValues<InterfaceDensity>())
        {
            page.ApplyDensityCommand.Execute(value);
            inForce[1] = value;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Ordered(inForce), scope.Chosen());
        }

        foreach (var value in Enum.GetValues<CornerRounding>())
        {
            page.ApplyRoundingCommand.Execute(value);
            inForce[2] = value;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Ordered(inForce), scope.Chosen());
        }

    }

    /// <summary>
    /// The language pills, on the view they moved to, and the same grammar: the one in force wears
    /// the chosen edge and the other does not.
    /// </summary>
    [AvaloniaFact]
    public void The_language_row_paints_the_one_in_force_and_only_that_one()
    {
        var language = new HeldLanguage("es");
        var page = new AppearanceSettingsViewModel(
            new HeldTheme(ThemePreference.System),
            language,
            new HeldAppearance(new AppearanceOptions()));
        using var scope = new Scope(new LanguageSettingsView { DataContext = page });

        object[] inForce = ["es"];
        Assert.Equal(Ordered(inForce), scope.Chosen());

        foreach (var value in new[] { "en", "es" })
        {
            page.ApplyLanguageCommand.Execute(value);
            inForce[0] = value;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Ordered(inForce), scope.Chosen());
        }
    }

    /// <summary>The library's three kind pills each light for their own kind, and alone.</summary>
    /// <remarks>
    /// The gate that said every pill binds its chosen style read that the binding was written, not
    /// what it pointed at: swapping the bindings of Películas and Series lit the wrong one of the two
    /// and nothing failed. Each pill is pressed here through its own command, as a click would.
    /// </remarks>
    [AvaloniaFact]
    public void The_library_kind_pills_each_light_for_their_own_kind()
    {
        var library = new LibraryViewModel(new EmptyCatalog());
        using var scope = new Scope(new LibraryView { DataContext = library });

        var pills = scope.Pills();
        Assert.Equal(3, pills.Length);
        foreach (var pill in pills)
        {
            pill.Command!.Execute(pill.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal([pill.CommandParameter?.ToString() ?? string.Empty], scope.Chosen());
        }
    }

    private static TextBlock? Label(Button pill) =>
        pill.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(block => block.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(block.Text));

    private static bool IsCircle(TextBlock block) =>
        block.Classes.Contains("state-glyph") || block.Text is "●" or "○";

    private static string Automation(Button pill) =>
        Avalonia.Automation.AutomationProperties.GetName(pill) ?? pill.CommandParameter?.ToString() ?? "?";

    private static string[] Ordered(object[] values) =>
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

    private sealed class EmptyCatalog : ICatalogQueryService
    {
        public Task<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CatalogPage([], null));
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
