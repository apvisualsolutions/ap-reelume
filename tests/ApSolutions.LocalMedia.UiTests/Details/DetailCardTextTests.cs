// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Show;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Details;

/// <summary>
/// What the two detail cards write under their titles, and what they write when there is nothing.
/// </summary>
/// <remarks>
/// Both banners said a year and stopped, because the read model behind them carried a year and
/// stopped. They carry the running time, the genres, the seasons and the counts since 2026-08-24,
/// and every one of those is a piece that can be missing — a film nobody identified has no genre, a
/// series with one season has nothing to say about its seasons, an episode with no file has no
/// length. What is measured here is the missing half: a line that comes out shorter rather than a
/// line that comes out with an orphaned separator in it.
/// </remarks>
public sealed class DetailCardTextTests
{
    private static readonly TitleId MovieId = new(Guid.Parse("e1000000-0000-4000-8000-000000000001"));
    private static readonly TitleId ShowId = new(Guid.Parse("e1000000-0000-4000-8000-000000000002"));
    private static readonly LibraryRootId Root = new(Guid.Parse("e2000000-0000-4000-8000-000000000001"));
    private static readonly DateTimeOffset Noon = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [AvaloniaFact]
    public void The_film_banner_writes_the_year_the_genres_and_the_length_and_drops_what_is_missing()
    {
        var full = new MovieDetailsViewModel();
        full.Apply(
            Film(2016, ["Ciencia ficción", "Drama", "Suspense"], TimeSpan.FromMinutes(116)),
            watchState: null,
            versions: null);

        Assert.True(full.HasMeta);
        Assert.StartsWith("2016 · Ciencia ficción · Drama", full.MetaText, StringComparison.Ordinal);
        Assert.DoesNotContain("Suspense", full.MetaText, StringComparison.Ordinal);
        Assert.Contains("116", full.MetaText, StringComparison.Ordinal);

        var bare = new MovieDetailsViewModel();
        bare.Apply(
            Film(year: null, genres: null, runtime: null),
            watchState: null,
            versions: null);
        Assert.Equal(string.Empty, bare.MetaText);
        Assert.False(bare.HasMeta);

        // A stored running time of zero is not a running time: the card would write «0 min» for a
        // file whose duration nobody ever read.
        var zero = new MovieDetailsViewModel();
        zero.Apply(Film(2016, null, TimeSpan.Zero), watchState: null, versions: null);
        Assert.Equal("2016", zero.MetaText);
    }

    /// <summary>The two chips under the title, which are keys rather than words.</summary>
    [AvaloniaFact]
    public void The_film_banner_says_whether_the_file_is_there_and_how_far_through_it_is()
    {
        var notStarted = new MovieDetailsViewModel();
        notStarted.Apply(Film(2016, null, null), watchState: null, versions: null);
        Assert.Equal("MediaAvailable", notStarted.AvailabilityKey);
        Assert.Equal("WatchStatusNotStarted", notStarted.WatchStatusKey);

        var inProgress = new MovieDetailsViewModel();
        inProgress.Apply(
            Film(2016, null, null),
            State(WatchStatus.InProgress),
            versions: null);
        Assert.Equal("WatchStatusInProgress", inProgress.WatchStatusKey);

        var watched = new MovieDetailsViewModel();
        watched.Apply(Film(2016, null, null), State(WatchStatus.Watched), versions: null);
        Assert.Equal("WatchStatusWatched", watched.WatchStatusKey);

        var gone = new MovieDetailsViewModel();
        gone.Apply(Film(2016, null, null, isAvailable: false), watchState: null, versions: null);
        Assert.Equal("MediaUnavailable", gone.AvailabilityKey);
    }

