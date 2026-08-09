using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Home;

public sealed class GetHomeTests
{
    private static readonly TitleId Movie = new(CreateGuid(1));
    private static readonly TitleId Show = new(CreateGuid(2));
    private static readonly EpisodeId Episode = new(CreateGuid(3));
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Empty_library_offers_nothing_to_resume_and_still_reports_a_summary()
    {
        var readModel = new StubHomeReadModel();

        var snapshot = await new GetHome(readModel).ExecuteAsync(
            new GetHomeQuery(),
            TestContext.Current.CancellationToken);

        Assert.Null(snapshot.Resume);
        Assert.Empty(snapshot.InProgress);
        Assert.Empty(snapshot.RecentlyAdded);
        Assert.Equal(new LibrarySummary(0, 0, 0), snapshot.Library);
        Assert.False(snapshot.HasResume);
    }

    [Fact]
    public async Task Most_recent_offerable_progress_becomes_the_resume_item()
    {
        var older = MovieProgress(TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(90), Noon.AddHours(-2));
        var newer = EpisodeProgress(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(50), Noon);
        var readModel = new StubHomeReadModel(older, newer);

        var snapshot = await new GetHome(readModel).ExecuteAsync(
            new GetHomeQuery(),
            TestContext.Current.CancellationToken);

        Assert.NotNull(snapshot.Resume);
        Assert.True(snapshot.HasResume);
        Assert.Equal(ContentKey.ForEpisode(Show, Episode), snapshot.Resume.Content);
        Assert.Equal(TimeSpan.FromMinutes(10), snapshot.Resume.Position);
        Assert.Equal(0.2, snapshot.Resume.CompletedFraction, 3);
        Assert.Equal(2, snapshot.InProgress.Count);
        Assert.Equal(
            [ContentKey.ForEpisode(Show, Episode), ContentKey.ForTitle(Movie)],
            snapshot.InProgress.Select(item => item.Content));
    }

    [Fact]
    public async Task Progress_below_the_minimum_resume_position_is_never_offered_as_a_hero()
    {
        // Twenty-nine seconds is inside the thirty-second floor the approved policy sets.
        var trivial = MovieProgress(TimeSpan.FromSeconds(29), TimeSpan.FromMinutes(90), Noon);
        var readModel = new StubHomeReadModel(trivial);

        var snapshot = await new GetHome(readModel).ExecuteAsync(
            new GetHomeQuery(),
            TestContext.Current.CancellationToken);

        Assert.Null(snapshot.Resume);
        Assert.False(snapshot.HasResume);
    }

    [Fact]
    public async Task An_unobserved_duration_still_resumes_and_reports_no_completed_fraction()
    {
        // A zero or negative length means unobserved, never "finishes immediately".
        var unobserved = MovieProgress(TimeSpan.FromMinutes(12), TimeSpan.Zero, Noon);
        var readModel = new StubHomeReadModel(unobserved);

        var snapshot = await new GetHome(readModel).ExecuteAsync(
            new GetHomeQuery(),
            TestContext.Current.CancellationToken);

        Assert.NotNull(snapshot.Resume);
        Assert.Equal(TimeSpan.FromMinutes(12), snapshot.Resume.Position);
        Assert.Equal(0d, snapshot.Resume.CompletedFraction);
    }

    [Fact]
    public async Task Unavailable_content_stays_in_the_rail_but_never_becomes_the_hero()
    {
        var missing = MovieProgress(
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(90),
            Noon,
            isAvailable: false);
        var present = EpisodeProgress(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(50), Noon.AddHours(-3));
        var readModel = new StubHomeReadModel(missing, present);

        var snapshot = await new GetHome(readModel).ExecuteAsync(
            new GetHomeQuery(),
            TestContext.Current.CancellationToken);

        Assert.NotNull(snapshot.Resume);
        Assert.Equal(ContentKey.ForEpisode(Show, Episode), snapshot.Resume.Content);
        Assert.Equal(2, snapshot.InProgress.Count);
        Assert.False(snapshot.InProgress.Single(item => item.Content == ContentKey.ForTitle(Movie)).IsAvailable);
    }

    [Fact]
    public async Task Nothing_available_leaves_the_hero_empty_while_the_rail_keeps_its_items()
    {
        var missing = MovieProgress(
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(90),
            Noon,
            isAvailable: false);
        var readModel = new StubHomeReadModel(missing);

        var snapshot = await new GetHome(readModel).ExecuteAsync(
            new GetHomeQuery(),
            TestContext.Current.CancellationToken);

        Assert.Null(snapshot.Resume);
        Assert.Single(snapshot.InProgress);
    }

