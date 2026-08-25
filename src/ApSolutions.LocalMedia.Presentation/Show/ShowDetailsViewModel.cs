// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Presentation.Catalog;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Movie;

namespace ApSolutions.LocalMedia.Presentation.Show;

/// <summary>
/// The complete series details: every season the catalogue knows, every episode inside it, and what
/// each episode's state and availability are. Specials come last because a special is chosen, not
/// run into.
/// </summary>
public sealed class ShowDetailsViewModel : INotifyPropertyChanged
{
    private readonly Func<PlayDetailsRequest, Task>? _onPlay;
    private readonly Func<string, Task>? _onOpenTrailerLink;
    private CatalogItem? _item;
    private string? _overview;
    private string? _trailerLink;
    private IReadOnlyList<SeasonViewModel> _seasons = [];
    private SeasonViewModel? _selectedSeason;

    public ShowDetailsViewModel(
        Func<PlayDetailsRequest, Task>? onPlay = null,
        Func<PersonalActionRequest, Task>? onPersonalActionChanged = null,
        Func<string, Task>? onOpenTrailerLink = null)
    {
        _onPlay = onPlay;
        _onOpenTrailerLink = onOpenTrailerLink;
        OpenTrailerLinkCommand = new AsyncRelayCommand(OpenTrailerLinkAsync, () => HasTrailerLink);
        PersonalActions = new PersonalActionsViewModel(onPersonalActionChanged);
        // The parameter is the row the view bound, and only a playable one gets through — an episode
        // with no file behind it is a row that is shown but cannot be started.
        PlayEpisodeCommand = new AsyncRelayCommand(
            parameter => parameter is EpisodeRowViewModel episode ? PlayAsync(episode) : Task.CompletedTask,
            parameter => parameter is EpisodeRowViewModel { IsPlayable: true });
        // What the banner's own button does: start the one episode this series is waiting on, which
        // the card already had to work out in order to name it.
        ContinueCommand = new AsyncRelayCommand(
            () => NextEpisode is { } next ? PlayAsync(next) : Task.CompletedTask,
            () => NextEpisode is { IsPlayable: true });
        SelectSeasonCommand = new AsyncRelayCommand(
            parameter =>
            {
                if (parameter is SeasonViewModel season)
                {
                    SelectedSeason = season;
                }

                return Task.CompletedTask;
            },
            parameter => parameter is SeasonViewModel);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand PlayEpisodeCommand { get; }

    /// <summary>Starts the episode the banner names, which is where a series is picked up.</summary>
    public ICommand ContinueCommand { get; }

    /// <summary>Puts one season on screen. The pills call it; nothing else changes the selection.</summary>
    public ICommand SelectSeasonCommand { get; }

    /// <summary>
    /// Opens the provider's trailer in the browser (LIB-015). A series has no single file to hang a
    /// local trailer on, so unlike a film this card offers only this one — and it leaves the
    /// application, because playing YouTube inside would need a route their terms do not allow.
    /// </summary>
    public ICommand OpenTrailerLinkCommand { get; }

    /// <summary>Favourite, watch later, and rating for the series as a whole.</summary>
    public PersonalActionsViewModel PersonalActions { get; }

    /// <summary>Which series is shown, so the host can key personal marks to the content.</summary>
    public TitleId TitleId => _item?.Id ?? default;

    public string Title => _item?.Title ?? string.Empty;

    /// <summary>The banner poster's two letters, from the same well the grid's cards drink.</summary>
    public string Initials => Library.PosterInitials.From(Title);

    /// <summary>
    /// What this series is about, as the stored metadata has it. Handed in like everything else this
    /// view model shows; it queries nothing.
    /// </summary>
    public string? Overview => _overview;

    /// <summary>True only for a synopsis with something in it; blank is absent.</summary>
    public bool HasOverview => !string.IsNullOrWhiteSpace(_overview);

    /// <summary>True only when the stored key was well formed; anything else offers nothing.</summary>
    public bool HasTrailerLink => _trailerLink is not null;

    public int? Year => _item?.Year;

    public string YearText => Year is { } year ? year.ToString(CultureInfo.CurrentCulture) : string.Empty;

    public bool HasYear => Year is not null;

    public bool IsAvailable => _item?.IsAvailable ?? false;

    /// <summary>
    /// The line under the title: year, genres, how many seasons and how many episodes.
    /// </summary>
    /// <remarks>
    /// The card said a year and nothing else. The season count is left out of a series that has one,
    /// because a one-season series has nothing to say about its seasons anyway — the picker above is
    /// absent for the same reason. The episode count stays and changes word instead: a series with
    /// one episode is a real thing, and «1 episodios» is not a sentence in either language.
    /// </remarks>
    public string MetaText => string.Join(
        " · ",
        new[]
        {
            YearText,
            _item?.Genres is { Count: > 0 } genres ? string.Join(" · ", genres.Take(2)) : string.Empty,
            Seasons.Count > 1 ? Count(Seasons.Count, "ShowSeasonsSuffix", "seasons") : string.Empty,
            EpisodeTotal > 0
                ? Count(
                    EpisodeTotal,
                    EpisodeTotal == 1 ? "CatalogEpisodeSuffixOne" : "CatalogEpisodesSuffix",
                    EpisodeTotal == 1 ? "episode" : "episodes")
                : string.Empty,
        }.Where(piece => piece.Length > 0));

    public bool HasMeta => MetaText.Length > 0;

    /// <summary>Every episode the catalogue knows of this series, specials included.</summary>
    public int EpisodeTotal => Seasons.Sum(season => season.EpisodeCount);

    /// <summary>And how many of them are finished.</summary>
    public int WatchedCount => Seasons.Sum(season => season.WatchedCount);

    /// <summary>The sentence under the bar, which counts what is done out of what there is.</summary>
    public string ProgressText => ShowText.Resource("ShowWatchedOfTotal", "{0}/{1} watched")
        .Replace("{0}", WatchedCount.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal)
        .Replace("{1}", EpisodeTotal.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);

    /// <summary>How much of the series is behind you, between nothing and one.</summary>
    public double WatchedFraction => EpisodeTotal > 0 ? WatchedCount / (double)EpisodeTotal : 0;

    /// <summary>A bar is drawn only for a series that has episodes to count.</summary>
    public bool HasProgress => EpisodeTotal > 0;

    /// <summary>
    /// The episode this series is waiting on: the one left half-watched, or the first nobody has
    /// started. Ordered by the same policy the player chains with, so a series never resumes into a
    /// special.
    /// </summary>
    public EpisodeRowViewModel? NextEpisode
    {
        get
        {
            var rows = Seasons.SelectMany(season => season.Episodes).ToArray();
            return Array.Find(rows, row => row.IsInProgress && row.IsPlayable)
                ?? Array.Find(rows, row => row.IsNotStarted && row.IsPlayable);
        }
    }

    public bool HasNextEpisode => NextEpisode is not null;

    /// <summary>The code and the name of that episode, or the sentence for one with nothing left.</summary>
    public string NextEpisodeLabel => NextEpisode is { } next
        ? ShowText.Resource("ShowNextEpisodeCode", "S{0}·E{1}")
            .Replace("{0}", next.SeasonNumber.ToString("D2", CultureInfo.CurrentCulture), StringComparison.Ordinal)
            .Replace("{1}", next.EpisodeNumber.ToString("D2", CultureInfo.CurrentCulture), StringComparison.Ordinal)
            + " · "
            + next.EpisodeTitle
        : ShowText.Resource("ShowFinished", "Series finished");

    /// <summary>Where it would be picked up, or that nobody has opened it.</summary>
    public string NextEpisodeSubText => NextEpisode is { IsInProgress: true } next
        ? next.MetaText
        : ShowText.Resource("WatchStatusNotStarted", "Not started");

    public IReadOnlyList<SeasonViewModel> Seasons
    {
        get => _seasons;
        private set => SetField(ref _seasons, value);
    }

    /// <summary>
    /// The season on screen. One season at a time rather than every season stacked: a long-running
    /// series is otherwise a page nobody can reach the end of, and the seasons above the one being
    /// watched are dead weight on every visit.
    /// </summary>
    public SeasonViewModel? SelectedSeason
    {
        get => _selectedSeason;
        set
        {
            if (!SetField(ref _selectedSeason, value))
            {
                return;
            }

            // Exactly one pill is lit, and the loop is what guarantees it: setting the new one and
            // trusting the old one to have been cleared is how a chooser ends up with two answers.
            foreach (var season in Seasons)
            {
                season.IsSelected = ReferenceEquals(season, value);
            }
        }
    }

    /// <summary>
    /// Whether the picker is worth showing. With one season it is <b>absent</b>, not disabled: a
    /// control that can only answer what it already says is a question nobody asked.
    /// </summary>
    public bool HasSeasonChoice => Seasons.Count > 1;

    /// <summary>True when the catalogue knows the series but no episode of it.</summary>
    public bool IsEmpty => Seasons.Count == 0;

    public bool HasSeasons => Seasons.Count > 0;

    /// <summary>Applies episodes and states that were already read; the view queries nothing.</summary>
    public void Apply(
        CatalogItem item,
        IReadOnlyList<EpisodeSequenceEntry> episodes,
        IReadOnlyDictionary<ContentKey, WatchState> watchStates,
        PersonalState? personalState = null,
        string? overview = null,
        string? trailerKey = null)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _overview = overview;
        _trailerLink = TrailerLinkPolicy.TryBuildWatchLink(trailerKey);
        ArgumentNullException.ThrowIfNull(episodes);
        ArgumentNullException.ThrowIfNull(watchStates);

        PersonalActions.Apply(personalState ?? PersonalState.Empty(ContentKey.ForTitle(item.Id)));
        Seasons =
        [
            .. NextEpisodePolicy.Order(episodes)
                .GroupBy(entry => entry.SeasonNumber)
                .Select(group => new SeasonViewModel(
                    group.Key,
                    [
                        .. group.Select(entry => new EpisodeRowViewModel(
                            entry,
                            watchStates.GetValueOrDefault(ContentKey.ForEpisode(entry.ShowId, entry.Id)),
                            item.Title))
                    ]))
        ];
        SelectedSeason = Seasons.Count > 0 ? Seasons[0] : null;
        foreach (var name in new[]
        {
            nameof(HasSeasonChoice),
            nameof(Title),
            nameof(Initials),
            nameof(Overview),
            nameof(HasOverview),
            nameof(Year),
            nameof(YearText),
            nameof(HasYear),
            nameof(IsAvailable),
            nameof(IsEmpty),
            nameof(HasSeasons),
            nameof(HasTrailerLink),
            nameof(MetaText),
            nameof(HasMeta),
            nameof(EpisodeTotal),
            nameof(WatchedCount),
            nameof(ProgressText),
            nameof(WatchedFraction),
            nameof(HasProgress),
            nameof(NextEpisode),
            nameof(HasNextEpisode),
            nameof(NextEpisodeLabel),
            nameof(NextEpisodeSubText),
        })
        {
            OnPropertyChanged(name);
        }

        (OpenTrailerLinkCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ContinueCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    /// <summary>A number and the word that follows it, in whichever language is loaded.</summary>
    private static string Count(int value, string key, string fallback) =>
        string.Create(CultureInfo.CurrentCulture, $"{value} {ShowText.Resource(key, fallback)}");

    private Task PlayAsync(EpisodeRowViewModel episode) => _onPlay is null || !episode.IsPlayable
        ? Task.CompletedTask
        // No position of its own: an episode left half-watched is resumed, and that is the resume
        // policy's decision to make rather than this row's.
        : _onPlay(new PlayDetailsRequest(
            episode.MediaFileId,
            StartPosition: null,
            Title,
            EpisodeLine(episode)));

    /// <summary>«T01·E02 · La marea baja», which is what the player's header writes under the series.</summary>
    private static string EpisodeLine(EpisodeRowViewModel episode) =>
        ShowText.Resource("ShowNextEpisodeCode", "S{0}·E{1}")
            .Replace("{0}", episode.SeasonNumber.ToString("D2", CultureInfo.CurrentCulture), StringComparison.Ordinal)
            .Replace("{1}", episode.EpisodeNumber.ToString("D2", CultureInfo.CurrentCulture), StringComparison.Ordinal)
        + " · "
        + episode.EpisodeTitle;

    private Task OpenTrailerLinkAsync() =>
        _onOpenTrailerLink is null || _trailerLink is not { Length: > 0 } link
            ? Task.CompletedTask
            : _onOpenTrailerLink(link);

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