    /// <summary>
    /// A film with no duplicates still lists the copy it has, which is what the card's left column
    /// is for. It listed nothing at all until 2026-08-25: a group is only formed when there are two,
    /// so the ordinary film had an empty half.
    /// </summary>
    [AvaloniaFact]
    public void A_film_with_no_duplicates_still_lists_the_copy_it_has()
    {
        var withFile = new MovieDetailsViewModel();
        withFile.Apply(
            Film(2016, null, TimeSpan.FromMinutes(116)),
            watchState: null,
            versions: null,
            file: File(TimeSpan.FromMinutes(116), 4_509_715_660));

        var row = Assert.Single(withFile.Versions);
        Assert.True(withFile.HasVersions);
        Assert.True(row.IsEffective);
        Assert.False(row.IsPreferred);
        Assert.True(row.HasTechnical);
        Assert.Contains("1:56:00", row.TechnicalText, StringComparison.Ordinal);
        Assert.Contains("GB", row.TechnicalText, StringComparison.Ordinal);
        Assert.EndsWith("Arrival.mkv", row.PathText, StringComparison.Ordinal);

        // Under a gigabyte the row says megabytes, and with neither a length nor a size it says
        // nothing rather than an empty pair of separators.
        var small = new MovieDetailsViewModel();
        small.Apply(
            Film(2016, null, null),
            watchState: null,
            versions: null,
            file: File(null, 734_003_200));
        Assert.Contains("MB", Assert.Single(small.Versions).TechnicalText, StringComparison.Ordinal);

        var silent = new MovieDetailsViewModel();
        silent.Apply(Film(2016, null, null), watchState: null, versions: null, file: File(null, 0));
        var quiet = Assert.Single(silent.Versions);
        Assert.False(quiet.HasTechnical);
        Assert.Equal(string.Empty, quiet.TechnicalText);

        // And a card built with no file at all lists nothing, which is what a title whose row the
        // catalogue lost looks like.
        var none = new MovieDetailsViewModel();
        none.Apply(Film(2016, null, null), watchState: null, versions: null);
        Assert.Empty(none.Versions);
        Assert.False(none.HasVersions);
    }

    /// <summary>
    /// The series banner's line, its counter, and the episode it is waiting on.
    /// </summary>
    [AvaloniaFact]
    public void The_series_banner_counts_its_seasons_its_episodes_and_what_is_left()
    {
        var show = new ShowDetailsViewModel();
        show.Apply(
            Series(2021, ["Drama", "Intriga"]),
            [
                Episode(1, 1, hasFile: true, TimeSpan.FromMinutes(48)),
                Episode(1, 2, hasFile: true, TimeSpan.FromMinutes(51)),
                Episode(2, 1, hasFile: true, TimeSpan.FromMinutes(49)),
            ],
            new Dictionary<ContentKey, WatchState>
            {
                [ContentKey.ForEpisode(ShowId, Ep(1, 1))] = State(WatchStatus.Watched),
                [ContentKey.ForEpisode(ShowId, Ep(1, 2))] = State(
                    WatchStatus.InProgress,
                    TimeSpan.FromMinutes(17),
                    TimeSpan.FromMinutes(51)),
            });

        Assert.True(show.HasMeta);
        Assert.Contains("2021", show.MetaText, StringComparison.Ordinal);
        Assert.Contains("Drama", show.MetaText, StringComparison.Ordinal);
        Assert.Contains("2", show.MetaText, StringComparison.Ordinal);
        Assert.Equal(3, show.EpisodeTotal);
        Assert.Equal(1, show.WatchedCount);
        Assert.True(show.HasProgress);
        Assert.Equal(1d / 3d, show.WatchedFraction, 3);
        Assert.Contains("1/3", show.ProgressText, StringComparison.Ordinal);

        // The one left half-watched comes before the one nobody has opened.
        Assert.True(show.HasNextEpisode);
        Assert.Equal(2, show.NextEpisode!.EpisodeNumber);
        Assert.Contains("E02", show.NextEpisodeLabel, StringComparison.Ordinal);
        Assert.Contains("17:00", show.NextEpisodeSubText, StringComparison.Ordinal);
        Assert.True(show.ContinueCommand.CanExecute(null));
    }

