// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Catalog;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Show;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Details;

public sealed class DetailsNavigationTests
{
    private static readonly TitleId MovieId = new(CreateGuid(21));
    private static readonly TitleId ShowId = new(CreateGuid(22));
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_movie_details_view_model_states_availability_watch_state_and_resume_point()
    {
        var viewModel = new MovieDetailsViewModel();

        viewModel.Apply(
            Item(MovieId, CatalogTitleKind.Movie, "Arrival", isAvailable: true),
            State(ContentKey.ForTitle(MovieId), TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(116)),
            versions: null);

        Assert.Equal("Arrival", viewModel.Title);
        Assert.Equal(2016, viewModel.Year);
        Assert.True(viewModel.IsAvailable);
        Assert.Equal(WatchStatus.InProgress, viewModel.WatchStatus.Status);
        Assert.True(viewModel.CanResume);
        Assert.Equal(TimeSpan.FromMinutes(40), viewModel.ResumePosition);
        Assert.False(viewModel.HasVersions);
        Assert.Empty(viewModel.Versions);
    }

    [Fact]
    public void Trivial_progress_and_unavailable_content_never_offer_a_resume_action()
    {
        var trivial = new MovieDetailsViewModel();
        trivial.Apply(
            Item(MovieId, CatalogTitleKind.Movie, "Arrival", isAvailable: true),
            State(ContentKey.ForTitle(MovieId), TimeSpan.FromSeconds(29), TimeSpan.FromMinutes(116)),
            versions: null);
        Assert.False(trivial.CanResume);

        var unavailable = new MovieDetailsViewModel();
        unavailable.Apply(
            Item(MovieId, CatalogTitleKind.Movie, "Arrival", isAvailable: false),
            State(ContentKey.ForTitle(MovieId), TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(116)),
            versions: null);
        Assert.False(unavailable.CanResume);
        Assert.False(unavailable.CanPlay);
    }

    [Fact]
    public void Every_version_is_listed_and_the_effective_one_is_marked_without_hiding_any()
    {
        var preferred = new MediaFileId(CreateGuid(31));
        var other = new MediaFileId(CreateGuid(32));
        var offline = new MediaFileId(CreateGuid(33));
        var group = new MediaVersionGroup(
            new MediaVersionId(CreateGuid(34)),
            ContentKey.ForTitle(MovieId).Value,
            [
                new MediaVersion(preferred, @"root\a.mkv", true, TimeSpan.FromMinutes(116), 3840, 2160, true, "HEVC", 90),
                new MediaVersion(other, @"root\b.mkv", true, TimeSpan.FromMinutes(116), 1920, 1080, false, "H264", 40),
                new MediaVersion(offline, @"root\c.mkv", false, TimeSpan.FromMinutes(116), 1920, 1080, false, "H264", 30),
            ],
            preferred);
        var viewModel = new MovieDetailsViewModel();

        viewModel.Apply(
            Item(MovieId, CatalogTitleKind.Movie, "Arrival", isAvailable: true),
            watchState: null,
            group);

        Assert.True(viewModel.HasVersions);
        Assert.Equal(3, viewModel.Versions.Count);
        Assert.Single(viewModel.Versions, version => version.IsEffective);
        Assert.Equal(preferred, viewModel.Versions.Single(version => version.IsEffective).MediaFileId);
        Assert.Single(viewModel.Versions, version => !version.IsAvailable);
        Assert.All(viewModel.Versions, version => Assert.False(string.IsNullOrWhiteSpace(version.QualityLabel)));
    }

