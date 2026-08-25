// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Xml.Linq;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Home;

/// <summary>
/// The recommendations rail and its switch. Every card explains itself in localised words, and turning
/// the feature off empties the rail rather than hiding a computed result.
/// </summary>
public sealed class RecommendationsRailTests
{
    [Fact]
    public async Task The_rail_shows_ranked_titles_with_a_localizable_reason_for_each()
    {
        var readModel = new StubRecommendationReadModel
        {
            Taste = new RecommendationTaste(
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Drama"] = 1.0 },
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                AverageRating: 8,
                PreferredYear: 2016),
            Candidates =
            [
                Candidate(1, ["Comedia"], "Alfa"),
                Candidate(2, ["Drama"], "Beta"),
            ],
        };
        var viewModel = new RecommendationsViewModel(
            new GetRecommendations(readModel),
            new StubRecommendationSettings(enabled: true),
            TitlesOf(readModel));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsEnabled);
        Assert.True(viewModel.HasRecommendations);
        Assert.Equal(2, viewModel.Items.Count);
        Assert.Equal("Beta", viewModel.Items[0].Title);
        Assert.NotEmpty(viewModel.Items[0].ReasonKeys);
        Assert.All(
            viewModel.Items[0].ReasonKeys,
            key => Assert.StartsWith("RecommendationReason", key, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Turning_the_switch_off_empties_the_rail_and_is_remembered()
    {
        var settings = new StubRecommendationSettings(enabled: true);
        var readModel = new StubRecommendationReadModel
        {
            Candidates = [Candidate(1, ["Drama"], "Alfa")],
        };
        var viewModel = new RecommendationsViewModel(
            new GetRecommendations(readModel),
            settings,
            TitlesOf(readModel));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.HasRecommendations);

        await viewModel.SetEnabledAsync(false, TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsEnabled);
        Assert.False(viewModel.HasRecommendations);
        Assert.Empty(viewModel.Items);
        Assert.False(settings.IsEnabled);

        var restarted = new RecommendationsViewModel(
            new GetRecommendations(readModel),
            settings,
            TitlesOf(readModel));
        await restarted.LoadAsync(TestContext.Current.CancellationToken);
        Assert.False(restarted.IsEnabled);
        Assert.Empty(restarted.Items);
    }

    [Fact]
    public async Task A_disabled_rail_never_asks_the_read_model_for_anything()
    {
        var readModel = new StubRecommendationReadModel
        {
            Candidates = [Candidate(1, ["Drama"], "Alfa")],
        };
        var viewModel = new RecommendationsViewModel(
            new GetRecommendations(readModel),
            new StubRecommendationSettings(enabled: false),
            TitlesOf(readModel));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, readModel.Reads);
        Assert.Empty(viewModel.Items);
    }

    [Fact]
    public void The_settings_view_model_reads_and_writes_the_stored_switch()
    {
        var settings = new StubRecommendationSettings(enabled: false);
        var viewModel = new RecommendationSettingsViewModel(settings);

        Assert.False(viewModel.IsEnabled);
        viewModel.IsEnabled = true;
        Assert.True(settings.IsEnabled);
        Assert.True(new RecommendationSettingsViewModel(settings).IsEnabled);
    }

