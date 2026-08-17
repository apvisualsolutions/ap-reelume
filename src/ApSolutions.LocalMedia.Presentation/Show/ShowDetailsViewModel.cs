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
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand PlayEpisodeCommand { get; }

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

    public IReadOnlyList<SeasonViewModel> Seasons
    {
        get => _seasons;
        private set => SetField(ref _seasons, value);
    }

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
                            watchStates.GetValueOrDefault(ContentKey.ForEpisode(entry.ShowId, entry.Id))))
                    ]))
        ];
        foreach (var name in new[]
        {
            nameof(Title),
            nameof(Overview),
            nameof(HasOverview),
            nameof(Year),
            nameof(YearText),
            nameof(HasYear),
            nameof(IsAvailable),
            nameof(IsEmpty),
            nameof(HasSeasons),
            nameof(HasTrailerLink),
        })
        {
            OnPropertyChanged(name);
        }

        (OpenTrailerLinkCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private Task PlayAsync(EpisodeRowViewModel episode) => _onPlay is null || !episode.IsPlayable
        ? Task.CompletedTask
        // No position of its own: an episode left half-watched is resumed, and that is the resume
        // policy's decision to make rather than this row's.
        : _onPlay(new PlayDetailsRequest(episode.MediaFileId, StartPosition: null));

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
