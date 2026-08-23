// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Show;

namespace ApSolutions.LocalMedia.Presentation.Library;

public enum LibrarySurface
{
    Browse,
    MovieDetails,
    ShowDetails,
}

public sealed class LibraryViewModel : INotifyPropertyChanged
{
    private readonly ICatalogQueryService _queryService;
    private readonly RelayCommand _back;
    private readonly AsyncRelayCommand _clearSearch;
    private readonly AsyncRelayCommand _clearFilters;
    private IReadOnlyList<CatalogItemViewModel> _items = [];
    private IReadOnlyList<IReadOnlyList<CatalogItemViewModel>> _rows = [];
    private int _columns = 1;
    private string? _search;
    private CatalogFilter _filters;
    private CatalogSort _sort;
    private LibrarySurface _surface;
    private CatalogItemViewModel? _selectedItem;
    private TitleId? _scrollAnchorId;
    private string? _nextCursor;
    private bool _hasLoaded;

    public LibraryViewModel(
        ICatalogQueryService queryService,
        MovieDetailsViewModel? movieDetails = null,
        ShowDetailsViewModel? showDetails = null,
        ScanProgressViewModel? scanProgress = null)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        MovieDetails = movieDetails ?? new MovieDetailsViewModel();
        ShowDetails = showDetails ?? new ShowDetailsViewModel();
        ScanProgress = scanProgress ?? new ScanProgressViewModel();
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None));
        LoadMoreCommand = new AsyncRelayCommand(() => LoadMoreAsync(CancellationToken.None));
        OpenDetailsCommand = new RelayCommand(
            parameter => OpenDetails((CatalogItemViewModel)parameter!),
            parameter => parameter is CatalogItemViewModel);
        _clearSearch = new AsyncRelayCommand(
            () => ClearSearchAsync(CancellationToken.None),
            () => !string.IsNullOrWhiteSpace(Search));
        ClearSearchCommand = _clearSearch;
        _back = new RelayCommand(_ => BackToLibrary(), _ => Surface != LibrarySurface.Browse);
        BackCommand = _back;
        SelectTypeCommand = new AsyncRelayCommand(
            parameter =>
            {
                TypeFilter = (CatalogFilter)parameter!;
                return LoadAsync(CancellationToken.None);
            },
            parameter => parameter is CatalogFilter);
        _clearFilters = new AsyncRelayCommand(() =>
        {
            _search = null;
            _filters = CatalogFilter.None;
            _sort = CatalogSort.Title;
            OnPropertyChanged(nameof(Search));
            OnPropertyChanged(nameof(Filters));
            OnPropertyChanged(nameof(Sort));
            OnPropertyChanged(nameof(TypeFilter));
            OnPropertyChanged(nameof(StatusFilter));
            OnPropertyChanged(nameof(IsEveryKind));
            OnPropertyChanged(nameof(IsMoviesOnly));
            OnPropertyChanged(nameof(IsShowsOnly));
            OnPropertyChanged(nameof(EveryKindStateCue));
            OnPropertyChanged(nameof(MoviesOnlyStateCue));
            OnPropertyChanged(nameof(ShowsOnlyStateCue));
            OnPropertyChanged(nameof(IsFiltersDirty));
            _clearSearch.RaiseCanExecuteChanged();
            return LoadAsync(CancellationToken.None);
        });
        ClearFiltersCommand = _clearFilters;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<CatalogItemViewModel> Items
    {
        get => _items;
        private set
        {
            if (SetField(ref _items, value))
            {
                OnPropertyChanged(nameof(IsSearchWithoutResults));
                OnPropertyChanged(nameof(IsLibraryEmpty));
                OnPropertyChanged(nameof(ItemCount));
                Regroup();
            }
        }
    }

    /// <summary>
    /// How many cards fit across, which is the one thing about this grid only the view can know.
    /// </summary>
    /// <remarks>
    /// The view measures itself and sets this; nothing here is in pixels. Below one it is one, so a
    /// window narrower than a single card still shows a column instead of dividing by zero.
    /// </remarks>
    public int Columns
    {
        get => _columns;
        set
        {
            if (SetField(ref _columns, Math.Max(1, value)))
            {
                Regroup();
            }
        }
    }

    /// <summary>
    /// The catalogue in rows of <see cref="Columns"/>, which is what makes the grid virtualise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured on 2026-08-22 over ten thousand cards in a 1600 x 1000 window, in Release:
    /// a <c>WrapPanel</c> takes <b>4559 ms</b> and keeps <b>10 000</b> cards alive, because it
    /// virtualises nothing; these rows inside a <c>VirtualizingStackPanel</c> take <b>6 ms</b> and
    /// keep <b>36</b>. That is 760x the time and 278x the live controls, and it is why the decision
    /// not to build a fluid grid is reversed: what was missing was never a control Avalonia lacks,
    /// it was grouping the items before handing them to the one it has.
    /// </para>
    /// <para>
    /// Regrouping on every resize was measured too, and it is cheap: 10 ms to seven columns, 3 ms to
    /// five, 12 ms back to nine, over the same ten thousand.
    /// </para>
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<CatalogItemViewModel>> Rows
    {
        get => _rows;
        private set => SetField(ref _rows, value);
    }

    private void Regroup() => Rows = [.. Items.Chunk(Columns)];

    public string? Search
    {
        get => _search;
        set
        {
            if (SetField(ref _search, value))
            {
                OnPropertyChanged(nameof(IsSearchWithoutResults));
                OnPropertyChanged(nameof(IsLibraryEmpty));

                // The clear button is bound to a predicate over this, so it has to be told: a command
                // that never announces is asked once at construction and keeps that first answer,
                // which is the defect ARQ-004 went through twenty-four classes to remove.
                _clearSearch.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsFiltersDirty));
            }
        }
    }

    public CatalogFilter Filters
    {
        get => _filters;
        set
        {
            if (SetField(ref _filters, value))
            {
                OnPropertyChanged(nameof(IsSearchWithoutResults));
                OnPropertyChanged(nameof(IsLibraryEmpty));
                OnPropertyChanged(nameof(TypeFilter));
                OnPropertyChanged(nameof(StatusFilter));
                OnPropertyChanged(nameof(IsEveryKind));
                OnPropertyChanged(nameof(IsMoviesOnly));
                OnPropertyChanged(nameof(IsShowsOnly));
                OnPropertyChanged(nameof(EveryKindStateCue));
                OnPropertyChanged(nameof(MoviesOnlyStateCue));
                OnPropertyChanged(nameof(ShowsOnlyStateCue));
                OnPropertyChanged(nameof(IsFiltersDirty));
            }
        }
    }

    /// <summary>The two bits the kind pills own, and nothing else.</summary>
    private const CatalogFilter KindMask = CatalogFilter.Movie | CatalogFilter.Show;

    /// <summary>
    /// Which kind the three pills are on: <see cref="CatalogFilter.None"/> for all of them.
    /// </summary>
    /// <remarks>
    /// Split from <see cref="StatusFilter"/> rather than sharing one property, because the prototype
    /// asks for two controls over one field and the repository already supports it: the query builder
    /// reads <c>Movie</c> and <c>Show</c> for the kind predicate and ANDs the rest in separately, so
    /// "films I have not started" was always expressible and no view ever asked for it. A single
    /// <c>ComboBox</c> bound to the whole flags value is what made the two exclusive — pressing
    /// Películas would have cleared En curso, which is a control undoing another one.
    /// </remarks>
    public CatalogFilter TypeFilter
    {
        get => _filters & KindMask;
        set => Filters = (_filters & ~KindMask) | (value & KindMask);
    }

    /// <summary>Everything the kind pills do not own: availability, progress, and the personal marks.</summary>
    /// <remarks>
    /// Setting it re-runs the query, because the drop-down applies as a choice is made — the Apply
    /// button this replaced was a control whose whole job was repeating what this one had already
    /// said. Through <see cref="RefreshCommand"/> rather than a bare call, so a failure lands in the
    /// command's catch instead of on the application (ARQ-004). The binding pushing the current
    /// value back at attach is a no-op: same value, no change, no query. And only once something
    /// has loaded: before the first <see cref="LoadAsync"/> there is nothing to re-run, and a
    /// constructor initializer that queried would eat a page the host was about to ask for.
    /// </remarks>
    public CatalogFilter StatusFilter
    {
        get => _filters & ~KindMask;
        set
        {
            var next = (_filters & KindMask) | (value & ~KindMask);
            if (next != _filters)
            {
                Filters = next;
                if (_hasLoaded)
                {
                    RefreshCommand.Execute(null);
                }
            }
        }
    }

    public bool IsEveryKind => TypeFilter is CatalogFilter.None or KindMask;

    public bool IsMoviesOnly => TypeFilter == CatalogFilter.Movie;

    public bool IsShowsOnly => TypeFilter == CatalogFilter.Show;

    /// <summary>
    /// The selected pill says so twice: the accent fill, and this glyph.
    /// </summary>
    /// <remarks>
    /// The same pair the theme and language pills already spend, and the reason is the same one the
    /// four themes force — in either high contrast dictionary the accent fill and the resting fill are
    /// the same white or the same black, so a pill that said "selected" in colour alone would say
    /// nothing at all there.
    /// </remarks>
    public string EveryKindStateCue => Cue(IsEveryKind);

    public string MoviesOnlyStateCue => Cue(IsMoviesOnly);

    public string ShowsOnlyStateCue => Cue(IsShowsOnly);

    private static string Cue(bool selected) => selected ? "●" : "○";

    /// <summary>Applies as it is chosen, same shape and same reason as <see cref="StatusFilter"/>.</summary>
    public CatalogSort Sort
    {
        get => _sort;
        set
        {
            if (SetField(ref _sort, value))
            {
                OnPropertyChanged(nameof(IsFiltersDirty));
                if (_hasLoaded)
                {
                    RefreshCommand.Execute(null);
                }
            }
        }
    }

    public LibrarySurface Surface
    {
        get => _surface;
        private set
        {
            if (SetField(ref _surface, value))
            {
                OnPropertyChanged(nameof(IsBrowsing));
                OnPropertyChanged(nameof(IsMovieDetails));
                OnPropertyChanged(nameof(IsShowDetails));

                // Both detail branches sit in the visual tree from the start, so the Back button
                // is asked once — while the surface is still Browse — and the answer is no. Without
                // this it renders enabled=False forever, which is what the walk measured.
                _back.RaiseCanExecuteChanged();
            }
        }
    }

    public CatalogItemViewModel? SelectedItem
    {
        get => _selectedItem;
        private set => SetField(ref _selectedItem, value);
    }

    public TitleId? ScrollAnchorId
    {
        get => _scrollAnchorId;
        private set => SetField(ref _scrollAnchorId, value);
    }

    public bool IsBrowsing => Surface == LibrarySurface.Browse;

    public bool IsMovieDetails => Surface == LibrarySurface.MovieDetails;

    public bool IsShowDetails => Surface == LibrarySurface.ShowDetails;

    public bool HasMore => _nextCursor is not null;

    /// <summary>
    /// How many cards the grid is holding, which is what the header counts.
    /// </summary>
    /// <remarks>
    /// The loaded page and not the catalogue, which is what the prototype counts too — its header
    /// reads the length of the tile list. A catalogue-wide total would need a count query that
    /// <see cref="ICatalogQueryService"/> does not offer, and inventing one to put a bigger number
    /// beside a smaller grid would be a header describing something the screen is not showing.
    /// </remarks>
    public int ItemCount => Items.Count;

    /// <summary>
    /// The query narrowed the catalogue and nothing came back.
    /// </summary>
    /// <remarks>
    /// Told apart from an empty library on purpose, and the empty library is not this view's sentence
    /// anyway — <c>ShellView</c> paints it. Without the narrowing test this would claim the library is
    /// empty every time somebody mistypes a title, which is false and unhelpful at the same time.
    /// </remarks>
    public bool IsSearchWithoutResults =>
        Items.Count == 0 && (!string.IsNullOrWhiteSpace(Search) || Filters != CatalogFilter.None);

    /// <summary>
    /// Nothing in the library and nothing narrowing it: the fourth of §4's four states.
    /// </summary>
    /// <remarks>
    /// The complement of <see cref="IsSearchWithoutResults"/> over an empty grid, and the two must
    /// never both be true: an empty grid under a filter is an answer about the filter, an empty grid
    /// under nothing is an answer about the library.
    /// </remarks>
    public bool IsLibraryEmpty =>
        Items.Count == 0 && string.IsNullOrWhiteSpace(Search) && Filters == CatalogFilter.None;

    public ICommand RefreshCommand { get; }

    /// <summary>Empties the search box and asks again, so getting back to everything is one press.</summary>
    public ICommand ClearSearchCommand { get; }

    /// <summary>
    /// Puts the whole row back where it starts — search, kind, status and order — in one press.
    /// </summary>
    /// <remarks>
    /// The prototype's «Quitar filtros», and it exists only while something is narrowed
    /// (<see cref="IsFiltersDirty"/>): a reset that is always on offer reads as a control with a
    /// job, and its job would be nothing. The fields are written directly and the query runs once
    /// at the end, because going through the setters would run it up to three times.
    /// </remarks>
    public ICommand ClearFiltersCommand { get; }

    /// <summary>Whether anything narrows the grid right now: search, a kind, a status, or an order that is not the default.</summary>
    public bool IsFiltersDirty =>
        !string.IsNullOrWhiteSpace(Search) || Filters != CatalogFilter.None || Sort != CatalogSort.Title;

    public ICommand LoadMoreCommand { get; }

    public ICommand OpenDetailsCommand { get; }

    /// <summary>
    /// The three kind pills, which narrow and re-run in one press.
    /// </summary>
    /// <remarks>
    /// It queries rather than only setting the field, because the prototype has no Apply beside the
    /// pills: pressing Películas is the whole gesture. The status and order drop-downs still wait for
    /// Apply, which is the shape this tree already had, and the two are told apart on screen by the
    /// pills being pills.
    /// </remarks>
    public ICommand SelectTypeCommand { get; }

    public ICommand BackCommand { get; }

    public MovieDetailsViewModel MovieDetails { get; }

    public ShowDetailsViewModel ShowDetails { get; }

    /// <summary>
    /// Scanning is the one job that runs for minutes. The library owns its announcement so the work
    /// is stated in words instead of only moving a bar.
    /// </summary>
    public ScanProgressViewModel ScanProgress { get; }

    /// <summary>
    /// Fills the detail surfaces for the item that was opened. The library itself reads nothing but
    /// the catalogue, so watch states, episodes, and versions stay the host's business.
    /// </summary>
    public Func<CatalogItemViewModel, Task>? DetailsLoader { get; set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var page = await _queryService.QueryAsync(
            CreateQuery(cursor: null),
            cancellationToken).ConfigureAwait(false);
        Items = page.Items.Select(item => new CatalogItemViewModel(item)).ToArray();
        _nextCursor = page.NextCursor;
        _hasLoaded = true;
        OnPropertyChanged(nameof(HasMore));
    }

    /// <summary>
    /// Clears the search and re-runs the query, which is the only thing that puts the results back.
    /// </summary>
    public async Task ClearSearchAsync(CancellationToken cancellationToken = default)
    {
        Search = null;
        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
    {
        if (_nextCursor is null)
        {
            return;
        }

        var page = await _queryService.QueryAsync(
            CreateQuery(_nextCursor),
            cancellationToken).ConfigureAwait(false);
        Items = Items.Concat(page.Items.Select(item => new CatalogItemViewModel(item))).ToArray();
        _nextCursor = page.NextCursor;
        OnPropertyChanged(nameof(HasMore));
    }

    /// <summary>
    /// Opens the card for one entry.
    /// <para>
    /// A title nobody has identified yet is one file with one path, so it gets the single-title card:
    /// the series card would show a season list it does not have and, with it, no way to play the
    /// file at all. That is what walking the real application found.
    /// </para>
    /// </summary>
    public void OpenDetails(CatalogItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        SelectedItem = item;
        ScrollAnchorId = item.Item.Id;
        Surface = item.Item.Kind == CatalogTitleKind.Show
            ? LibrarySurface.ShowDetails
            : LibrarySurface.MovieDetails;
    }

    /// <summary>
    /// Opens the details and fills them. Browse state — query, items, cursor and anchor — is kept, so
    /// coming back lands exactly where the person left.
    /// </summary>
    public async Task OpenDetailsAsync(CatalogItemViewModel item, CancellationToken cancellationToken = default)
    {
        OpenDetails(item);
        if (DetailsLoader is { } loader)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await loader(item).ConfigureAwait(false);
        }
    }

    public void BackToLibrary() => Surface = LibrarySurface.Browse;

    private CatalogQuery CreateQuery(string? cursor) => new(
        Search,
        Filters,
        Sort,
        PageSize: 50,
        Cursor: cursor);

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

    private sealed class RelayCommand(Action<object?> execute, Predicate<object?> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute(parameter);

        public void Execute(object? parameter) => execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
