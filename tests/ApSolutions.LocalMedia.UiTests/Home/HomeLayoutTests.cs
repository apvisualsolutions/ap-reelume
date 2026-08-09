using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Navigation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Home;

public sealed class HomeLayoutTests
{
    private static readonly TitleId Movie = new(CreateGuid(11));
    private static readonly TitleId Show = new(CreateGuid(12));
    private static readonly EpisodeId Episode = new(CreateGuid(13));
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions BaselineOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly string[] SurfaceFolders = ["Home", "Movie", "Show"];

    [AvaloniaFact]
    public async Task Continue_is_the_first_focus_only_when_there_is_offerable_progress()
    {
        ApplyLanguage("es-ES");
        var withProgress = await CreateViewModelAsync(EpisodeProgress());
        using (var host = Host(withProgress, 1366, 768, 1.0))
        {
            Assert.True(withProgress.HasResume);
            Assert.Equal("ResumeHeroAction", FocusedName(host));
        }

        var withoutProgress = await CreateViewModelAsync();
        using (var host = Host(withoutProgress, 1366, 768, 1.0))
        {
            Assert.False(withoutProgress.HasResume);
            Assert.Equal("LibraryEntryAction", FocusedName(host));
        }
    }

    [AvaloniaFact]
    public async Task Library_access_stays_inside_the_first_viewport_at_1366_by_768()
    {
        ApplyLanguage("es-ES");
        var viewModel = await CreateViewModelAsync(EpisodeProgress(), MovieProgress());
        using var host = Host(viewModel, 1366, 768, 1.0);

        var library = Named(host, "LibraryEntryAction");
        var bounds = library.Bounds;
        var origin = library.TranslatePoint(new Point(0, 0), host.Window) ?? new Point(-1, -1);

        Assert.True(library.IsVisible, "The library entry must be visible on Home.");
        Assert.True(
            origin.Y + bounds.Height <= 768,
            $"Library access ended at {origin.Y + bounds.Height:F0} px, past the 768 px viewport.");
        Assert.True(
            origin.X + bounds.Width <= 1366,
            $"Library access ended at {origin.X + bounds.Width:F0} px, past the 1366 px viewport.");
    }

    [AvaloniaFact]
    public async Task The_in_progress_rail_virtualizes_and_marks_unavailable_items()
    {
        ApplyLanguage("es-ES");
        var entries = Enumerable.Range(0, 60)
            .Select(index => Progress(
                new TitleId(CreateGuid(1_000 + index)),
                $"Título {index:D3}",
                isAvailable: index % 10 != 0,
                updatedUtc: Noon.AddMinutes(-index)))
            .ToArray();
        var viewModel = await CreateViewModelAsync(entries);
        using var host = Host(viewModel, 1366, 768, 1.0);

        var rail = Assert.IsAssignableFrom<ItemsControl>(Named(host, "InProgressRail"));
        Assert.NotNull(rail.ItemsPanelRoot);
        Assert.IsType<VirtualizingStackPanel>(rail.ItemsPanelRoot);
        Assert.Equal(60, viewModel.InProgress.Count);
        Assert.True(
            rail.GetRealizedContainers().Count() < 60,
            "The rail realized every item, so it is not virtualizing.");
        Assert.Contains(viewModel.InProgress, item => !item.IsAvailable);
    }

    [AvaloniaFact]
    public async Task Home_matches_its_approved_structural_baseline_across_every_combination()
    {
        var actual = new List<LayoutRecord>();
        var captures = Path.Combine(GetRepositoryRoot(), "artifacts", "ui-captures", "T30");
        Directory.CreateDirectory(captures);

        foreach (var (widthPixels, heightPixels) in new[] { (1366, 768), (3840, 2160) })
        {
            foreach (var scale in new[] { 1.0, 1.5, 2.0 })
            {
                foreach (var theme in new[] { "Light", "Dark", "HighContrast" })
                {
                    foreach (var language in new[] { "es", "en" })
                    {
                        actual.Add(await CaptureAsync(
                            widthPixels,
                            heightPixels,
                            scale,
                            theme,
                            language,
                            captures));
                    }
                }
            }
        }

        var actualPath = Path.Combine(
            GetRepositoryRoot(),
            "artifacts",
            "test-results",
            "T30",
            "home-layout-actual.json");
        Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);
        await File.WriteAllTextAsync(
            actualPath,
            JsonSerializer.Serialize(actual, BaselineOptions),
            TestContext.Current.CancellationToken);

