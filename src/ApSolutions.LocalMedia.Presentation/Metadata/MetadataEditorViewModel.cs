using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Presentation.Metadata;

public sealed class MetadataEditorViewModel : INotifyPropertyChanged
{
    private readonly UpdateMetadata _updateMetadata;
    private readonly RefreshMetadata _refreshMetadata;
    private CatalogMetadata _catalog;
    private string _title = string.Empty;
    private string? _originalTitle;
    private string? _overview;
    private string? _releaseYear;
    private string? _genres;
    private string? _posterPath;
    private string? _backdropPath;
    private bool _lockTitle;
    private bool _lockOriginalTitle;
    private bool _lockOverview;
    private bool _lockReleaseYear;
    private bool _lockGenres;
    private bool _lockPosterPath;
    private bool _lockBackdropPath;
    private bool _hasConflict;

    public MetadataEditorViewModel(
        CatalogMetadata catalog,
        UpdateMetadata updateMetadata,
        RefreshMetadata refreshMetadata,
        ArtworkPickerViewModel artworkPicker)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _updateMetadata = updateMetadata ?? throw new ArgumentNullException(nameof(updateMetadata));
        _refreshMetadata = refreshMetadata ?? throw new ArgumentNullException(nameof(refreshMetadata));
        ArtworkPicker = artworkPicker ?? throw new ArgumentNullException(nameof(artworkPicker));
        SaveCommand = new AsyncCommand(SaveAsync);
        RefreshProviderCommand = new AsyncCommand(() => RefreshAsync(restoreProviderFields: false));
        RestoreProviderCommand = new AsyncCommand(() => RefreshAsync(restoreProviderFields: true));
        ApplyCatalog(catalog);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MetadataDetails? ProviderMetadata { get; set; }

    public ArtworkPickerViewModel ArtworkPicker { get; }

    public ICommand SaveCommand { get; }

    public ICommand RefreshProviderCommand { get; }

    public ICommand RestoreProviderCommand { get; }

    public string Title { get => _title; set => SetField(ref _title, value); }

    public string? OriginalTitle { get => _originalTitle; set => SetField(ref _originalTitle, value); }

    public string? Overview { get => _overview; set => SetField(ref _overview, value); }

    public string? ReleaseYear { get => _releaseYear; set => SetField(ref _releaseYear, value); }

    public string? Genres { get => _genres; set => SetField(ref _genres, value); }

    public string? PosterPath { get => _posterPath; set => SetField(ref _posterPath, value); }

    public string? BackdropPath { get => _backdropPath; set => SetField(ref _backdropPath, value); }

    public bool LockTitle { get => _lockTitle; set => SetField(ref _lockTitle, value); }

    public bool LockOriginalTitle { get => _lockOriginalTitle; set => SetField(ref _lockOriginalTitle, value); }

    public bool LockOverview { get => _lockOverview; set => SetField(ref _lockOverview, value); }

    public bool LockReleaseYear { get => _lockReleaseYear; set => SetField(ref _lockReleaseYear, value); }

    public bool LockGenres { get => _lockGenres; set => SetField(ref _lockGenres, value); }

    public bool LockPosterPath { get => _lockPosterPath; set => SetField(ref _lockPosterPath, value); }

    public bool LockBackdropPath { get => _lockBackdropPath; set => SetField(ref _lockBackdropPath, value); }

    public bool HasConflict { get => _hasConflict; private set => SetField(ref _hasConflict, value); }

    private async Task SaveAsync()
    {
        var releaseYear = int.TryParse(ReleaseYear, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear)
            ? parsedYear
            : (int?)null;
        var genres = (Genres ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var result = await _updateMetadata.ExecuteAsync(new UpdateMetadataCommand(
            _catalog.TitleId,
            new MetadataFieldChanges(
                Title,
                OriginalTitle,
                Overview,
                releaseYear,
                genres,
                PosterPath,
                BackdropPath),
            GetLockedFields(),
            _catalog.Revision)).ConfigureAwait(true);
        ApplyResult(result);
    }

    private async Task RefreshAsync(bool restoreProviderFields)
    {
        if (ProviderMetadata is null)
        {
            return;
        }

        var result = await _refreshMetadata.ExecuteAsync(new RefreshMetadataCommand(
            _catalog.TitleId,
            ProviderMetadata,
            _catalog.Revision,
            restoreProviderFields)).ConfigureAwait(true);
        ApplyResult(result);
    }

    private HashSet<MetadataField> GetLockedFields()
    {
        var locked = new HashSet<MetadataField>();
        AddIfLocked(locked, MetadataField.Title, LockTitle);
        AddIfLocked(locked, MetadataField.OriginalTitle, LockOriginalTitle);
        AddIfLocked(locked, MetadataField.Overview, LockOverview);
        AddIfLocked(locked, MetadataField.ReleaseYear, LockReleaseYear);
        AddIfLocked(locked, MetadataField.Genres, LockGenres);
        AddIfLocked(locked, MetadataField.PosterPath, LockPosterPath);
        AddIfLocked(locked, MetadataField.BackdropPath, LockBackdropPath);
        return locked;
    }

    private static void AddIfLocked(HashSet<MetadataField> locked, MetadataField field, bool isLocked)
    {
        if (isLocked)
        {
            locked.Add(field);
        }
    }

    private void ApplyResult(MetadataWriteResult result)
    {
        HasConflict = result.Outcome == MetadataWriteOutcome.Conflict;
        if (result.Outcome == MetadataWriteOutcome.Applied && result.Catalog is not null)
        {
            ApplyCatalog(result.Catalog);
        }
    }

    private void ApplyCatalog(CatalogMetadata catalog)
    {
        _catalog = catalog;
        Title = catalog.Metadata.Title;
        OriginalTitle = catalog.Metadata.OriginalTitle;
        Overview = catalog.Metadata.Overview;
        ReleaseYear = catalog.Metadata.ReleaseYear?.ToString(CultureInfo.InvariantCulture);
        Genres = string.Join(", ", catalog.Metadata.Genres);
        PosterPath = catalog.Metadata.PosterPath;
        BackdropPath = catalog.Metadata.BackdropPath;
        LockTitle = catalog.Metadata.LockedFields.Contains(MetadataField.Title);
        LockOriginalTitle = catalog.Metadata.LockedFields.Contains(MetadataField.OriginalTitle);
        LockOverview = catalog.Metadata.LockedFields.Contains(MetadataField.Overview);
        LockReleaseYear = catalog.Metadata.LockedFields.Contains(MetadataField.ReleaseYear);
        LockGenres = catalog.Metadata.LockedFields.Contains(MetadataField.Genres);
        LockPosterPath = catalog.Metadata.LockedFields.Contains(MetadataField.PosterPath);
        LockBackdropPath = catalog.Metadata.LockedFields.Contains(MetadataField.BackdropPath);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
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
