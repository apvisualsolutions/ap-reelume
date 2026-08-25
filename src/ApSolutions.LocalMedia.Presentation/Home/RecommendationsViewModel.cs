// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Library;

namespace ApSolutions.LocalMedia.Presentation.Home;

/// <summary>
/// The recommendations rail. Every card carries the reason keys behind it, so the "why" the person
/// reads is translated text rather than a number nobody can interpret.
/// </summary>
public sealed class RecommendationsViewModel : INotifyPropertyChanged
{
    private readonly GetRecommendations _getRecommendations;
    private readonly IRecommendationSettings _settings;
    private readonly Func<IReadOnlyList<TitleId>, CancellationToken, Task<IReadOnlyDictionary<TitleId, string>>> _titleLookup;
    private readonly Func<TitleId, Task>? _onOpenDetails;
    private IReadOnlyList<RecommendationItemViewModel> _items = [];

    public RecommendationsViewModel(
        GetRecommendations getRecommendations,
        IRecommendationSettings settings,
        Func<IReadOnlyList<TitleId>, CancellationToken, Task<IReadOnlyDictionary<TitleId, string>>>? titleLookup = null,
        Func<TitleId, Task>? onOpenDetails = null)
    {
        _getRecommendations = getRecommendations ?? throw new ArgumentNullException(nameof(getRecommendations));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _titleLookup = titleLookup
            ?? ((_, _) => Task.FromResult<IReadOnlyDictionary<TitleId, string>>(
                new Dictionary<TitleId, string>()));
        _onOpenDetails = onOpenDetails;
        ToggleCommand = new AsyncRelayCommand(
            () => SetEnabledAsync(!IsEnabled, CancellationToken.None));
        OpenItemDetailsCommand = new AsyncRelayCommand(
            parameter => OpenItemDetailsAsync(parameter as IRailCard),
            parameter => parameter is IRailCard);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ToggleCommand { get; }

    /// <summary>
    /// Opens the card of whichever suggestion is passed to it.
    /// </summary>
    /// <remarks>
    /// This rail drew the prototype's card and never gave it the thing the prototype does with it:
    /// `&lt;button onClick="r.open"&gt;` around the whole cover. Its own command rather than Home's,
    /// because the rail is mounted with itself as the data context and a binding that walked out to
    /// find Home would be a rail that only works in one place.
    /// </remarks>
    public ICommand OpenItemDetailsCommand { get; }

    public bool IsEnabled => _settings.IsEnabled;

    public bool IsDisabled => !IsEnabled;

    public IReadOnlyList<RecommendationItemViewModel> Items
    {
        get => _items;
        private set => SetField(ref _items, value);
    }

    public bool HasRecommendations => Items.Count > 0;

    /// <summary>
    /// On and ranked nothing. Kept apart from <see cref="IsDisabled"/> because the rail must never say
    /// there is nothing to suggest about a catalogue it did not read: switched off, the formula
    /// returns before asking the read model anything at all.
    /// </summary>
    public bool IsEmpty => IsEnabled && Items.Count == 0;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var results = await _getRecommendations
            .ExecuteAsync(new RecommendationOptions(IsEnabled, Limit: 20), cancellationToken)
            .ConfigureAwait(false);

        // The words behind the ids, asked for once and for the whole rail. A per-card lookup would
        // be twenty queries for one row of pictures, and a synchronous one over a connection would
        // be twenty blocking calls on the thread that draws them.
        var titles = await _titleLookup([.. results.Select(result => result.ContentId)], cancellationToken)
            .ConfigureAwait(false);
        Items =
        [
            .. results.Select(result => new RecommendationItemViewModel(
                result,
                titles.TryGetValue(result.ContentId, out var title) ? title : string.Empty)),
        ];
        OnPropertyChanged(nameof(HasRecommendations));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Stores the choice and reloads; switched off, the rail empties instead of hiding a result.</summary>
    public async Task SetEnabledAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        _settings.SetEnabled(isEnabled);
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(IsDisabled));
        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task OpenItemDetailsAsync(IRailCard? card)
    {
        if (card is not null && _onOpenDetails is { } open)
        {
            await open(card.TitleId).ConfigureAwait(true);
        }
    }

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

/// <summary>One suggestion, with the resource keys that explain it.</summary>
public sealed class RecommendationItemViewModel(Recommendation recommendation, string title)
    : IPosterCard, IRailCard
{
    private readonly Recommendation _recommendation = recommendation
        ?? throw new ArgumentNullException(nameof(recommendation));

    public TitleId ContentId => _recommendation.ContentId;

    /// <summary>
    /// The same id under the name a rail card is opened by.
    /// </summary>
    /// <remarks>
    /// This rail drew the prototype's card and left out the one thing the prototype does with it:
    /// press it. A suggestion nobody can open is a picture of a recommendation.
    /// </remarks>
    public TitleId TitleId => _recommendation.ContentId;

    public string Title { get; } = title ?? string.Empty;

    /// <summary>The same, for this rail: which rail, then the title. See the recently added card.</summary>
    public string OpenAccessibleName =>
        HomeViewModel.Word("RecommendationsHeading", "Suggestions") + " · " + Title;

    public double Score => _recommendation.Score;

    public string Initials => PosterInitials.From(Title);

    /// <summary>A suggestion is something not started, so there is nothing to draw.</summary>
    public bool HasKnownProgress => false;

    public double CompletedFraction => 0;

    /// <summary>
    /// Empty, and it is the same omission as the caption: a suggestion carries an id, a score and
    /// its reasons, and the kind would be a lookup this rail does not make.
    /// </summary>
    public string KindKey => string.Empty;

    public bool HasKind => false;

    public string MetaText => string.Empty;

    public bool HasMeta => false;

    /// <summary>A suggestion is something not started; that is what makes it a suggestion.</summary>
    public string StatusKey => "WatchStatusNotStarted";

    public string EpisodeCountText => string.Empty;

    public bool CountsEpisodes => false;

    public bool IsWatched => false;

    /// <summary>
    /// True, and it is an omission rather than a fact: this rail is built from scores and never
    /// reads the file behind them, so it cannot say a medium is missing and must not pretend to.
    /// </summary>
    public bool IsAvailable => true;

    /// <summary>Resource keys for the signals behind this suggestion, heaviest first.</summary>
    public IReadOnlyList<string> ReasonKeys { get; } =
        [.. recommendation.ReasonCodes.Select(reason => $"RecommendationReason{reason}")];

    public bool HasReasons => ReasonKeys.Count > 0;
}