        var baselinePath = Path.Combine(
            GetRepositoryRoot(),
            "tests",
            "ApSolutions.LocalMedia.UiTests",
            "Baselines",
            "T30",
            "home-layout.json");
        Assert.True(
            File.Exists(baselinePath),
            $"The approved structural baseline is missing: {baselinePath}");
        var baseline = JsonSerializer.Deserialize<List<LayoutRecord>>(
            await File.ReadAllTextAsync(baselinePath, TestContext.Current.CancellationToken))
            ?? [];

        Assert.Equal(36, actual.Count);
        Assert.Equal(baseline.Select(record => record.Id), actual.Select(record => record.Id));
        foreach (var (expected, observed) in baseline.Zip(actual))
        {
            // The focus order is compared as a sequence; the rest of the record compares by value.
            Assert.Equal(expected.FocusOrder, observed.FocusOrder);
            Assert.Equal(expected with { FocusOrder = [] }, observed with { FocusOrder = [] });
        }
    }

    [Fact]
    public async Task The_hero_states_the_title_the_episode_and_the_percentage_in_words()
    {
        var viewModel = await CreateViewModelAsync(EpisodeProgress());

        Assert.True(viewModel.HasResume);
        Assert.Equal("Crónicas", viewModel.ResumeTitle);
        Assert.True(viewModel.HasResumeSubtitle);
        Assert.Contains("Ned", viewModel.ResumeSubtitle, StringComparison.Ordinal);
        Assert.Equal(0.2, viewModel.ResumeCompletedFraction, 3);
        Assert.Equal("20", viewModel.ResumeCompletedText);
    }

    [Fact]
    public async Task A_film_has_no_episode_subtitle_and_an_empty_home_states_no_progress()
    {
        var film = await CreateViewModelAsync(MovieProgress());
        Assert.False(film.HasResumeSubtitle);
        Assert.Equal(string.Empty, film.ResumeSubtitle);
        Assert.False(film.InProgress[0].HasSubtitle);
        Assert.False(film.InProgress[0].IsShow);

        var empty = await CreateViewModelAsync();
        Assert.False(empty.HasResume);
        Assert.False(empty.HasInProgress);
        Assert.Equal(string.Empty, empty.ResumeTitle);
        Assert.Equal("0", empty.ResumeCompletedText);
    }

    [Fact]
    public async Task The_rail_card_states_season_episode_and_percentage_for_a_series()
    {
        var viewModel = await CreateViewModelAsync(EpisodeProgress());

        var card = Assert.Single(viewModel.InProgress);
        Assert.True(card.IsShow);
        Assert.True(card.HasSubtitle);
        Assert.Contains("1", card.Subtitle, StringComparison.Ordinal);
        Assert.Contains("2", card.Subtitle, StringComparison.Ordinal);
        Assert.Equal("20", card.CompletedText);
        Assert.Equal(ContentKey.ForEpisode(Show, Episode), card.Content);
        Assert.Equal(Show, card.TitleId);
    }

    [Fact]
    public async Task The_library_summary_counts_titles_and_names_unavailable_ones()
    {
        var viewModel = new HomeViewModel(new GetHome(new StubHomeReadModel([])
        {
            Summary = new LibrarySummary(4, 2, 3),
            RecentlyAdded =
            [
                new RecentlyAddedItem(Movie, CatalogTitleKind.Movie, "Arrival", 2016, true, Noon),
                new RecentlyAddedItem(Show, CatalogTitleKind.Show, "Crónicas", null, false, Noon),
            ],
        }));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, viewModel.MovieCount);
        Assert.Equal(2, viewModel.ShowCount);
        Assert.Equal(3, viewModel.UnavailableCount);
        Assert.True(viewModel.HasUnavailable);
        Assert.Contains("4", viewModel.LibrarySummaryText, StringComparison.Ordinal);
        Assert.Contains("2", viewModel.LibrarySummaryText, StringComparison.Ordinal);

        Assert.True(viewModel.HasRecentlyAdded);
        Assert.Equal(2, viewModel.RecentlyAdded.Count);
        Assert.True(viewModel.RecentlyAdded[0].HasYear);
        Assert.Equal("2016", viewModel.RecentlyAdded[0].YearText);
        Assert.True(viewModel.RecentlyAdded[0].IsAvailable);
        Assert.False(viewModel.RecentlyAdded[1].HasYear);
        Assert.Equal(string.Empty, viewModel.RecentlyAdded[1].YearText);
        Assert.True(viewModel.RecentlyAdded[1].IsShow);
        Assert.False(viewModel.RecentlyAdded[1].IsAvailable);
        Assert.Equal(Movie, viewModel.RecentlyAdded[0].Id);
    }

    [Fact]
    public async Task Continue_reaches_the_host_with_the_content_key_and_does_nothing_when_empty()
    {
        var resumed = new List<ContentKey>();
        var withProgress = new HomeViewModel(
            new GetHome(new StubHomeReadModel([EpisodeProgress()])),
            new NavigationService(),
            content =>
            {
                resumed.Add(content);
                return Task.CompletedTask;
            });
        await withProgress.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(withProgress.ResumeCommand.CanExecute(null));
        withProgress.ResumeCommand.Execute(null);
        Assert.Equal([ContentKey.ForEpisode(Show, Episode)], resumed);

        var empty = new HomeViewModel(
            new GetHome(new StubHomeReadModel([])),
            new NavigationService(),
            content =>
            {
                resumed.Add(content);
                return Task.CompletedTask;
            });
        await empty.LoadAsync(TestContext.Current.CancellationToken);
        Assert.False(empty.ResumeCommand.CanExecute(null));
        empty.ResumeCommand.Execute(null);
        Assert.Single(resumed);
    }

    [Fact]
    public async Task The_library_shortcut_navigates_and_refresh_reloads_the_snapshot()
    {
        var navigation = new NavigationService();
        var readModel = new StubHomeReadModel([EpisodeProgress()]);
        var viewModel = new HomeViewModel(new GetHome(readModel), navigation);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        viewModel.OpenLibraryCommand.Execute(null);
        Assert.Equal(AppRoute.Library, navigation.CurrentRoute);

        Assert.True(viewModel.RefreshCommand.CanExecute(null));
        viewModel.RefreshCommand.Execute(null);
        Assert.True(readModel.ProgressReads >= 2);
    }

    [Fact]
    public void The_view_model_rejects_a_missing_use_case_and_a_missing_snapshot()
    {
        Assert.Throws<ArgumentNullException>(() => new HomeViewModel(null!));
        Assert.Throws<ArgumentNullException>(() =>
            new HomeViewModel(new GetHome(new StubHomeReadModel([]))).Apply(null!));
    }

    [Fact]
    public void Every_visible_string_on_home_and_details_comes_from_a_resource()
    {
        var presentationRoot = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Presentation");
        var views = SurfaceFolders
            .Select(folder => Path.Combine(presentationRoot, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.axaml", SearchOption.AllDirectories))
            .ToArray();

        Assert.NotEmpty(views);
        foreach (var view in views)
        {
            var document = XDocument.Load(view);
            var literals = document.Descendants()
                .Attributes()
                .Where(attribute => attribute.Name.LocalName is "Text" or "Content" or "PlaceholderText"
                    or "ToolTip.Tip" or "Header")
                .Where(attribute => !attribute.Value.TrimStart().StartsWith('{'))
                .Select(attribute => $"{Path.GetFileName(view)}:{attribute.Name.LocalName}={attribute.Value}")
                .ToArray();
            Assert.Empty(literals);
        }
    }

    private static async Task<LayoutRecord> CaptureAsync(
        int widthPixels,
        int heightPixels,
        double scale,
        string theme,
        string language,
        string captureDirectory)
    {
        ApplyLanguage(language == "en" ? "en-US" : "es-ES");
        Avalonia.Application.Current!.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => new ThemeVariant("HighContrast", ThemeVariant.Light),
        };

        // The baseline captures Home as the host composes it, recommendations rail included.
        var viewModel = await CreateViewModelWithRailAsync(EpisodeProgress(), MovieProgress());
        var logicalWidth = widthPixels / scale;
        var logicalHeight = heightPixels / scale;
        using var host = Host(viewModel, logicalWidth, logicalHeight, scale);

        var identifier = string.Create(
            CultureInfo.InvariantCulture,
            $"{widthPixels}x{heightPixels}@{scale * 100:F0}-{theme}-{language}");
        var frame = host.Window.CaptureRenderedFrame();
        if (frame is not null)
        {
            frame.Save(Path.Combine(captureDirectory, $"home-{identifier}.png"), PngBitmapEncoderOptions.Default);
        }

        var library = Named(host, "LibraryEntryAction");
        var libraryOrigin = library.TranslatePoint(new Point(0, 0), host.Window) ?? new Point(-1, -1);
        return new LayoutRecord(
            identifier,
            widthPixels,
            heightPixels,
            scale,
            theme,
            language,
            Math.Round(logicalWidth),
            Math.Round(logicalHeight),
            FocusedName(host),
            [.. FocusableNames(host)],
            Named(host, "ResumeHeroAction").IsVisible,
            library.IsVisible,
            (int)Math.Round(libraryOrigin.Y + library.Bounds.Height),
            libraryOrigin.Y + library.Bounds.Height <= logicalHeight,
            host.View.GetVisualDescendants()
                .OfType<Presentation.Home.RecommendationsRailView>()
                .Any(rail => rail.IsVisible));
    }

    private static async Task<HomeViewModel> CreateViewModelAsync(params HomeProgressEntry[] entries)
    {
        var viewModel = new HomeViewModel(
            new GetHome(new StubHomeReadModel(entries)),
            new NavigationService());
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        return viewModel;
    }

    private static async Task<HomeViewModel> CreateViewModelWithRailAsync(params HomeProgressEntry[] entries)
    {
        var viewModel = new HomeViewModel(
            new GetHome(new StubHomeReadModel(entries)),
            new NavigationService(),
            onResume: null,
            new RecommendationsViewModel(
                new GetRecommendations(new EmptyRecommendationReadModel()),
                new EnabledRecommendationSettings()));
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        return viewModel;
    }

    private static ViewHost Host(HomeViewModel viewModel, double width, double height, double scale)
    {
        var view = new HomeView { DataContext = viewModel };
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = view,
        };
        window.SetRenderScaling(scale);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
        return new ViewHost(window, view);
    }

    private static Control Named(ViewHost host, string name) =>
        host.View.GetVisualDescendants()
            .OfType<Control>()
            .SingleOrDefault(control => control.Name == name)
        ?? throw new InvalidOperationException($"Home does not declare a control named {name}.");

    private static string FocusedName(ViewHost host)
    {
        var focused = TopLevel.GetTopLevel(host.Window)?.FocusManager?.GetFocusedElement();
        return focused is Control control ? control.Name ?? string.Empty : string.Empty;
    }

    private static IEnumerable<string> FocusableNames(ViewHost host) =>
        host.View.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.Focusable && control.IsVisible && !string.IsNullOrEmpty(control.Name))
            .Select(control => control.Name!);

    private static void ApplyLanguage(string cultureName)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo(cultureName));
    }

    private static HomeProgressEntry MovieProgress() => Progress(Movie, "Arrival", true, Noon.AddHours(-2));

    private static HomeProgressEntry EpisodeProgress() => new(
        ContentKey.ForEpisode(Show, Episode),
        Show,
        CatalogTitleKind.Show,
        "Crónicas",
        SeasonNumber: 1,
        EpisodeNumber: 2,
        EpisodeTitle: "Ned",
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(50),
        WatchStatus.InProgress,
        IsAvailable: true,
        Noon);

    private static HomeProgressEntry Progress(
        TitleId titleId,
        string title,
        bool isAvailable,
        DateTimeOffset updatedUtc) => new(
        ContentKey.ForTitle(titleId),
        titleId,
        CatalogTitleKind.Movie,
        title,
        SeasonNumber: null,
        EpisodeNumber: null,
        EpisodeTitle: null,
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(90),
        WatchStatus.InProgress,
        isAvailable,
        updatedUtc);

    private static Guid CreateGuid(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new Guid(bytes);
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

    public sealed record LayoutRecord(
        string Id,
        int WidthPixels,
        int HeightPixels,
        double Scale,
        string Theme,
        string Language,
        double ViewportWidth,
        double ViewportHeight,
        string FirstFocus,
        IReadOnlyList<string> FocusOrder,
        bool ResumeHeroVisible,
        bool LibraryEntryVisible,
        int LibraryEntryBottom,
        bool LibraryEntryWithinFirstViewport,
        bool RecommendationsRailVisible);

    private sealed class ViewHost(Window window, Control view) : IDisposable
    {
        public Window Window { get; } = window;

        public Control View { get; } = view;

        public void Dispose() => Window.Close();
    }

    private sealed class EnabledRecommendationSettings : IRecommendationSettings
    {
        public bool IsEnabled { get; private set; } = true;

        public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;
    }

    private sealed class EmptyRecommendationReadModel : IRecommendationReadModel
    {
        public Task<RecommendationTaste> ReadTasteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(RecommendationTaste.Empty);
        }

        public Task<IReadOnlyList<RecommendationCandidate>> ReadCandidatesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<RecommendationCandidate>>([]);
        }
    }

    private sealed class StubHomeReadModel(HomeProgressEntry[] entries) : IHomeReadModel
    {
        public int ProgressReads { get; private set; }

        public IReadOnlyList<RecentlyAddedItem> RecentlyAdded { get; init; } = [];

        public LibrarySummary Summary { get; init; } = new(1, 1, 0);

        public Task<IReadOnlyList<HomeProgressEntry>> ReadProgressAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProgressReads++;
            return Task.FromResult<IReadOnlyList<HomeProgressEntry>>(entries);
        }

        public Task<IReadOnlyList<RecentlyAddedItem>> ReadRecentlyAddedAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(RecentlyAdded);
        }

        public Task<LibrarySummary> ReadLibrarySummaryAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Summary);
        }
    }
}
