// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;

namespace ApSolutions.LocalMedia.Presentation.Home;

/// <summary>
/// What Home is asking to continue: which content, and what it is called.
/// </summary>
/// <remarks>
/// The key alone was enough while the player's header had nothing to write in it. It writes the
/// title and the line under it since 2026-08-25, and Home is the one surface that knows both without
/// reading anything — so it says them instead of making the composition look them up again.
/// </remarks>
/// <summary>
/// What Home asks the host to open, and from where.
/// </summary>
/// <remarks>
/// <c>FromStart</c> arrived on 2026-08-25 with the glyph beside Continue. The film card has carried
/// the pair since it was drawn — «en la tarjeta ancha del inicio justo después habría que poner el
/// icono de reproducir desde el inicio, como en la vista detalle del vídeo» — and the difference is
/// one flag rather than a second hook: what changes is the position the session opens at, and the
/// host is already the thing that reads it.
/// </remarks>
public sealed record HomeResumeRequest(
    ContentKey Content,
    string? Title,
    string? Subtitle,
    bool FromStart = false);

/// <summary>
/// The hybrid Home. Continue is the primary action whenever there is something worth continuing, and
/// the library stays one keystroke away either way, so the shortcut never becomes a detour.
/// </summary>
public sealed class HomeViewModel : INotifyPropertyChanged
{
    private readonly GetHome _getHome;
    private readonly INavigationService? _navigation;
    private readonly Func<HomeResumeRequest, Task>? _onResume;
    private readonly Func<TitleId, Task>? _onOpenDetails;
    private HomeSnapshot _snapshot = new(null, [], [], new LibrarySummary(0, 0, 0));
    private IReadOnlyList<InProgressItemViewModel> _inProgress = [];
    private IReadOnlyList<RecentlyAddedItemViewModel> _recentlyAdded = [];