    [Fact]
    public void A_show_groups_episodes_by_season_with_specials_last_and_states_playability()
    {
        var viewModel = new ShowDetailsViewModel();
        var episodes = new[]
        {
            EpisodeEntry(101, season: 2, number: 1, isAvailable: true, hasFile: true),
            EpisodeEntry(102, season: 1, number: 2, isAvailable: true, hasFile: true),
            EpisodeEntry(103, season: 1, number: 1, isAvailable: true, hasFile: true),
            EpisodeEntry(104, season: 0, number: 1, isAvailable: true, hasFile: true),
            EpisodeEntry(105, season: 1, number: 3, isAvailable: false, hasFile: true),
            EpisodeEntry(106, season: 1, number: 4, isAvailable: true, hasFile: false),
        };

        viewModel.Apply(
            Item(ShowId, CatalogTitleKind.Show, "Crónicas", isAvailable: true),
            episodes,
            new Dictionary<ContentKey, WatchState>
            {
                [ContentKey.ForEpisode(ShowId, episodes[2].Id)] = State(
                    ContentKey.ForEpisode(ShowId, episodes[2].Id),
                    TimeSpan.FromMinutes(45),
                    TimeSpan.FromMinutes(50),
                    WatchStatus.Watched),
            });

        Assert.Equal([1, 2, 0], viewModel.Seasons.Select(season => season.SeasonNumber));
        Assert.True(viewModel.Seasons[^1].IsSpecials);
        Assert.False(viewModel.Seasons[0].IsSpecials);

        var firstSeason = viewModel.Seasons[0];
        Assert.Equal([1, 2, 3, 4], firstSeason.Episodes.Select(episode => episode.EpisodeNumber));
        Assert.Equal(WatchStatus.Watched, firstSeason.Episodes[0].WatchStatus);
        Assert.Equal(WatchStatus.NotStarted, firstSeason.Episodes[1].WatchStatus);
        Assert.False(firstSeason.Episodes[2].IsAvailable);
        Assert.False(firstSeason.Episodes[2].IsPlayable);
        Assert.False(firstSeason.Episodes[3].IsPlayable);
        Assert.True(firstSeason.Episodes[0].IsPlayable);
    }

