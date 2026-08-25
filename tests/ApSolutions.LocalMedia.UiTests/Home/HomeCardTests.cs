// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Navigation;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Home;

/// <summary>
/// The lines and the buttons Home's cards gained with the prototype's front page.
/// </summary>
/// <remarks>
/// The hero's meta line drops each piece the catalogue cannot answer — a title with no year, no
/// genre and no known length has to leave no separators behind — and the rail's cards carry two
/// buttons each, named by what they do AND by what they act on. Both are branchy in ways a layout
/// test cannot see: a screenshot of one card proves nothing about the card whose year is missing.
/// </remarks>
public sealed class HomeCardTests
{
    private static readonly TitleId Show = new(Guid.Parse("d1000000-0000-4000-8000-000000000001"));
    private static readonly TitleId Movie = new(Guid.Parse("d1000000-0000-4000-8000-000000000002"));
    private static readonly EpisodeId Episode = new(Guid.Parse("d2000000-0000-4000-8000-000000000001"));
    private static readonly DateTimeOffset Noon = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A film with a year, two genres and a known length writes all three; the same film with none
    /// of them writes nothing at all rather than a line of separators.
    /// </summary>
    [AvaloniaFact]
    public async Task The_hero_line_drops_every_piece_the_catalogue_cannot_answer()
    {
        var rich = await LoadAsync(FilmProgress(
            year: 2019,
            genres: ["Drama", "Misterio", "Suspense"],
            duration: TimeSpan.FromMinutes(113)));
        Assert.True(rich.HasResumeMeta);

        // Two genres and not three: the line is a summary, and the prototype writes two.
        Assert.StartsWith("2019 · Drama · Misterio", rich.ResumeMetaText, StringComparison.Ordinal);
        Assert.DoesNotContain("Suspense", rich.ResumeMetaText, StringComparison.Ordinal);

        var bare = await LoadAsync(FilmProgress(year: null, genres: null, duration: null));
        Assert.Equal(string.Empty, bare.ResumeMetaText);
        Assert.False(bare.HasResumeMeta);

        // A length already spent says nothing either: "left -00:04:00" is not a sentence.
        var overrun = await LoadAsync(FilmProgress(
            year: null,
            genres: null,
            duration: TimeSpan.FromMinutes(10),
            position: TimeSpan.FromMinutes(14)));
        Assert.Equal(string.Empty, overrun.ResumeMetaText);

        // And a series puts its season and episode where a film puts its year.
        var series = await LoadAsync(EpisodeProgress());
        Assert.StartsWith("T1 · E2 · Ned", series.ResumeMetaText, StringComparison.Ordinal);

        // And the same line without what is left, which is what the player's header writes under the
        // title: the prototype puts «2024 · Suspense» there, and a film opened from the hero used to
        // reach the player with a title and nothing under it — the subtitle it was handed only ever
        // had a value for an episode.
        Assert.Equal("2019 · Drama · Misterio", rich.ResumePlayerSubtitle);
        Assert.StartsWith(rich.ResumePlayerSubtitle, rich.ResumeMetaText, StringComparison.Ordinal);
        Assert.NotEqual(rich.ResumePlayerSubtitle, rich.ResumeMetaText);
        Assert.Equal("T1 · E2 · Ned", series.ResumePlayerSubtitle);
        Assert.Equal(string.Empty, bare.ResumePlayerSubtitle);

        // The one that says nothing about itself but does know how far in it is: the hero's line is
        // what is left and no more, and the player's header stays empty rather than printing it.
        Assert.Equal(string.Empty, overrun.ResumePlayerSubtitle);
        var timed = await LoadAsync(FilmProgress(
            year: null,
            genres: null,
            duration: TimeSpan.FromMinutes(90),
            position: TimeSpan.FromMinutes(30)));
        Assert.Equal(string.Empty, timed.ResumePlayerSubtitle);
        Assert.NotEqual(string.Empty, timed.ResumeMetaText);
    }

    /// <summary>
    /// The hero's second button reaches the host with the title it offers, and does nothing at all
    /// when there is nothing to offer or nobody to hand it to.
    /// </summary>
    [AvaloniaFact]
    public async Task Details_on_the_hero_reaches_the_host_and_is_silent_without_one()
    {
        var opened = new List<TitleId>();
        var viewModel = new HomeViewModel(
            new GetHome(new StubHome([FilmProgress(2019, ["Drama"], TimeSpan.FromMinutes(113))])),
            new NavigationService(),
            onResume: null,
            recommendations: null,
            onOpenDetails: titleId =>
            {
                opened.Add(titleId);
                return Task.CompletedTask;
            });
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.OpenResumeDetailsCommand.CanExecute(null));
        viewModel.OpenResumeDetailsCommand.Execute(null);
        Assert.Equal([Movie], opened);