    /// <summary>
    /// A series nobody has started offers its first episode; one that is finished offers none, and
    /// says so instead of naming an episode that does not exist.
    /// </summary>
    [AvaloniaFact]
    public void A_series_offers_the_first_episode_or_says_it_is_finished()
    {
        var fresh = new ShowDetailsViewModel();
        fresh.Apply(
            Series(null, null),
            [Episode(1, 1, hasFile: true, TimeSpan.FromMinutes(48))],
            new Dictionary<ContentKey, WatchState>());

        Assert.Equal(1, fresh.NextEpisode!.EpisodeNumber);
        Assert.Equal(0, fresh.WatchedCount);
        Assert.Equal(0, fresh.WatchedFraction);

        // One season says nothing about its seasons — the picker is absent for the same reason — and
        // its one episode is counted in the singular rather than as «1 episodios».
        Assert.DoesNotContain("season", fresh.MetaText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("temporada", fresh.MetaText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("episodes", fresh.MetaText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("episodios", fresh.MetaText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 ", fresh.MetaText, StringComparison.Ordinal);

        var finished = new ShowDetailsViewModel();
        finished.Apply(
            Series(2021, null),
            [Episode(1, 1, hasFile: true, TimeSpan.FromMinutes(48))],
            new Dictionary<ContentKey, WatchState>
            {
                [ContentKey.ForEpisode(ShowId, Ep(1, 1))] = State(WatchStatus.Watched),
            });

        Assert.Null(finished.NextEpisode);
        Assert.False(finished.HasNextEpisode);
        Assert.False(finished.ContinueCommand.CanExecute(null));
        Assert.NotEqual(string.Empty, finished.NextEpisodeLabel);
        Assert.NotEqual(string.Empty, finished.NextEpisodeSubText);
        Assert.Equal(1, finished.WatchedCount);

        // An episode with no file behind it is never what a series is waiting on: it is listed so
        // the season is not shorter than it is, and it cannot be started.
        var unplayable = new ShowDetailsViewModel();
        unplayable.Apply(
            Series(2021, null),
            [Episode(1, 1, hasFile: false, runtime: null)],
            new Dictionary<ContentKey, WatchState>());
        Assert.Null(unplayable.NextEpisode);
        Assert.False(unplayable.ContinueCommand.CanExecute(null));

        // The episode is still counted: a season that hid the file it cannot reach would look
        // shorter than it is, which is the one thing the row exists to prevent.
        Assert.Equal(1, unplayable.EpisodeTotal);
        Assert.Equal(0, unplayable.WatchedCount);
        Assert.Equal(0, unplayable.WatchedFraction);
        Assert.Contains("0/1", unplayable.ProgressText, StringComparison.Ordinal);
    }

    /// <summary>
    /// A season names itself, counts what is watched in it, and exactly one of them is lit.
    /// </summary>
    [AvaloniaFact]
    public void The_season_pills_name_themselves_and_only_one_is_ever_lit()
    {
        var show = new ShowDetailsViewModel();
        show.Apply(
            Series(2021, null),
            [
                Episode(0, 1, hasFile: true, TimeSpan.FromMinutes(20)),
                Episode(1, 1, hasFile: true, TimeSpan.FromMinutes(48)),
                Episode(2, 1, hasFile: true, TimeSpan.FromMinutes(49)),
            ],
            new Dictionary<ContentKey, WatchState>
            {
                [ContentKey.ForEpisode(ShowId, Ep(1, 1))] = State(WatchStatus.Watched),
            });

        Assert.Equal(3, show.Seasons.Count);
        Assert.True(show.HasSeasonChoice);
        Assert.Single(show.Seasons, season => season.IsSelected);
        Assert.Equal(1, show.SelectedSeason!.SeasonNumber);
        Assert.Equal(1, show.SelectedSeason.WatchedCount);
        Assert.NotEqual(string.Empty, show.SelectedSeason.SeasonLabel);

        var specials = show.Seasons.Single(season => season.IsSpecials);
        Assert.NotEqual(specials.SeasonLabel, show.SelectedSeason.SeasonLabel);
        Assert.Equal(0, specials.WatchedCount);

        show.SelectSeasonCommand.Execute(specials);
        Assert.True(specials.IsSelected);
        Assert.Single(show.Seasons, season => season.IsSelected);

        // Setting the same season again changes nothing and says nothing.
        specials.IsSelected = true;
        show.SelectSeasonCommand.Execute(specials);
        Assert.Single(show.Seasons, season => season.IsSelected);

        // And anything that is not a season is refused rather than acted on.
        Assert.False(show.SelectSeasonCommand.CanExecute("Temporada 1"));
        show.SelectSeasonCommand.Execute(null);
        Assert.True(specials.IsSelected);
    }

    /// <summary>What one episode row says: its number, its name, its length and its state.</summary>
    [AvaloniaFact]
    public void An_episode_row_says_its_number_its_name_its_length_and_its_state()
    {
        var show = new ShowDetailsViewModel();
        show.Apply(
            Series(2021, null),
            [
                Episode(1, 9, hasFile: true, TimeSpan.FromMinutes(48), "La marea baja"),
                Episode(1, 10, hasFile: true, TimeSpan.FromMinutes(51), title: null),
                Episode(1, 11, hasFile: false, runtime: null, "Sin archivo"),
            ],
            new Dictionary<ContentKey, WatchState>
            {
                [ContentKey.ForEpisode(ShowId, Ep(1, 9))] = State(
                    WatchStatus.Watched,
                    TimeSpan.FromMinutes(48),
                    TimeSpan.FromMinutes(48)),
                [ContentKey.ForEpisode(ShowId, Ep(1, 10))] = State(
                    WatchStatus.InProgress,
                    TimeSpan.FromMinutes(17),
                    TimeSpan.FromMinutes(51)),
            });

        var episodes = show.SelectedSeason!.Episodes;
        var watched = episodes[0];
        Assert.Equal("E09", watched.NumberBadge);
        Assert.Equal("La marea baja", watched.EpisodeTitle);
        Assert.Contains("48", watched.MetaText, StringComparison.Ordinal);
        Assert.True(watched.HasProgress);
        Assert.Equal(1, watched.CompletedFraction, 3);

        var started = episodes[1];
        Assert.Equal("E10", started.NumberBadge);
        Assert.Equal("S01E10", started.EpisodeTitle);
        Assert.Contains("17:00", started.MetaText, StringComparison.Ordinal);
        Assert.Equal(17d / 51d, started.CompletedFraction, 3);

        var untouched = episodes[2];
        Assert.Equal("Sin archivo", untouched.EpisodeTitle);
        Assert.False(untouched.HasProgress);
        Assert.Equal(0, untouched.CompletedFraction);
        Assert.False(untouched.IsPlayable);
        Assert.Equal(string.Empty, untouched.MetaText.Replace(untouched.MetaText, string.Empty, StringComparison.Ordinal));
    }

    /// <summary>
    /// The corners of both cards that no screen reaches: a card built over nothing, a command with
    /// no hand behind it, and the identifiers the shell reads off whichever card is open.
    /// </summary>
    /// <remarks>
    /// Each of these is a branch that only runs when something upstream is absent, which is exactly
    /// when a crash is least welcome — the shell asks a card for its title id while the card is
    /// still empty, and a trailer button is offered by a card whose host never wired one.
    /// </remarks>
    [AvaloniaFact]
    public void A_card_over_nothing_answers_without_throwing()
    {
        var emptyFilm = new MovieDetailsViewModel();
        Assert.Equal(default, emptyFilm.TitleId);
        Assert.False(emptyFilm.HasYear);
        Assert.False(emptyFilm.IsAvailable);
        Assert.Equal(string.Empty, emptyFilm.MetaText);
        emptyFilm.PlayTrailerCommand.Execute(null);
        emptyFilm.OpenTrailerLinkCommand.Execute(null);

        var emptyShow = new ShowDetailsViewModel();
        Assert.Equal(default, emptyShow.TitleId);
        Assert.False(emptyShow.IsAvailable);
        Assert.False(emptyShow.HasProgress);
        Assert.Equal(0, emptyShow.EpisodeTotal);
        emptyShow.ContinueCommand.Execute(null);
        emptyShow.OpenTrailerLinkCommand.Execute(null);

        // Neither the episode command nor the season command answers to something that is not one.
        emptyShow.PlayEpisodeCommand.Execute("not an episode");
        emptyShow.SelectSeasonCommand.Execute(42);
        Assert.Null(emptyShow.SelectedSeason);
    }

    /// <summary>
    /// A film with a running time and nothing else still writes a line, and one whose card has no
    /// hand behind Play reaches nobody rather than throwing.
    /// </summary>
    [AvaloniaFact]
    public void A_film_with_only_a_running_time_still_writes_a_line()
    {
        var runtimeOnly = new MovieDetailsViewModel();
        runtimeOnly.Apply(
            Film(year: null, genres: null, runtime: TimeSpan.FromMinutes(118)),
            watchState: null,
            versions: null);

        Assert.True(runtimeOnly.HasMeta);
        Assert.Contains("118", runtimeOnly.MetaText, StringComparison.Ordinal);
        Assert.DoesNotContain("·", runtimeOnly.MetaText, StringComparison.Ordinal);
        Assert.Equal(MovieId, runtimeOnly.TitleId);
        Assert.True(runtimeOnly.IsAvailable);

        // Nothing is wired behind Play here, which is what a card looks like before its host hands
        // one in: the command answers and the card stays standing.
        runtimeOnly.PlayCommand.Execute(null);
        runtimeOnly.ResumeCommand.Execute(null);
    }

    /// <summary>
    /// An episode whose file never reported a duration falls back to what the watch state observed,
    /// and one that is in progress at its very start says so in words rather than in a time.
    /// </summary>
    [AvaloniaFact]
    public void An_episode_with_no_stored_length_falls_back_to_what_was_observed()
    {
        var show = new ShowDetailsViewModel();
        show.Apply(
            Series(2021, null),
            [
                Episode(0, 1, hasFile: true, runtime: null, "Especial"),
                Episode(1, 1, hasFile: true, runtime: null, "Sin duración"),
            ],
            new Dictionary<ContentKey, WatchState>
            {
                [ContentKey.ForEpisode(ShowId, Ep(1, 1))] = State(
                    WatchStatus.InProgress,
                    TimeSpan.FromMinutes(12),
                    TimeSpan.FromMinutes(48)),
                [ContentKey.ForEpisode(ShowId, Ep(0, 1))] = State(
                    WatchStatus.InProgress,
                    TimeSpan.Zero,
                    TimeSpan.FromMinutes(20)),
            });

        var regular = show.Seasons.Single(season => season.IsRegular);
        Assert.True(regular.IsRegular);
        var started = regular.Episodes[0];
        Assert.Equal(12d / 48d, started.CompletedFraction, 3);
        Assert.True(started.HasProgress);

        // A running time nobody read means the row says only its state — and a state of «in progress»
        // with nothing behind it says the words rather than «Resume at 0:00».
        var specials = show.Seasons.Single(season => season.IsSpecials);
        var atZero = specials.Episodes[0];
        Assert.Equal(0, atZero.CompletedFraction);
        Assert.False(atZero.HasProgress);
        Assert.DoesNotContain("0:00", atZero.MetaText, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, atZero.MetaText);
    }

    /// <summary>
    /// Starting something from a card carries what the card calls it, and the trailer and the
    /// version list have the same silent halves.
    /// </summary>
    /// <remarks>
    /// The player's header writes the title and the line under it, and both travel on the request
    /// rather than being looked up again — so what a card hands over is asserted here, on the object
    /// itself, and not only through a session that would have to be opened to see it.
    /// </remarks>
    [AvaloniaFact]
    public void What_a_card_hands_over_carries_the_name_the_card_used()
    {
        var requests = new List<PlayDetailsRequest>();
        var film = new MovieDetailsViewModel(request =>
        {
            requests.Add(request);
            return Task.CompletedTask;
        });
        film.Apply(
            Film(2016, ["Drama"], TimeSpan.FromMinutes(116)),
            watchState: null,
            versions: null,
            file: File(TimeSpan.FromMinutes(116), 4_509_715_660));

        film.PlayCommand.Execute(null);
        var request = Assert.Single(requests);
        Assert.Equal("Arrival", request.Title);
        Assert.Contains("2016", request.Subtitle!, StringComparison.Ordinal);

        // A group with nothing in it lists nothing: the card asks the policy for an effective
        // version and a policy with no versions has none to answer with.
        var empty = new MovieDetailsViewModel();
        empty.Apply(
            Film(2016, null, null),
            watchState: null,
            new MediaVersionGroup(
                new MediaVersionId(Guid.Parse("e4000000-0000-4000-8000-000000000001")),
                ContentKey.ForTitle(MovieId).Value,
                [],
                PreferredMediaFileId: null));
        Assert.Empty(empty.Versions);
        Assert.False(empty.HasVersions);
    }

    /// <summary>The trailer beside the film, and the one behind a link, each reaching its own hand.</summary>
    [AvaloniaFact]
    public void Both_trailers_reach_the_hand_that_opens_them()
    {
        var played = new List<string>();
        var opened = new List<string>();
        var film = new MovieDetailsViewModel(
            onPlayTrailer: path =>
            {
                played.Add(path);
                return Task.CompletedTask;
            },
            onOpenTrailerLink: link =>
            {
                opened.Add(link);
                return Task.CompletedTask;
            });
        film.Apply(
            Film(2016, null, null),
            watchState: null,
            versions: null,
            trailerPath: @"D:\Cine\Arrival-trailer.mkv",
            trailerKey: "dQw4w9WgXcQ");

        Assert.True(film.HasTrailer);
        Assert.True(film.HasTrailerLink);
        film.PlayTrailerCommand.Execute(null);
        film.OpenTrailerLinkCommand.Execute(null);

        Assert.Equal(@"D:\Cine\Arrival-trailer.mkv", Assert.Single(played));
        Assert.Contains("dQw4w9WgXcQ", Assert.Single(opened), StringComparison.Ordinal);
    }

    /// <summary>The series card's own button starts the episode it names.</summary>
    [AvaloniaFact]
    public void Continue_on_a_series_starts_the_episode_the_banner_names()
    {
        var requests = new List<PlayDetailsRequest>();
        var show = new ShowDetailsViewModel(request =>
        {
            requests.Add(request);
            return Task.CompletedTask;
        });
        show.Apply(
            Series(2021, null),
            [
                Episode(1, 1, hasFile: true, TimeSpan.FromMinutes(48)),
                Episode(1, 2, hasFile: true, TimeSpan.FromMinutes(51), "Cuaderno"),
            ],
            new Dictionary<ContentKey, WatchState>
            {
                [ContentKey.ForEpisode(ShowId, Ep(1, 1))] = State(
                    WatchStatus.Watched,
                    TimeSpan.FromMinutes(48),
                    TimeSpan.FromMinutes(48)),
            });

        Assert.True(show.ContinueCommand.CanExecute(null));
        show.ContinueCommand.Execute(null);

        var request = Assert.Single(requests);
        Assert.Equal(show.NextEpisode!.MediaFileId, request.MediaFileId);
        Assert.Equal("Puerto Sombra", request.Title);
        Assert.Contains("Cuaderno", request.Subtitle!, StringComparison.Ordinal);
        Assert.Null(request.StartPosition);
    }

    private static CatalogItem Film(
        int? year,
        IReadOnlyList<string>? genres,
        TimeSpan? runtime,
        bool isAvailable = true) => new(
        MovieId,
        CatalogTitleKind.Movie,
        "Arrival",
        year,
        isAvailable,
        HasProgress: false,
        IsPersonal: false,
        Noon,
        LastPlayedUtc: null,
        runtime,
        genres);

    private static CatalogItem Series(int? year, IReadOnlyList<string>? genres) => new(
        ShowId,
        CatalogTitleKind.Show,
        "Puerto Sombra",
        year,
        IsAvailable: true,
        HasProgress: false,
        IsPersonal: false,
        Noon,
        LastPlayedUtc: null,
        Runtime: null,
        genres);

    private static MediaFile File(TimeSpan? duration, long sizeBytes) => new(
        new MediaFileId(Guid.Parse("e3000000-0000-4000-8000-000000000001")),
        Root,
        @"D:\Cine\Arrival.mkv",
        sizeBytes,
        Noon,
        new TechnicalMetadata(duration, "matroska", ["h264"], ["aac"], 1920, 1080));

    private static EpisodeSequenceEntry Episode(
        int season,
        int number,
        bool hasFile,
        TimeSpan? runtime,
        string? title = null) => new(
        Ep(season, number),
        ShowId,
        season,
        number,
        hasFile ? new MediaFileId(Ep(season, number + 100).Value) : null,
        hasFile ? $@"root\s{season:D2}e{number:D2}.mkv" : null,
        hasFile,
        title,
        runtime);

    private static WatchState State(
        WatchStatus status,
        TimeSpan? position = null,
        TimeSpan? duration = null) => new()
        {
            Content = ContentKey.ForTitle(MovieId),
            Position = position ?? TimeSpan.FromMinutes(40),
            ObservedDuration = duration ?? TimeSpan.FromMinutes(116),
            SourceMediaFileId = new MediaFileId(Guid.Parse("e3000000-0000-4000-8000-000000000002")),
            Status = status,
            IsManualOverride = false,
            StartedUtc = Noon.AddHours(-1),
            UpdatedUtc = Noon,
        };

    private static EpisodeId Ep(int season, int number)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes((season * 1000) + number).CopyTo(bytes, 0);
        return new EpisodeId(new Guid(bytes));
    }
}