    [Fact]
    public void A_show_with_no_episodes_reports_it_instead_of_rendering_an_empty_list()
    {
        var viewModel = new ShowDetailsViewModel();

        viewModel.Apply(
            Item(ShowId, CatalogTitleKind.Show, "Crónicas", isAvailable: true),
            [],
            new Dictionary<ContentKey, WatchState>());

        Assert.Empty(viewModel.Seasons);
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public async Task Opening_details_loads_them_through_the_hook_and_back_restores_the_browse_state()
    {
        var loaded = new List<TitleId>();
        var queryService = new SingleQueryService(new CatalogPage(
            [
                Item(MovieId, CatalogTitleKind.Movie, "Arrival", true),
                Item(ShowId, CatalogTitleKind.Show, "Crónicas", true),
            ],
            null));
        var viewModel = new LibraryViewModel(queryService)
        {
            Search = "ciencia",
            Filters = CatalogFilter.Available,
            Sort = CatalogSort.Year,
        };
        viewModel.DetailsLoader = item =>
        {
            loaded.Add(item.Item.Id);
            return Task.CompletedTask;
        };

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        await viewModel.OpenDetailsAsync(viewModel.Items[1], TestContext.Current.CancellationToken);

        Assert.Equal([ShowId], loaded);
        Assert.Equal(LibrarySurface.ShowDetails, viewModel.Surface);

        viewModel.BackToLibrary();
        Assert.Equal(LibrarySurface.Browse, viewModel.Surface);
        Assert.Equal(ShowId, viewModel.ScrollAnchorId);
        Assert.Equal("ciencia", viewModel.Search);
        Assert.Equal(CatalogFilter.Available, viewModel.Filters);
        Assert.Equal(CatalogSort.Year, viewModel.Sort);
        Assert.Equal(["Arrival", "Crónicas"], viewModel.Items.Select(item => item.Title));
    }

    [Fact]
    public void Play_and_resume_reach_the_host_with_the_effective_version_and_the_right_position()
    {
        var requests = new List<PlayDetailsRequest>();
        var effective = new MediaFileId(CreateGuid(51));
        var viewModel = new MovieDetailsViewModel(request =>
        {
            requests.Add(request);
            return Task.CompletedTask;
        });
        viewModel.Apply(
            Item(MovieId, CatalogTitleKind.Movie, "Arrival", isAvailable: true),
            State(ContentKey.ForTitle(MovieId), TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(116)),
            new MediaVersionGroup(
                new MediaVersionId(CreateGuid(52)),
                ContentKey.ForTitle(MovieId).Value,
                [new MediaVersion(effective, @"root\a.mkv", true, TimeSpan.FromMinutes(116), 1920, 1080, false, "H264", 40)],
                null));

        Assert.True(viewModel.PlayCommand.CanExecute(null));
        viewModel.PlayCommand.Execute(null);
        Assert.True(viewModel.ResumeCommand.CanExecute(null));
        viewModel.ResumeCommand.Execute(null);

        Assert.Equal(2, requests.Count);
        Assert.Equal(new PlayDetailsRequest(effective, TimeSpan.Zero), requests[0]);
        Assert.Equal(new PlayDetailsRequest(effective, TimeSpan.FromMinutes(40)), requests[1]);
        Assert.Equal("40:00", viewModel.ResumePositionText);
    }

    [Fact]
    public void A_resume_point_past_an_hour_is_written_with_hours_and_a_position_is_clamped()
    {
        var viewModel = new MovieDetailsViewModel();
        viewModel.Apply(
            Item(MovieId, CatalogTitleKind.Movie, "Arrival", isAvailable: true),
            State(ContentKey.ForTitle(MovieId), TimeSpan.FromMinutes(200), TimeSpan.FromMinutes(116)),
            versions: null);

        Assert.Equal(TimeSpan.FromMinutes(116), viewModel.ResumePosition);
        Assert.Equal("1:56:00", viewModel.ResumePositionText);
    }

    [Fact]
    public void Details_without_a_host_hook_stay_silent_and_reject_a_missing_item()
    {
        var movie = new MovieDetailsViewModel();
        movie.Apply(Item(MovieId, CatalogTitleKind.Movie, "Arrival", true), null, null);
        movie.PlayCommand.Execute(null);
        movie.ResumeCommand.Execute(null);

        Assert.Equal(ApSolutions.LocalMedia.Domain.Continuity.WatchStatus.NotStarted, movie.WatchStatus.Status);
        Assert.False(movie.CanResume);
        Assert.Throws<ArgumentNullException>(() => movie.Apply(null!, null, null));

        var show = new ShowDetailsViewModel();
        Assert.Throws<ArgumentNullException>(() => show.Apply(null!, [], new Dictionary<ContentKey, WatchState>()));
        Assert.Throws<ArgumentNullException>(() =>
            show.Apply(Item(ShowId, CatalogTitleKind.Show, "Crónicas", true), null!, new Dictionary<ContentKey, WatchState>()));
        Assert.Throws<ArgumentNullException>(() =>
            show.Apply(Item(ShowId, CatalogTitleKind.Show, "Crónicas", true), [], null!));
    }

    [Fact]
    public void An_episode_plays_only_when_a_reachable_file_backs_it()
    {
        var requests = new List<PlayDetailsRequest>();
        var viewModel = new ShowDetailsViewModel(request =>
        {
            requests.Add(request);
            return Task.CompletedTask;
        });
        var playable = EpisodeEntry(301, season: 1, number: 1, isAvailable: true, hasFile: true);
        var missing = EpisodeEntry(302, season: 1, number: 2, isAvailable: true, hasFile: false);
        viewModel.Apply(
            Item(ShowId, CatalogTitleKind.Show, "Crónicas", isAvailable: true),
            [playable, missing],
            new Dictionary<ContentKey, WatchState>());

        var rows = viewModel.Seasons[0].Episodes;
        Assert.Equal(2, viewModel.Seasons[0].EpisodeCount);
        Assert.Equal("2", viewModel.Seasons[0].EpisodeCountText);
        Assert.Equal("1", viewModel.Seasons[0].SeasonNumberText);
        Assert.Equal("1", rows[0].EpisodeNumberText);

        Assert.True(viewModel.PlayEpisodeCommand.CanExecute(rows[0]));
        Assert.False(viewModel.PlayEpisodeCommand.CanExecute(rows[1]));
        Assert.False(viewModel.PlayEpisodeCommand.CanExecute(null));
        viewModel.PlayEpisodeCommand.Execute(rows[0]);
        viewModel.PlayEpisodeCommand.Execute(rows[1]);
        viewModel.PlayEpisodeCommand.Execute("not an episode");

        var request = Assert.Single(requests);
        Assert.Equal(playable.MediaFileId, request.MediaFileId);

        // No position, rather than zero: an episode left half-watched is resumed, and zero now means
        // "start it again" to whoever opens it. The film card is the one that names a second.
        Assert.Null(request.StartPosition);
        Assert.Equal(playable.Id, rows[0].Id);
    }

    [Fact]
    public void A_version_without_dimensions_still_produces_a_readable_label()
    {
        var bare = new MediaFileId(CreateGuid(61));
        var viewModel = new MovieDetailsViewModel();

        viewModel.Apply(
            Item(MovieId, CatalogTitleKind.Movie, "Arrival", isAvailable: true),
            watchState: null,
            new MediaVersionGroup(
                new MediaVersionId(CreateGuid(62)),
                ContentKey.ForTitle(MovieId).Value,
                [new MediaVersion(bare, @"root\a.mkv", true, null, null, null, false, "H264", 10)],
                null));

        var row = Assert.Single(viewModel.Versions);
        Assert.Equal("H264", row.QualityLabel);
        Assert.False(row.IsPreferred);
        Assert.True(row.IsEffective);
    }

    [Fact]
    public void A_manual_override_on_an_episode_is_reported_as_such()
    {
        var viewModel = new ShowDetailsViewModel();
        var episode = EpisodeEntry(401, season: 1, number: 1, isAvailable: true, hasFile: true);
        var key = ContentKey.ForEpisode(ShowId, episode.Id);

        viewModel.Apply(
            Item(ShowId, CatalogTitleKind.Show, "Crónicas", isAvailable: true),
            [episode],
            new Dictionary<ContentKey, WatchState>
            {
                [key] = State(key, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(50), WatchStatus.Watched) with
                {
                    IsManualOverride = true,
                },
            });

        var row = viewModel.Seasons[0].Episodes[0];
        Assert.True(row.IsManualOverride);
        Assert.True(row.IsWatched);
        Assert.False(row.IsInProgress);
        Assert.False(row.IsNotStarted);
        Assert.Equal(2016, viewModel.Year);
        Assert.True(viewModel.HasYear);
        Assert.Equal("2016", viewModel.YearText);
    }

    [Fact]
    public void A_show_carries_the_same_personal_marks_a_film_does()
    {
        var requests = new List<PersonalActionRequest>();
        var viewModel = new ShowDetailsViewModel(
            onPersonalActionChanged: request =>
            {
                requests.Add(request);
                return Task.CompletedTask;
            });

        viewModel.Apply(
            Item(ShowId, CatalogTitleKind.Show, "Crónicas", isAvailable: true),
            [EpisodeEntry(501, season: 1, number: 1, isAvailable: true, hasFile: true)],
            new Dictionary<ContentKey, WatchState>(),
            PersonalState.Empty(ContentKey.ForTitle(ShowId)).WithFavorite(true).WithRating(9));

        Assert.Equal(ShowId, viewModel.TitleId);
        Assert.True(viewModel.PersonalActions.IsFavorite);
        Assert.Equal(9, viewModel.PersonalActions.Rating);

        viewModel.PersonalActions.ToggleWatchLaterCommand.Execute(null);
        var request = Assert.Single(requests);
        Assert.Equal(PersonalActionKind.ToggleWatchLater, request.Kind);
    }

    [Fact]
    public void A_show_with_no_stored_marks_starts_from_an_empty_personal_state()
    {
        var viewModel = new ShowDetailsViewModel();

        viewModel.Apply(
            Item(ShowId, CatalogTitleKind.Show, "Crónicas", isAvailable: true),
            [],
            new Dictionary<ContentKey, WatchState>());

        Assert.False(viewModel.PersonalActions.IsFavorite);
        Assert.False(viewModel.PersonalActions.IsWatchLater);
        Assert.False(viewModel.PersonalActions.HasRating);
    }

    [Fact]
    public void A_season_rejects_a_negative_number_and_a_missing_episode_list()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeasonViewModel(-1, []));
        Assert.Throws<ArgumentNullException>(() => new SeasonViewModel(1, null!));
        Assert.Throws<ArgumentNullException>(() => new EpisodeRowViewModel(null!, null));
    }

    /// <summary>
    /// One season is on screen at a time, and with only one season the picker is absent.
    /// </summary>
    /// <remarks>
    /// Absent rather than disabled: a control whose only possible answer is the one it already shows
    /// is a question nobody asked, and this card has no room to spend on one. The second half is the
    /// point of the picker at all — choosing another season has to change which episodes are drawn,
    /// which a test that only checked the combo box's presence would never notice.
    /// </remarks>
    [AvaloniaFact]
    public void One_season_at_a_time_and_a_single_season_gets_no_picker()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

        var alone = new ShowDetailsViewModel();
        alone.Apply(
            Item(ShowId, CatalogTitleKind.Show, "Crónicas", isAvailable: true),
            [EpisodeEntry(401, season: 1, number: 1, isAvailable: true, hasFile: true)],
            new Dictionary<ContentKey, WatchState>());
        Assert.False(alone.HasSeasonChoice);
        using (var host = Mounted(alone))
        {
            Assert.DoesNotContain(
                host.View.GetVisualDescendants().OfType<ComboBox>(),
                picker => picker.IsEffectivelyVisible);
        }

        var many = new ShowDetailsViewModel();
        many.Apply(
            Item(ShowId, CatalogTitleKind.Show, "Crónicas", isAvailable: true),
            [
                EpisodeEntry(411, season: 1, number: 1, isAvailable: true, hasFile: true),
                EpisodeEntry(412, season: 2, number: 1, isAvailable: true, hasFile: true),
                EpisodeEntry(413, season: 2, number: 2, isAvailable: true, hasFile: true),
            ],
            new Dictionary<ContentKey, WatchState>());
        Assert.True(many.HasSeasonChoice);
        Assert.Equal(1, many.SelectedSeason?.SeasonNumber);
        using (var host = Mounted(many))
        {
            Assert.Contains(
                host.View.GetVisualDescendants().OfType<ComboBox>(),
                picker => picker.IsEffectivelyVisible);
            Assert.Single(host.View.GetVisualDescendants().OfType<EpisodeRowView>());

            many.SelectedSeason = many.Seasons[1];
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, host.View.GetVisualDescendants().OfType<EpisodeRowView>().Count());
        }
    }

    /// <summary>
    /// An episode row is 56 px tall and its numbers end on the same pixel, whatever their width.
    /// </summary>
    /// <remarks>
    /// §4 asks for the number to be monospaced and right-aligned "so the column lines up", and what
    /// lines a column up is measurable: episode 9 and episode 10 have to finish at the same x. Asserted
    /// that way rather than on the font family, because a family name is a means and the alignment is
    /// the end — and a proportional font in a fixed, right-aligned column would satisfy the row's
    /// purpose while a monospaced one in a loose column would not.
    /// </remarks>
    [AvaloniaFact]
    public void An_episode_row_is_56_px_tall_and_its_numbers_end_on_the_same_pixel()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

        var show = new ShowDetailsViewModel();
        show.Apply(
            Item(ShowId, CatalogTitleKind.Show, "Crónicas", isAvailable: true),
            [
                EpisodeEntry(301, season: 1, number: 9, isAvailable: true, hasFile: true),
                EpisodeEntry(302, season: 1, number: 10, isAvailable: true, hasFile: true),
            ],
            new Dictionary<ContentKey, WatchState>());

        var view = new ShowDetailsView { DataContext = show };
        var window = new Window { Width = 1024, Height = 720, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var rows = view.GetVisualDescendants().OfType<EpisodeRowView>().ToArray();
        Assert.Equal(2, rows.Length);
        Assert.All(rows, row => Assert.Equal(56, row.Bounds.Height));

        var rightEdges = rows
            .Select(row => Assert.Single(
                row.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text is "9" or "10"))
            .Select(block => block.TranslatePoint(new Point(block.Bounds.Width, 0), window)!.Value.X)
            .ToArray();
        Assert.Equal(rightEdges[0], rightEdges[1], precision: 3);

        window.Close();
    }

    [AvaloniaFact]
    public void Both_detail_surfaces_name_every_control_and_accept_keyboard_focus()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

        var movie = new MovieDetailsViewModel();
        movie.Apply(
            Item(MovieId, CatalogTitleKind.Movie, "Arrival", isAvailable: true),
            State(ContentKey.ForTitle(MovieId), TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(116)),
            versions: null);
        AssertNamedAndFocusable(new MovieDetailsView { DataContext = movie });

        var show = new ShowDetailsViewModel();
        show.Apply(
            Item(ShowId, CatalogTitleKind.Show, "Crónicas", isAvailable: true),
            [EpisodeEntry(201, season: 1, number: 1, isAvailable: true, hasFile: true)],
            new Dictionary<ContentKey, WatchState>());
        AssertNamedAndFocusable(new ShowDetailsView { DataContext = show });
    }

    private static void AssertNamedAndFocusable(Control view)
    {
        var window = new Window { Width = 1024, Height = 720, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var buttons = view.GetVisualDescendants().OfType<Button>().ToArray();
        Assert.NotEmpty(buttons);
        foreach (var button in buttons)
        {
            Assert.True(button.Focusable);
            Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)),
                "A details button has no accessible name.");
        }

        window.Close();
    }

    private static CatalogItem Item(TitleId id, CatalogTitleKind kind, string title, bool isAvailable) => new(
        id,
        kind,
        title,
        2016,
        isAvailable,
        HasProgress: true,
        IsPersonal: false,
        Noon,
        Noon);

    private static WatchState State(
        ContentKey content,
        TimeSpan position,
        TimeSpan duration,
        WatchStatus status = WatchStatus.InProgress) => new()
        {
            Content = content,
            Position = position,
            ObservedDuration = duration,
            SourceMediaFileId = new MediaFileId(CreateGuid(41)),
            Status = status,
            IsManualOverride = false,
            StartedUtc = Noon.AddHours(-1),
            UpdatedUtc = Noon,
        };

    /// <summary>A show card in a window, closed when the caller is done with it.</summary>
    private static MountedShow Mounted(ShowDetailsViewModel viewModel)
    {
        var view = new ShowDetailsView { DataContext = viewModel };
        var window = new Window { Width = 1024, Height = 720, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new MountedShow(window, view);
    }

    private sealed record MountedShow(Window Window, ShowDetailsView View) : IDisposable
    {
        public void Dispose() => Window.Close();
    }

    private static EpisodeSequenceEntry EpisodeEntry(
        int seed,
        int season,
        int number,
        bool isAvailable,
        bool hasFile) => new(
        new EpisodeId(CreateGuid(seed)),
        ShowId,
        season,
        number,
        hasFile ? new MediaFileId(CreateGuid(seed + 500)) : null,
        hasFile ? $@"root\s{season:D2}e{number:D2}.mkv" : null,
        isAvailable);

    private static Guid CreateGuid(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private sealed class SingleQueryService(CatalogPage page) : ICatalogQueryService
    {
        public Task<CatalogPage> QueryAsync(
            CatalogQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(page);
        }
    }
}
