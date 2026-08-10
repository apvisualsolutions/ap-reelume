// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
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
    private IReadOnlyList<CatalogItemViewModel> _items = [];
    private string? _search;
    private CatalogFilter _filters;
    private CatalogSort _sort;
    private LibrarySurface _surface;
    private CatalogItemViewModel? _selectedItem;
    private TitleId? _scrollAnchorId;
    private string? _nextCursor;

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
        RefreshCommand = new AsyncCommand(() => LoadAsync(CancellationToken.None));
        LoadMoreCommand = new AsyncCommand(() => LoadMoreAsync(CancellationToken.None));
        OpenDetailsCommand = new RelayCommand(
            parameter => OpenDetails((CatalogItemViewModel)parameter!),
            parameter => parameter is CatalogItemViewModel);
        BackCommand = new RelayCommand(_ => BackToLibrary(), _ => Surface != LibrarySurface.Browse);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<CatalogItemViewModel> Items
    {
        get => _items;
        private set => SetField(ref _items, value);
    }

    public string? Search
    {
        get => _search;
        set => SetField(ref _search, value);
    }

    public CatalogFilter Filters
    {
        get => _filters;
        set => SetField(ref _filters, value);
    }

    public CatalogSort Sort
    {
        get => _sort;
        set => SetField(ref _sort, value);
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

    public ICommand RefreshCommand { get; }

    public ICommand LoadMoreCommand { get; }

    public ICommand OpenDetailsCommand { get; }

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
        OnPropertyChanged(nameof(HasMore));
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
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => canExecute(parameter);

        public void Execute(object? parameter) => execute(parameter);
    }

    private sealed class AsyncCommand(Func<Task> execute) : ICommand
    {
        private bool _isRunning;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => !_isRunning;

        public async void Execute(object? parameter)
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                await execute().ConfigureAwait(true);
            }
            finally
            {
                _isRunning = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
