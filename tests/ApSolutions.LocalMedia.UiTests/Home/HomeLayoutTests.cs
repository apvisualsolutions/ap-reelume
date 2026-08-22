// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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
using ApSolutions.LocalMedia.TestSupport;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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

    /// <summary>
    /// Home's shape across every window, scale, theme and language, against an approved record.
    /// </summary>
    /// <remarks>
    /// <b>What the 2026-08-22 approval changed, and why.</b> The hero grew 122 px when it became the
    /// one §4 describes, so <c>LibraryEntryBottom</c> moves in all thirty-six records, and in six of
    /// them — 1366x768 at 200%, a viewport 384 logical px tall — the library block stops fitting in
    /// the first screenful. That is a real loss and it is approved rather than hidden: the
    /// prototype's own hero is <b>398 px tall</b>, so nothing that looks like the approved design
    /// fits above a 384 px fold, and since the same day Home scrolls, which is what makes everything
    /// under it reachable at all. The record says so; a green here would not.
    /// </remarks>
    [AvaloniaFact]
    public async Task Home_matches_its_approved_structural_baseline_across_every_combination()
    {
        var actual = new List<LayoutRecord>();
        var captures = Path.Combine(RepositoryLayout.Root, "artifacts", "ui-captures", "T30");
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
            RepositoryLayout.Root,
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
            RepositoryLayout.Root,
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
        Assert.False(film.InProgress[0].HasCaption);
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
        Assert.True(card.HasCaption);
        Assert.Contains("1", card.CaptionText, StringComparison.Ordinal);
        Assert.Contains("2", card.CaptionText, StringComparison.Ordinal);
        Assert.Equal("20", card.CompletedText);
        Assert.Equal(ContentKey.ForEpisode(Show, Episode), card.Content);
        Assert.Equal(Show, card.TitleId);
    }

    /// <summary>
    /// What Home reads about recently added titles reaches the screen.
    /// </summary>
    /// <remarks>
    /// Every layer under this one was already built and already tested: SQLite orders by date added,
    /// <c>GetHome</c> carries twelve of them, and <c>RecentlyAddedItemViewModel</c> formats the year.
    /// No view painted any of it, which is the house defect in its sixth form — produced everywhere
    /// and consumed nowhere — with the aggravation that the tests above pass, so it read as covered.
    /// Asserted on visible text rather than on the view model, because the view model was never the
    /// half that was missing.
    /// </remarks>
    [AvaloniaFact]
    public async Task Home_paints_the_recently_added_titles_it_already_reads()
    {
        ApplyLanguage("es-ES");
        var viewModel = await CreateViewModelWithRecentAsync(
            new RecentlyAddedItem(Movie, CatalogTitleKind.Movie, "Arrival", 2016, true, Noon),
            new RecentlyAddedItem(Show, CatalogTitleKind.Show, "Crónicas", null, false, Noon));
        using var host = Host(viewModel, 1366, 768, 1.0);

        var texts = host.View.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Text ?? string.Empty)
            .ToArray();

        Assert.Contains("Arrival", texts);
        Assert.Contains("2016", texts);
        Assert.Contains("Crónicas", texts);
    }

    /// <summary>
    /// A recently added card gives the title at most two lines and sets the year apart from it.
    /// </summary>
    /// <remarks>
    /// The year is asserted to be a <b>different</b> colour from the title as well as the secondary
    /// one: a card where both are the same brush would satisfy "the year is TextSecondaryBrush" the
    /// day somebody made the title secondary too, and the point of the rule is the contrast between
    /// the two, not the name of one token.
    /// </remarks>
    [AvaloniaFact]
    public async Task A_recently_added_card_holds_the_title_to_two_lines_and_sets_the_year_apart()
    {
        ApplyLanguage("es-ES");
        var viewModel = await CreateViewModelWithRecentAsync(
            new RecentlyAddedItem(Movie, CatalogTitleKind.Movie, "Arrival", 2016, true, Noon));
        using var host = Host(viewModel, 1366, 768, 1.0);

        var rail = Assert.Single(host.View.GetVisualDescendants().OfType<RecentlyAddedRailView>());
        var blocks = rail.GetVisualDescendants().OfType<TextBlock>().ToArray();
        var title = Assert.Single(blocks, block => block.Text == "Arrival");
        var year = Assert.Single(blocks, block => block.Text == "2016");

        Assert.Equal(2, title.MaxLines);
        Assert.Equal(TextWrapping.Wrap, title.TextWrapping);
        Assert.Equal(
            ThemeColour("TextSecondaryBrush"),
            Assert.IsAssignableFrom<ISolidColorBrush>(year.Foreground).Color);
        Assert.NotEqual(
            Assert.IsAssignableFrom<ISolidColorBrush>(title.Foreground).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(year.Foreground).Color);
    }

    /// <summary>
    /// Continue is the only solid accent on Home, and when there is nothing to continue the hero is
    /// absent rather than empty.
    /// </summary>
    /// <remarks>
    /// <c>LeadingActionTests</c> decides this one view at a time, which cannot see that Home mounts
    /// four of them at once: four views each leading with nothing wrong would still pass there and
    /// put four accents on one screen. This asks the assembled screen. The second half is the same
    /// distinction the rest of the tree draws — absent leaves no gap, disabled does — measured as
    /// zero height rather than as a false <c>IsVisible</c>, because a collapsed panel and a panel
    /// that still reserves its row read identically from the binding.
    /// </remarks>
    [AvaloniaFact]
    public async Task Continue_is_the_only_solid_accent_and_its_absence_leaves_no_gap()
    {
        ApplyLanguage("es-ES");
        var withProgress = await CreateViewModelWithRailAsync(EpisodeProgress());
        using (var host = Host(withProgress, 1366, 768, 1.0))
        {
            var accented = host.View.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control.Classes.Contains("primary-action"))
                .ToArray();
            Assert.Equal("ResumeHeroAction", Assert.Single(accented).Name);
        }

        var withoutProgress = await CreateViewModelWithRailAsync();
        using (var host = Host(withoutProgress, 1366, 768, 1.0))
        {
            Assert.DoesNotContain(
                host.View.GetVisualDescendants().OfType<Control>(),
                control => control.Classes.Contains("primary-action"));

            var hero = Assert.Single(host.View.GetVisualDescendants().OfType<ResumeHeroView>());
            Assert.False(hero.IsVisible);
            Assert.Equal(0, hero.Bounds.Height);
        }
    }

    /// <summary>
    /// The in-progress card carries its progress as a 3 px bar at its foot, in the accent over the
    /// control fill.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read from the elements that <b>paint</b> rather than from the control's own properties: a
    /// <c>ProgressBar</c> takes <c>Foreground</c> and <c>Background</c> and its <c>ControlTheme</c>
    /// decides whether either reaches the screen, which is the house defect wearing a setter.
    /// </para>
    /// <para>
    /// Its <b>position</b> is asserted and not merely its existence, because a rule about where
    /// something sits passes either way if all it checks is that the thing is there. §4 puts the bar
    /// "at the foot of each poster", so what is asserted is that it is inside the artwork, aligned to
    /// its bottom, and as wide as the artwork is: under the card it would be a fourth line of text
    /// rather than a rule across the picture. It used to be the last child of the card, which is
    /// where it lived before <c>PosterCardView</c> existed.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public async Task The_in_progress_card_ends_with_a_three_pixel_bar_in_the_accent()
    {
        ApplyLanguage("es-ES");
        var viewModel = await CreateViewModelAsync(EpisodeProgress());
        using var host = Host(viewModel, 1366, 768, 1.0);

        var rail = Assert.Single(host.View.GetVisualDescendants().OfType<InProgressRailView>());
        var bar = Assert.Single(rail.GetVisualDescendants().OfType<ProgressBar>());
        Assert.Equal(3, bar.Bounds.Height);

        var painted = bar.GetVisualDescendants()
            .OfType<Border>()
            .Select(border => border.Background)
            .OfType<ISolidColorBrush>()
            .Select(brush => brush.Color)
            .ToArray();
        Assert.Contains(ThemeColour("AccentBrush"), painted);
        Assert.Contains(ThemeColour("ControlFillBrush"), painted);

        var artwork = Assert.Single(
            bar.GetVisualAncestors().OfType<Border>(),
            border => border.Background is ISolidColorBrush fill
                && fill.Color == ThemeColour("ControlFillBrush"));
        Assert.Equal(VerticalAlignment.Bottom, bar.VerticalAlignment);
        Assert.Equal(artwork.Bounds.Width - artwork.BorderThickness.Left * 2, bar.Bounds.Width);
        var barBottom = bar.TranslatePoint(new Point(0, bar.Bounds.Height), artwork);
        Assert.NotNull(barBottom);
        Assert.Equal(artwork.Bounds.Height - artwork.BorderThickness.Bottom, barBottom!.Value.Y);
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
        Assert.True(viewModel.RecentlyAdded[0].HasCaption);
        Assert.Equal("2016", viewModel.RecentlyAdded[0].CaptionText);
        Assert.True(viewModel.RecentlyAdded[0].IsAvailable);
        Assert.False(viewModel.RecentlyAdded[1].HasCaption);
        Assert.Equal(string.Empty, viewModel.RecentlyAdded[1].CaptionText);
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
            RepositoryLayout.Root,
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

    private static async Task<HomeViewModel> CreateViewModelWithRecentAsync(params RecentlyAddedItem[] recent)
    {
        var viewModel = new HomeViewModel(
            new GetHome(new StubHomeReadModel([]) { RecentlyAdded = recent }),
            new NavigationService());
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

    /// <summary>
    /// A theme brush's colour. Asked for by variant, because the four dictionaries each declare their
    /// own and <c>TryFindResource</c> over the application answers null for all of them.
    /// </summary>
    private static Color ThemeColour(string key)
    {
        var application = Avalonia.Application.Current;
        Assert.NotNull(application);
        Assert.True(
            application.TryGetResource(key, application.ActualThemeVariant, out var value),
            $"{key} is not declared in this theme variant.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

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