    [AvaloniaFact]
    public void A_reason_key_resolves_to_its_translated_words_and_an_unknown_key_stays_itself()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));
        var converter = new ResourceKeyConverter();

        var spanish = converter.Convert(
            "RecommendationReasonFreshness",
            typeof(string),
            null,
            CultureInfo.CurrentCulture);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("en-US"));
        var english = converter.Convert(
            "RecommendationReasonFreshness",
            typeof(string),
            null,
            CultureInfo.CurrentCulture);

        Assert.NotEqual(spanish, english);
        Assert.False(string.IsNullOrWhiteSpace(spanish as string));
        Assert.Equal(
            "NotAResourceKey",
            converter.Convert("NotAResourceKey", typeof(string), null, CultureInfo.CurrentCulture));
        Assert.Equal(
            string.Empty,
            converter.Convert(null, typeof(string), null, CultureInfo.CurrentCulture));
        Assert.Equal(
            string.Empty,
            converter.Convert("   ", typeof(string), null, CultureInfo.CurrentCulture));
        Assert.Throws<NotSupportedException>(
            () => converter.ConvertBack("x", typeof(string), null, CultureInfo.CurrentCulture));
    }

    [Fact]
    public void The_toggle_command_flips_the_switch_from_the_rail_itself()
    {
        var settings = new StubRecommendationSettings(enabled: true);
        var viewModel = new RecommendationsViewModel(
            new GetRecommendations(new StubRecommendationReadModel()),
            settings);

        Assert.True(viewModel.ToggleCommand.CanExecute(null));
        viewModel.ToggleCommand.Execute(null);

        Assert.False(settings.IsEnabled);
        Assert.True(viewModel.IsDisabled);
    }

    [Fact]
    public void The_settings_view_model_ignores_a_write_that_changes_nothing()
    {
        var settings = new StubRecommendationSettings(enabled: true);
        var viewModel = new RecommendationSettingsViewModel(settings);
        var changes = new List<string>();
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName ?? string.Empty);

        viewModel.IsEnabled = true;
        Assert.Empty(changes);

        viewModel.IsEnabled = false;
        Assert.Contains(nameof(RecommendationSettingsViewModel.IsEnabled), changes, StringComparer.Ordinal);
        Assert.Contains(nameof(RecommendationSettingsViewModel.IsDisabled), changes, StringComparer.Ordinal);
        Assert.True(viewModel.IsDisabled);
    }

    [Fact]
    public void The_view_models_reject_missing_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new RecommendationsViewModel(
            null!,
            new StubRecommendationSettings(true),
            NoTitles));
        Assert.Throws<ArgumentNullException>(() => new RecommendationsViewModel(
            new GetRecommendations(new StubRecommendationReadModel()),
            null!,
            NoTitles));
        Assert.Throws<ArgumentNullException>(() => new RecommendationSettingsViewModel(null!));
    }

    [AvaloniaFact]
    public async Task Both_surfaces_are_named_focusable_and_free_of_literal_text()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));
        var readModel = new StubRecommendationReadModel
        {
            Candidates = [Candidate(1, ["Drama"], "Alfa")],
        };
        var rail = new RecommendationsViewModel(
            new GetRecommendations(readModel),
            new StubRecommendationSettings(enabled: true),
            TitlesOf(readModel));
        await rail.LoadAsync(TestContext.Current.CancellationToken);

        var window = new Window
        {
            Width = 1024,
            Height = 720,
            Content = new StackPanel
            {
                Children =
                {
                    new RecommendationsRailView { DataContext = rail },
                    new RecommendationSettingsView
                    {
                        DataContext = new RecommendationSettingsViewModel(
                            new StubRecommendationSettings(enabled: true)),
                    },
                },
            },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var controls = window.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control is Button or CheckBox or ToggleSwitch)
            .ToArray();
        Assert.NotEmpty(controls);
        foreach (var control in controls)
        {
            Assert.True(control.Focusable);
            Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)),
                "A recommendation control has no accessible name.");
        }

        window.Close();

        foreach (var view in new[]
        {
            Path.Combine("Home", "RecommendationsRailView.axaml"),
            Path.Combine("Settings", "RecommendationSettingsView.axaml"),
        })
        {
            var path = Path.Combine(
                RepositoryLayout.Root,
                "src",
                "ApSolutions.LocalMedia.Presentation",
                view);
            Assert.True(File.Exists(path), $"Missing view: {path}");
            var literals = XDocument.Load(path)
                .Descendants()
                .Attributes()
                .Where(attribute => attribute.Name.LocalName is "Text" or "Content" or "Header")
                .Where(attribute => !attribute.Value.TrimStart().StartsWith('{'))
                .Select(attribute => $"{Path.GetFileName(path)}:{attribute.Value}")
                .ToArray();
            Assert.Empty(literals);
        }
    }

    /// <summary>
    /// The rail has three states and switched off is not one of the other two.
    /// </summary>
    /// <remarks>
    /// Empty means the formula ran and ranked nothing; switched off means it never ran, because
    /// <c>GetRecommendations</c> returns before reading anything. Painting the empty sentence in both
    /// says "there is nothing to suggest" about a catalogue nobody looked at, which is the one claim
    /// the switch exists to avoid making. Asserted on what is <b>on screen</b> rather than on the
    /// bindings, because a binding that is right and a panel that is collapsed look the same here.
    /// </remarks>
    [AvaloniaFact]
    public async Task Switched_off_is_not_the_same_as_empty_and_the_rail_says_which()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));
        var empty = Resource("RecommendationsEmpty");
        var off = Resource("RecommendationsOffDescription");
        var stocked = new StubRecommendationReadModel
        {
            Candidates = [Candidate(1, ["Drama"], "Alfa")],
        };

        var switchedOff = await VisibleTextsAsync(stocked, enabled: false);
        Assert.Contains(off, switchedOff);
        Assert.DoesNotContain(empty, switchedOff);

        var ranNothing = await VisibleTextsAsync(new StubRecommendationReadModel(), enabled: true);
        Assert.Contains(empty, ranNothing);
        Assert.DoesNotContain(off, ranNothing);

        var withContent = await VisibleTextsAsync(stocked, enabled: true);
        Assert.DoesNotContain(empty, withContent);
        Assert.DoesNotContain(off, withContent);
    }

    [Fact]
    public void Every_reason_code_has_a_resource_key_in_both_languages()
    {
        var presentationRoot = Path.Combine(
            RepositoryLayout.Root,
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Resources");
        foreach (var language in new[] { "es", "en" })
        {
            var keys = XDocument.Load(Path.Combine(presentationRoot, $"Strings.{language}.axaml"))
                .Descendants()
                .Attributes()
                .Where(attribute => attribute.Name.LocalName == "Key")
                .Select(attribute => attribute.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var reason in Enum.GetValues<RecommendationReason>())
            {
                Assert.Contains($"RecommendationReason{reason}", keys);
            }
        }
    }

    /// <summary>The words a mounted rail actually puts on screen, collapsed branches excluded.</summary>
    private static async Task<string[]> VisibleTextsAsync(StubRecommendationReadModel readModel, bool enabled)
    {
        var rail = new RecommendationsViewModel(
            new GetRecommendations(readModel),
            new StubRecommendationSettings(enabled),
            TitlesOf(readModel));
        await rail.LoadAsync(TestContext.Current.CancellationToken);

        var window = new Window
        {
            Width = 1024,
            Height = 720,
            Content = new RecommendationsRailView { DataContext = rail },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var texts = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Text ?? string.Empty)
            .ToArray();
        window.Close();
        return texts;
    }

    /// <summary>A string from the language dictionary, which does not vary by theme variant.</summary>
    private static string Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        return Assert.IsType<string>(value);
    }

    private static RecommendationCandidate Candidate(int seed, string[] genres, string title)
    {
        _ = title;
        return new RecommendationCandidate(
            Title(seed),
            genres,
            [],
            2016,
            IsAvailable: true,
            IsWatched: false,
            Rating: null);
    }

    private static TitleId Title(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new TitleId(new Guid(bytes));
    }

    private sealed class StubRecommendationSettings(bool enabled) : IRecommendationSettings
    {
        public bool IsEnabled { get; private set; } = enabled;

        public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;
    }

    private sealed class StubRecommendationReadModel : IRecommendationReadModel
    {
        private static readonly string[] Titles = ["Alfa", "Beta", "Gamma", "Delta"];

        public int Reads { get; private set; }

        public RecommendationTaste Taste { get; init; } = RecommendationTaste.Empty;

        public IReadOnlyList<RecommendationCandidate> Candidates { get; init; } = [];

        public string TitleOf(TitleId id)
        {
            var index = Candidates
                .Select((candidate, position) => (candidate, position))
                .FirstOrDefault(entry => entry.candidate.Id == id)
                .position;
            return Titles[index % Titles.Length];
        }

        public Task<RecommendationTaste> ReadTasteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            return Task.FromResult(Taste);
        }

        public Task<IReadOnlyList<RecommendationCandidate>> ReadCandidatesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            return Task.FromResult(Candidates);
        }
    }

    /// <summary>
    /// The words behind a page of ids, which the rail now asks for once instead of card by card.
    /// </summary>
    /// <remarks>
    /// The lookup went from <c>Func&lt;TitleId, string&gt;</c> to a batch on 2026-08-25, because the
    /// composition had never fed it: the catalogue is asked over a connection and a per-card
    /// synchronous lookup can only be answered by blocking the thread that draws the cards. What the
    /// rail drew until then was twenty covers of initials of nothing.
    /// </remarks>
    private static Func<IReadOnlyList<TitleId>, CancellationToken, Task<IReadOnlyDictionary<TitleId, string>>>
        TitlesOf(StubRecommendationReadModel readModel) =>
        (ids, _) => Task.FromResult<IReadOnlyDictionary<TitleId, string>>(
            ids.ToDictionary(id => id, readModel.TitleOf));

    private static Task<IReadOnlyDictionary<TitleId, string>> NoTitles(
        IReadOnlyList<TitleId> ids,
        CancellationToken cancellationToken)
    {
        _ = ids;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyDictionary<TitleId, string>>(new Dictionary<TitleId, string>());
    }
}