    [Fact]
    public async Task A_movie_carries_no_episode_label_and_an_episode_carries_season_and_number()
    {
        var readModel = new StubHomeReadModel(
            MovieProgress(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(90), Noon.AddHours(-1)));
        var movieSnapshot = await new GetHome(readModel).ExecuteAsync(
            new GetHomeQuery(),
            TestContext.Current.CancellationToken);

        Assert.NotNull(movieSnapshot.Resume);
        Assert.Equal(CatalogTitleKind.Movie, movieSnapshot.Resume.Kind);
        Assert.Null(movieSnapshot.Resume.SeasonNumber);
        Assert.Null(movieSnapshot.Resume.EpisodeNumber);

        var episodeSnapshot = await new GetHome(
                new StubHomeReadModel(EpisodeProgress(
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromMinutes(50),
                    Noon)))
            .ExecuteAsync(new GetHomeQuery(), TestContext.Current.CancellationToken);

        Assert.NotNull(episodeSnapshot.Resume);
        Assert.Equal(CatalogTitleKind.Show, episodeSnapshot.Resume.Kind);
        Assert.Equal(1, episodeSnapshot.Resume.SeasonNumber);
        Assert.Equal(2, episodeSnapshot.Resume.EpisodeNumber);
        Assert.Equal("Ned", episodeSnapshot.Resume.EpisodeTitle);
    }

    [Fact]
    public async Task Watched_and_unstarted_entries_never_reach_the_rail()
    {
        var watched = MovieProgress(
            TimeSpan.FromMinutes(89),
            TimeSpan.FromMinutes(90),
            Noon,
            status: WatchStatus.Watched);
        var unstarted = EpisodeProgress(
            TimeSpan.Zero,
            TimeSpan.FromMinutes(50),
            Noon.AddHours(-1),
            status: WatchStatus.NotStarted);
        var readModel = new StubHomeReadModel(watched, unstarted);

        var snapshot = await new GetHome(readModel).ExecuteAsync(
            new GetHomeQuery(),
            TestContext.Current.CancellationToken);

        Assert.Null(snapshot.Resume);
        Assert.Empty(snapshot.InProgress);
    }

    [Fact]
    public async Task The_query_limits_reach_the_read_model_instead_of_loading_the_catalog()
    {
        var readModel = new StubHomeReadModel();

        await new GetHome(readModel).ExecuteAsync(
            new GetHomeQuery(InProgressLimit: 7, RecentlyAddedLimit: 4),
            TestContext.Current.CancellationToken);

        Assert.Equal(7, readModel.RequestedProgressLimit);
        Assert.Equal(4, readModel.RequestedRecentlyAddedLimit);
    }

    [Fact]
    public async Task Recently_added_and_the_summary_pass_through_untouched()
    {
        var added = new RecentlyAddedItem(
            Movie,
            CatalogTitleKind.Movie,
            "Arrival",
            2016,
            IsAvailable: true,
            Noon);
        var readModel = new StubHomeReadModel { RecentlyAdded = [added], Summary = new LibrarySummary(4, 2, 1) };

        var snapshot = await new GetHome(readModel).ExecuteAsync(
            new GetHomeQuery(),
            TestContext.Current.CancellationToken);

        Assert.Equal([added], snapshot.RecentlyAdded);
        Assert.Equal(new LibrarySummary(4, 2, 1), snapshot.Library);
    }

    [Fact]
    public void The_use_case_rejects_a_missing_read_model_and_an_invalid_query()
    {
        Assert.Throws<ArgumentNullException>(() => new GetHome(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GetHomeQuery(InProgressLimit: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GetHomeQuery(RecentlyAddedLimit: 0));
    }

    private static HomeProgressEntry MovieProgress(
        TimeSpan position,
        TimeSpan duration,
        DateTimeOffset updatedUtc,
        bool isAvailable = true,
        WatchStatus status = WatchStatus.InProgress) => new(
        ContentKey.ForTitle(Movie),
        Movie,
        CatalogTitleKind.Movie,
        "Arrival",
        SeasonNumber: null,
        EpisodeNumber: null,
        EpisodeTitle: null,
        position,
        duration,
        status,
        isAvailable,
        updatedUtc);

    private static HomeProgressEntry EpisodeProgress(
        TimeSpan position,
        TimeSpan duration,
        DateTimeOffset updatedUtc,
        bool isAvailable = true,
        WatchStatus status = WatchStatus.InProgress) => new(
        ContentKey.ForEpisode(Show, Episode),
        Show,
        CatalogTitleKind.Show,
        "Crónicas",
        SeasonNumber: 1,
        EpisodeNumber: 2,
        EpisodeTitle: "Ned",
        position,
        duration,
        status,
        isAvailable,
        updatedUtc);

    private static Guid CreateGuid(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private sealed class StubHomeReadModel(params HomeProgressEntry[] entries) : IHomeReadModel
    {
        public int RequestedProgressLimit { get; private set; }

        public int RequestedRecentlyAddedLimit { get; private set; }

        public IReadOnlyList<RecentlyAddedItem> RecentlyAdded { get; init; } = [];

        public LibrarySummary Summary { get; init; } = new(0, 0, 0);

        public Task<IReadOnlyList<HomeProgressEntry>> ReadProgressAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedProgressLimit = limit;
            return Task.FromResult<IReadOnlyList<HomeProgressEntry>>(entries);
        }

        public Task<IReadOnlyList<RecentlyAddedItem>> ReadRecentlyAddedAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedRecentlyAddedLimit = limit;
            return Task.FromResult(RecentlyAdded);
        }

        public Task<LibrarySummary> ReadLibrarySummaryAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Summary);
        }
    }
}