    public HomeViewModel(
        GetHome getHome,
        INavigationService? navigation = null,
        Func<HomeResumeRequest, Task>? onResume = null,
        RecommendationsViewModel? recommendations = null,
        Func<TitleId, Task>? onOpenDetails = null)
    {
        _getHome = getHome ?? throw new ArgumentNullException(nameof(getHome));
        _navigation = navigation;
        _onResume = onResume;
        _onOpenDetails = onOpenDetails;
        Recommendations = recommendations;
        ResumeCommand = new AsyncRelayCommand(() => ResumeAsync(fromStart: false), () => HasResume);
        OpenResumeDetailsCommand = new AsyncRelayCommand(OpenResumeDetailsAsync, () => HasResume);
        OpenLibraryCommand = new AsyncRelayCommand(OpenLibraryAsync);
        RestartCommand = new AsyncRelayCommand(() => ResumeAsync(fromStart: true), () => HasResume);
        ResumeItemCommand = new AsyncRelayCommand(
            parameter => ResumeItemAsync(parameter as InProgressItemViewModel, fromStart: false),
            parameter => parameter is InProgressItemViewModel { IsAvailable: true });
        RestartItemCommand = new AsyncRelayCommand(
            parameter => ResumeItemAsync(parameter as InProgressItemViewModel, fromStart: true),
            parameter => parameter is InProgressItemViewModel { IsAvailable: true });
        OpenItemDetailsCommand = new AsyncRelayCommand(
            parameter => OpenItemDetailsAsync(parameter as IRailCard),
            parameter => parameter is IRailCard);
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ResumeCommand { get; }

    /// <summary>The hero's own «from the start», the pair of ResumeCommand.</summary>
    public ICommand RestartCommand { get; }

    /// <summary>
    /// The hero's second action: the card for what it offers to continue.
    /// </summary>
    /// <remarks>
    /// The prototype puts Detalles beside Continuar, and the note on the old one-button row said the
    /// second would arrive «the day the read model can answer for it». It answers now: the resume
    /// item carries its title id, and opening a card from a rail is what the library already knows
    /// how to do — so this is a hook the composition fills, exactly like onResume, rather than a
    /// second way into the same surface.
    /// </remarks>
    public ICommand OpenResumeDetailsCommand { get; }

    public ICommand OpenLibraryCommand { get; }

    /// <summary>
    /// Continues one card of the rail, which the prototype puts a button on.
    /// </summary>
    /// <remarks>
    /// The rail used to be a row of covers with no action on them at all: the card was a list item,
    /// and pressing it selected. The prototype gives every card the two things the hero has — resume
    /// at its own minute, and open its card — and these are those, taking the card as their
    /// parameter so one command serves the whole rail.
    /// </remarks>
    public ICommand ResumeItemCommand { get; }

    /// <summary>
    /// The same card, from zero: the glyph the film card has carried since it was drawn.
    /// </summary>
    /// <remarks>
    /// It sits beside Continue rather than replacing it, because they answer different questions and
    /// a person who wants one does not want the other. On a rail the two travel as one pair, which
    /// is why this takes the card as its parameter exactly as its neighbour does.
    /// </remarks>
    public ICommand RestartItemCommand { get; }

    /// <summary>Opens the card of whichever rail item is passed to it.</summary>
    public ICommand OpenItemDetailsCommand { get; }

    public ICommand RefreshCommand { get; }

    /// <summary>The suggestions rail, absent when the host does not compose one.</summary>
    public RecommendationsViewModel? Recommendations { get; }

    public bool HasRecommendationsRail => Recommendations is not null;

    /// <summary>True when Continue is a real action, which is what makes it the first focus.</summary>
    public bool HasResume => _snapshot.Resume is not null;

    public string ResumeTitle => _snapshot.Resume?.Title ?? string.Empty;

    /// <summary>Season and episode for a series, empty for a film; never a path or a file name.</summary>
    public string ResumeSubtitle => _snapshot.Resume is { SeasonNumber: { } season, EpisodeNumber: { } episode }
        ? string.Create(
            CultureInfo.CurrentCulture,
            $"T{season} · E{episode}{(_snapshot.Resume.EpisodeTitle is { Length: > 0 } title ? $" · {title}" : string.Empty)}")
        : string.Empty;

    public bool HasResumeSubtitle => ResumeSubtitle.Length > 0;

    public double ResumeCompletedFraction => _snapshot.Resume?.CompletedFraction ?? 0;

    public string ResumeCompletedText => FormatPercentage(ResumeCompletedFraction);

    /// <summary>
    /// The hero's own line: «2019 · Drama · Misterio · quedan 1:09:00».
    /// </summary>
    /// <remarks>
    /// The prototype writes what this is and how much of it is left, and this said neither — it had
    /// a percentage beside the bar instead, which answers a different question. What is left is the
    /// one number somebody deciding whether to press Continue actually weighs.
    ///
    /// <para>
    /// Each piece is dropped when the catalogue has nothing to say, so a title with no year, no
    /// genre and no known length leaves no separators behind. For a series the season and episode
    /// take the place of the year, which is what the prototype does with its own.
    /// </para>
    /// </remarks>
    public string ResumeMetaText
    {
        get
        {
            if (_snapshot.Resume is not { ObservedDuration: { } duration } resume
                || duration <= resume.Position)
            {
                return ResumePlayerSubtitle;
            }

            var remaining = Remaining(duration - resume.Position);
            return ResumePlayerSubtitle is { Length: > 0 } known
                ? string.Join(" · ", known, remaining)
                : remaining;
        }
    }

    /// <summary>
    /// What this is, without how much of it is left: «T2 · E5 · Puerto de invierno», or «2024 ·
    /// Suspense» for something with no episode to name.
    /// </summary>
    /// <remarks>
    /// The player's header writes this under the title, which is what the prototype puts there, and
    /// the hero's own line is this plus what is left. Written once and used twice rather than
    /// composed twice: two copies of a rule about dropping empty pieces drift apart on the piece
    /// nobody tested.
    /// </remarks>
    public string ResumePlayerSubtitle
    {
        get
        {
            if (_snapshot.Resume is not { } resume)
            {
                return string.Empty;
            }

            var pieces = new List<string>();
            if (HasResumeSubtitle)
            {
                pieces.Add(ResumeSubtitle);
            }
            else if (resume.Year is { } year)
            {
                pieces.Add(year.ToString(CultureInfo.CurrentCulture));
            }

            if (resume.Genres is { Count: > 0 } genres)
            {
                pieces.AddRange(genres.Take(2));
            }

            return string.Join(" · ", pieces);
        }
    }

    public bool HasResumeMeta => ResumeMetaText.Length > 0;

    /// <summary>«Continuar · 49:00», which is the prototype's own label for this button.</summary>
    /// <remarks>
    /// The time is in the button because it is what the button does: it does not resume "the film",
    /// it resumes it at that minute, and a person who left off at 49:00 recognises the number before
    /// they read the word.
    /// </remarks>
    public string ResumeActionText => _snapshot.Resume is { } resume
        ? Resource("HomeResumeAction", "Continue") + " · " + PlaybackClock.Format(resume.Position)
        : Resource("HomeResumeAction", "Continue");

    private static string Remaining(TimeSpan left) =>
        Resource("HomeResumeRemaining", "left {0}").Replace(
            "{0}",
            PlaybackClock.Format(left),
            StringComparison.Ordinal);

    /// <summary>
    /// The words behind a key, resolved where the resources are.
    /// </summary>
    /// <remarks>
    /// Two of this model's strings are patterns rather than picks — «Continuar · 49:00» and «quedan
    /// 1:09:00» — so they are built here. The fallback matters: a headless test mounts this model
    /// without the string dictionaries, and a null would make the hero's button say the time alone.
    /// </remarks>
    private static string Resource(string key, string fallback) => Word(key, fallback);

    /// <summary>The words behind a key, for the three models on this page that build a label.</summary>
    internal static string Word(string key, string fallback) =>
        Avalonia.Application.Current is { } application
            && application.TryGetResource(key, application.ActualThemeVariant, out var value)
            && value is string text
                ? text
                : fallback;

    public IReadOnlyList<InProgressItemViewModel> InProgress
    {
        get => _inProgress;
        private set => SetField(ref _inProgress, value);
    }

    public IReadOnlyList<RecentlyAddedItemViewModel> RecentlyAdded
    {
        get => _recentlyAdded;
        private set => SetField(ref _recentlyAdded, value);
    }

    public bool HasInProgress => InProgress.Count > 0;

    public bool HasRecentlyAdded => RecentlyAdded.Count > 0;

    public int MovieCount => _snapshot.Library.MovieCount;

    public int ShowCount => _snapshot.Library.ShowCount;

    public int UnavailableCount => _snapshot.Library.UnavailableCount;

    public bool HasUnavailable => UnavailableCount > 0;

    /// <summary>The counts as one string, so the summary is never colour or position alone.</summary>
    public string LibrarySummaryText => string.Create(
        CultureInfo.CurrentCulture,
        $"{MovieCount} · {ShowCount}");

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Apply(await _getHome.ExecuteAsync(new GetHomeQuery(), cancellationToken).ConfigureAwait(false));
        if (Recommendations is { } recommendations)
        {
            await recommendations.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Applies an already-read snapshot; the view never decides what Home contains.</summary>
    public void Apply(HomeSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        InProgress = [.. snapshot.InProgress.Select(item => new InProgressItemViewModel(item))];
        RecentlyAdded = [.. snapshot.RecentlyAdded.Select(item => new RecentlyAddedItemViewModel(item))];
        foreach (var name in new[]
        {
            nameof(HasResume),
            nameof(ResumeTitle),
            nameof(ResumeSubtitle),
            nameof(HasResumeSubtitle),
            nameof(ResumeCompletedFraction),
            nameof(ResumeCompletedText),
            nameof(ResumeMetaText),
            nameof(ResumePlayerSubtitle),
            nameof(HasResumeMeta),
            nameof(ResumeActionText),
            nameof(HasInProgress),
            nameof(HasRecentlyAdded),
            nameof(MovieCount),
            nameof(ShowCount),
            nameof(UnavailableCount),
            nameof(HasUnavailable),
            nameof(LibrarySummaryText),
        })
        {
            OnPropertyChanged(name);
        }

        (ResumeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RestartCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (OpenResumeDetailsCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    internal static string FormatPercentage(double fraction) =>
        Math.Round(fraction * 100).ToString("F0", CultureInfo.CurrentCulture);

    private Task ResumeAsync(bool fromStart = false) =>
        _snapshot.Resume is { } resume && _onResume is not null
            ? _onResume(new HomeResumeRequest(
                resume.Content,
                ResumeTitle,
                ResumePlayerSubtitle,
                fromStart))
            : Task.CompletedTask;

    private Task OpenLibraryAsync()
    {
        _navigation?.Navigate(AppRoute.Library);
        return Task.CompletedTask;
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

    private async Task ResumeItemAsync(InProgressItemViewModel? card, bool fromStart)
    {
        if (card is not null && _onResume is { } resume)
        {
            await resume(new HomeResumeRequest(card.Content, card.Title, card.RailSubtitle, fromStart))
                .ConfigureAwait(true);
        }
    }

    private async Task OpenItemDetailsAsync(IRailCard? card)
    {
        if (card is not null && _onOpenDetails is { } open)
        {
            await open(card.TitleId).ConfigureAwait(true);
        }
    }

    private async Task OpenResumeDetailsAsync()
    {
        if (_snapshot.Resume is { } resume && _onOpenDetails is { } open)
        {
            await open(resume.TitleId).ConfigureAwait(true);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>One card of the in-progress rail. An unreachable file is shown, never quietly dropped.</summary>
public sealed class InProgressItemViewModel(InProgressItem item) : IPosterCard, IRailCard
{
    private readonly InProgressItem _item = item ?? throw new ArgumentNullException(nameof(item));

    public ContentKey Content => _item.Content;

    public TitleId TitleId => _item.TitleId;

    public string Title => _item.Title;

    /// <summary>Season and episode for a series, empty for a film; never a path or a file name.</summary>
    public string CaptionText => _item is { SeasonNumber: { } season, EpisodeNumber: { } episode }
        ? string.Create(CultureInfo.CurrentCulture, $"T{season} · E{episode}")
        : string.Empty;

    public bool HasCaption => CaptionText.Length > 0;

    public bool IsAvailable => _item.IsAvailable;

    public bool IsShow => _item.Kind == CatalogTitleKind.Show;

    public string Initials => PosterInitials.From(Title);

    /// <summary>The one rail that reads the fraction, which is why the card can draw a bar at all.</summary>
    public bool HasKnownProgress => true;

    public double CompletedFraction => _item.CompletedFraction;

    public string CompletedText => HomeViewModel.FormatPercentage(_item.CompletedFraction);

    /// <summary>
    /// The line under the title on this rail's card: «T02·E05 · Puerto de invierno» for an episode,
    /// «Película · 2020» for a film.
    /// </summary>
    /// <remarks>
    /// The kind is spelled out for a film because there is no episode to say instead, which is the
    /// prototype's own asymmetry: what the line answers is "which one of these am I looking at",
    /// and for an episode that is the number and its title.
    /// </remarks>
    public string RailSubtitle => IsShow
        ? CaptionText + (_item.EpisodeTitle is { Length: > 0 } title ? " · " + title : string.Empty)
        : string.Join(
            " · ",
            new[]
            {
                HomeViewModel.Word("CatalogKindMovie", "Film"),
                _item.Year?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
            }.Where(piece => piece.Length > 0));

    /// <summary>«Continuar · 17:00», the same label the hero's button carries.</summary>
    public string ResumeActionText =>
        HomeViewModel.Word("HomeResumeAction", "Continue") + " · " + PlaybackClock.Format(_item.Position);

    /// <summary>
    /// What a reader hears on this card's two buttons, which is the label and what it acts on.
    /// </summary>
    /// <remarks>
    /// A rail of three cards has three Continues and three Detalles on it, and named by their words
    /// alone they are three controls a screen reader cannot tell apart — measured by the audit the
    /// moment the buttons arrived. The title is what tells them apart, and it is the same thing the
    /// eye uses: the button sits under it.
    /// </remarks>
    public string ResumeAccessibleName => ResumeActionText + " · " + Title;

    /// <summary>The same, for the card's second button.</summary>
    public string DetailsAccessibleName =>
        HomeViewModel.Word("HomeResumeDetailsAction", "Details") + " · " + Title;

    /// <summary>And for the glyph between them, which is a sentence rather than a shape to a reader.</summary>
    public string RestartAccessibleName =>
        HomeViewModel.Word("HomeRestartAction", "Play from the start") + " · " + Title;

    public string KindKey => IsShow ? "CatalogKindShow" : "CatalogKindMovie";

    public bool HasKind => true;

    /// <summary>The season and episode this rail already had, in the line the card gives it.</summary>
    public string MetaText => CaptionText;

    public bool HasMeta => CaptionText.Length > 0;

    /// <summary>Every card on this rail is by definition part way through.</summary>
    public string StatusKey => "WatchStatusInProgress";

    /// <summary>This rail counts nothing: it shows one episode, not how many are left.</summary>
    public string EpisodeCountText => string.Empty;

    public bool CountsEpisodes => false;

    public bool IsWatched => false;
}

/// <summary>One card of the recently added rail.</summary>
public sealed class RecentlyAddedItemViewModel(RecentlyAddedItem item) : IPosterCard, IRailCard
{
    /// <summary>
    /// What a reader hears on the cover, which is the rail and then the title.
    /// </summary>
    /// <remarks>
    /// The title alone is not enough and the walk found it the first time both covers became
    /// buttons: one title can be on this rail and on the suggestions rail at the same moment, and
    /// two controls with one name is the very defect the rail's Continue and Detalles were renamed
    /// to avoid. The rail is what tells them apart, and it is the same thing the eye uses — the
    /// heading is directly above the cover.
    /// </remarks>
    public string OpenAccessibleName =>
        HomeViewModel.Word("HomeRecentlyAddedHeading", "Recently added") + " · " + Title;

    private readonly RecentlyAddedItem _item = item ?? throw new ArgumentNullException(nameof(item));

    public TitleId Id => _item.Id;

    public TitleId TitleId => _item.Id;

    public string Title => _item.Title;

    public string CaptionText => _item.Year is { } year
        ? year.ToString(CultureInfo.CurrentCulture)
        : string.Empty;

    public bool HasCaption => _item.Year is not null;

    public bool IsAvailable => _item.IsAvailable;

    public bool IsShow => _item.Kind == CatalogTitleKind.Show;

    public string Initials => PosterInitials.From(Title);

    /// <summary>What was just added has not been watched yet, so there is no bar to draw.</summary>
    public bool HasKnownProgress => false;

    public double CompletedFraction => 0;

    public string KindKey => IsShow ? "CatalogKindShow" : "CatalogKindMovie";

    public bool HasKind => true;

    public string MetaText => CaptionText;

    public bool HasMeta => CaptionText.Length > 0;

    /// <summary>Newly added and not yet started, which is the one thing this rail knows.</summary>
    public string StatusKey => "WatchStatusNotStarted";

    public string EpisodeCountText => string.Empty;

    public bool CountsEpisodes => false;

    public bool IsWatched => false;
}
