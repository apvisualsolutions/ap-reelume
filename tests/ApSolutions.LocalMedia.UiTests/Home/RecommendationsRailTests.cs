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
            id => readModel.TitleOf(id));

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
            id => readModel.TitleOf(id));
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
            id => readModel.TitleOf(id));
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
            id => readModel.TitleOf(id));

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
            _ => string.Empty));
        Assert.Throws<ArgumentNullException>(() => new RecommendationsViewModel(
            new GetRecommendations(new StubRecommendationReadModel()),
            null!,
            _ => string.Empty));
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
            id => readModel.TitleOf(id));
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
                GetRepositoryRoot(),
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

    [Fact]
    public void Every_reason_code_has_a_resource_key_in_both_languages()
    {
        var presentationRoot = Path.Combine(
            GetRepositoryRoot(),
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

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
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
}