        // No hook: the button is not offered rather than offered and inert, which is this
        // repository's characteristic defect wearing a command.
        var unwired = await LoadAsync(FilmProgress(2019, ["Drama"], TimeSpan.FromMinutes(113)));
        unwired.OpenResumeDetailsCommand.Execute(null);
        Assert.Single(opened);

        // And nothing to open: the snapshot has no resume, so there is no card to reach.
        var empty = new HomeViewModel(
            new GetHome(new StubHome([])),
            new NavigationService(),
            onResume: null,
            recommendations: null,
            onOpenDetails: titleId =>
            {
                opened.Add(titleId);
                return Task.CompletedTask;
            });
        await empty.LoadAsync(TestContext.Current.CancellationToken);
        empty.OpenResumeDetailsCommand.Execute(null);
        Assert.Single(opened);
    }

    /// <summary>
    /// Each card of the continue rail carries the hero's two actions, and both of them act on the
    /// card they sit under rather than on whatever the rail happens to have selected.
    /// </summary>
    [AvaloniaFact]
    public async Task A_rail_card_resumes_and_opens_the_title_it_names()
    {
        var resumed = new List<ContentKey>();
        var opened = new List<TitleId>();
        var viewModel = new HomeViewModel(
            new GetHome(new StubHome([EpisodeProgress(), FilmProgress(2019, null, TimeSpan.FromMinutes(113))])),
            new NavigationService(),
            request =>
            {
                resumed.Add(request.Content);
                return Task.CompletedTask;
            },
            recommendations: null,
            onOpenDetails: titleId =>
            {
                opened.Add(titleId);
                return Task.CompletedTask;
            });
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        var film = Assert.Single(viewModel.InProgress, card => !card.IsShow);
        Assert.True(viewModel.ResumeItemCommand.CanExecute(film));
        viewModel.ResumeItemCommand.Execute(film);
        Assert.Equal([ContentKey.ForTitle(Movie)], resumed);

        viewModel.OpenItemDetailsCommand.Execute(film);
        Assert.Equal([Movie], opened);

        // Neither command answers to anything that is not a card, and neither answers for a card
        // whose file is not there: a rail that offered to play an unplugged drive would be lying.
        Assert.False(viewModel.ResumeItemCommand.CanExecute(null));
        Assert.False(viewModel.OpenItemDetailsCommand.CanExecute("not a card"));
        viewModel.ResumeItemCommand.Execute(null);
        viewModel.OpenItemDetailsCommand.Execute(null);
        Assert.Single(resumed);
        Assert.Single(opened);
    }

    /// <summary>
    /// What one card of the rail says: its line, its two announced names, the kind it is and the
    /// state every card on this rail is by definition in.
    /// </summary>
    [AvaloniaFact]
    public async Task A_rail_card_names_its_two_buttons_by_the_title_they_act_on()
    {
        var viewModel = await LoadAsync(EpisodeProgress(), FilmProgress(2019, null, TimeSpan.FromMinutes(113)));

        var episode = Assert.Single(viewModel.InProgress, card => card.IsShow);
        Assert.Equal("T1 · E2 · Ned", episode.RailSubtitle);
        Assert.Equal("T1 · E2", episode.MetaText);
        Assert.True(episode.HasMeta);
        Assert.True(episode.HasCaption);
        Assert.Equal("CatalogKindShow", episode.KindKey);
        Assert.True(episode.HasKind);
        Assert.Equal("WatchStatusInProgress", episode.StatusKey);
        Assert.False(episode.CountsEpisodes);
        Assert.False(episode.IsWatched);
        Assert.Equal(string.Empty, episode.EpisodeCountText);
        Assert.True(episode.HasKnownProgress);
        Assert.Contains(episode.Title, episode.ResumeAccessibleName, StringComparison.Ordinal);
        Assert.Contains(episode.Title, episode.DetailsAccessibleName, StringComparison.Ordinal);
        Assert.NotEqual(episode.ResumeAccessibleName, episode.DetailsAccessibleName);

        // A film says what kind it is instead of an episode, because there is no episode to say.
        var film = Assert.Single(viewModel.InProgress, card => !card.IsShow);
        Assert.Equal("CatalogKindMovie", film.KindKey);
        Assert.Equal(string.Empty, film.MetaText);
        Assert.False(film.HasMeta);
        Assert.False(film.HasCaption);
        Assert.EndsWith("2019", film.RailSubtitle, StringComparison.Ordinal);
        Assert.NotEqual(film.ResumeAccessibleName, episode.ResumeAccessibleName);
    }

    /// <summary>A film with no year at all says only what kind it is, with no separator after it.</summary>
    [AvaloniaFact]
    public async Task A_film_with_no_year_leaves_no_separator_behind()
    {
        var viewModel = await LoadAsync(FilmProgress(year: null, genres: null, duration: TimeSpan.FromMinutes(113)));

        var film = Assert.Single(viewModel.InProgress);
        Assert.DoesNotContain("·", film.RailSubtitle, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, film.RailSubtitle);
    }

    [AvaloniaFact]
    public void A_card_built_over_nothing_refuses_to_be_built()
    {
        Assert.Throws<ArgumentNullException>(() => new InProgressItemViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new RecentlyAddedItemViewModel(null!));
    }

    /// <summary>
    /// A recently added card says which title it is, twice: once as its own id and once as the id
    /// whatever opens a card asks for.
    /// </summary>
    /// <remarks>
    /// Two names for one value is not duplication here — <c>Id</c> is the card's own and
    /// <c>TitleId</c> is what <c>IRailCard</c> promises — and nothing read either of them, which is
    /// how a rail ends up opening the card of the title beside the one that was clicked.
    /// </remarks>
    [AvaloniaFact]
    public async Task A_recently_added_card_carries_the_title_it_would_open()
    {
        var titleId = new TitleId(Guid.Parse("d1000000-0000-4000-8000-000000000003"));
        var viewModel = new HomeViewModel(
            new GetHome(new StubHome([])
            {
                RecentlyAdded =
                [
                    new RecentlyAddedItem(titleId, CatalogTitleKind.Movie, "Arrival", 2016, true, Noon),
                ],
            }),
            new NavigationService());
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        var card = Assert.Single(viewModel.RecentlyAdded);
        Assert.Equal(titleId, card.Id);
        Assert.Equal(titleId, card.TitleId);
        Assert.Equal("Arrival", card.Title);
    }

    /// <summary>
    /// Setting a list to what it already is announces nothing, which is the half of every setter
    /// that only runs when a reload finds the library unchanged.
    /// </summary>
    [AvaloniaFact]
    public async Task Loading_the_same_thing_twice_announces_nothing_the_second_time()
    {
        var viewModel = await LoadAsync(EpisodeProgress());

        var announced = new List<string>();
        viewModel.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

        var same = typeof(HomeViewModel).GetProperty(nameof(viewModel.InProgress));
        Assert.NotNull(same);
        same!.SetValue(viewModel, viewModel.InProgress);

        Assert.Empty(announced);
    }

    private static async Task<HomeViewModel> LoadAsync(params HomeProgressEntry[] entries)
    {
        var viewModel = new HomeViewModel(new GetHome(new StubHome(entries)), new NavigationService());
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        return viewModel;
    }

    private static HomeProgressEntry FilmProgress(
        int? year,
        IReadOnlyList<string>? genres,
        TimeSpan? duration,
        TimeSpan? position = null) => new(
        ContentKey.ForTitle(Movie),
        Movie,
        CatalogTitleKind.Movie,
        "Arrival",
        SeasonNumber: null,
        EpisodeNumber: null,
        EpisodeTitle: null,
        position ?? TimeSpan.FromMinutes(40),
        duration,
        WatchStatus.InProgress,
        IsAvailable: true,
        Noon,
        year,
        genres);

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
        Noon.AddHours(1));

    private sealed class StubHome(IReadOnlyList<HomeProgressEntry> entries) : IHomeReadModel
    {
        public IReadOnlyList<RecentlyAddedItem> RecentlyAdded { get; init; } = [];

        public Task<IReadOnlyList<HomeProgressEntry>> ReadProgressAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<HomeProgressEntry>>([.. entries.Take(limit)]);

        public Task<IReadOnlyList<RecentlyAddedItem>> ReadRecentlyAddedAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RecentlyAddedItem>>([.. RecentlyAdded.Take(limit)]);

        public Task<LibrarySummary> ReadLibrarySummaryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LibrarySummary(0, 0, 0));
    }
}
