// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Metadata;

public sealed class MetadataEditorViewModel : INotifyPropertyChanged
{
    private readonly UpdateMetadata _updateMetadata;
    private readonly RefreshMetadata _refreshMetadata;
    private readonly Func<Task>? _onApplied;
    private CatalogMetadata _catalog;
    private string _title = string.Empty;
    private string? _originalTitle;
    private string? _overview;
    private string? _releaseYear;
    private string? _genres;
    private string? _posterPath;
    private string? _backdropPath;
    private string? _personalCover;
    private int _coverSourceIndex;
    private bool _lockTitle;
    private bool _lockOriginalTitle;
    private bool _lockOverview;
    private bool _lockReleaseYear;
    private bool _lockGenres;
    private bool _lockPosterPath;
    private bool _lockBackdropPath;
    private bool _hasConflict;
    private bool _isUnidentified;
    private bool _hasNoProviderAnswer;

    /// <param name="onApplied">
    /// Told that a write landed, so whatever is drawing this title behind the editor can read it
    /// again. Optional, and a test that only asks what the editor holds hands in nothing.
    /// </param>
    /// <remarks>
    /// <b><paramref name="onApplied"/> is what makes a saved cover appear.</b> Closing the editor
    /// drops both surfaces and reloads nothing, so the card underneath is the one built when the
    /// title was opened — which meant that until 2026-09-04 somebody could choose a cover, be told
    /// «Portada puesta», save, and watch nothing change until they left the title and came back. The
    /// editor does not reach for the card itself: it says a write landed, and composition decides
    /// what that is worth re-reading.
    /// </remarks>
    public MetadataEditorViewModel(
        CatalogMetadata catalog,
        UpdateMetadata updateMetadata,
        RefreshMetadata refreshMetadata,
        ArtworkPickerViewModel artworkPicker,
        Func<Task>? onApplied = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _updateMetadata = updateMetadata ?? throw new ArgumentNullException(nameof(updateMetadata));
        _refreshMetadata = refreshMetadata ?? throw new ArgumentNullException(nameof(refreshMetadata));
        ArtworkPicker = artworkPicker ?? throw new ArgumentNullException(nameof(artworkPicker));
        _onApplied = onApplied;

        // The editor listens rather than the picker reaching back into it: a cover that has been
        // imported is a new path for the poster field and a lock on it, and both of those are this
        // view model's to write. Without the lock the next provider refresh would put the
        // provider's artwork back over the one somebody chose, which is the whole point of LIB-011.
        ArtworkPicker.PropertyChanged += OnPickerChanged;
        ChooseCoverSourceCommand = new ChooseCoverSource(this);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        RefreshProviderCommand = new AsyncRelayCommand(() => RefreshAsync(restoreProviderFields: false));
        RestoreProviderCommand = new AsyncRelayCommand(() => RefreshAsync(restoreProviderFields: true));
        ApplyCatalog(catalog);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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

    /// <summary>
    /// The picked cover's file name, kept apart from <see cref="PosterPath"/> (LIB-021, ADR-0009).
    /// Choosing a cover fills this and nothing else, so the provider's poster and its lock stay as
    /// they were.
    /// </summary>
    public string? PersonalCover { get => _personalCover; private set => SetField(ref _personalCover, value); }

    /// <summary>
    /// Which cover wins for this one title: <c>0</c> follows the general order, and the rest name one
    /// origin, in the order <see cref="CoverOrigin"/> declares them (LIB-021, ADR-0009 decision 4).
    /// </summary>
    /// <remarks>
    /// One drop-down rather than a second reorderer. Picking an origin puts it first and leaves the
    /// rest as they were, which is what «override the general order for this title» means; a full
    /// reorderer here would teach the same idea twice on a page that already has eleven fields.
    /// </remarks>
    public int CoverSourceIndex
    {
        get => _coverSourceIndex;
        set
        {
            if (!SetField(ref _coverSourceIndex, value)) { return; }

            foreach (var choice in CoverSourceChoices)
            {
                choice.IsSelected = choice.Index == value;
            }
        }
    }

    /// <summary>
    /// The choices, in the order they are offered: the general order first and then one per origin.
    /// </summary>
    /// <remarks>
    /// A row of options and not a drop-down, which is the shape the audio device list already has
    /// here. A drop-down keeps its choices inside a popup, and nothing inside one can be clicked by
    /// the autonomous walk — so the one control on this page nobody could press would have been the
    /// one added last.
    /// </remarks>
    public IReadOnlyList<CoverSourceOption> CoverSourceChoices { get; } =
    [
        new(0, "MetadataCoverSourceDefault"),
        .. Enum.GetValues<CoverOrigin>().Select(origin => new CoverSourceOption(1 + (int)origin, "CoverOrigin" + origin)),
    ];

    /// <summary>Picking one of the choices above.</summary>
    public ICommand ChooseCoverSourceCommand { get; }

    public bool LockTitle { get => _lockTitle; set => SetField(ref _lockTitle, value); }

    public bool LockOriginalTitle { get => _lockOriginalTitle; set => SetField(ref _lockOriginalTitle, value); }

    public bool LockOverview { get => _lockOverview; set => SetField(ref _lockOverview, value); }

    public bool LockReleaseYear { get => _lockReleaseYear; set => SetField(ref _lockReleaseYear, value); }

    public bool LockGenres { get => _lockGenres; set => SetField(ref _lockGenres, value); }

    public bool LockPosterPath { get => _lockPosterPath; set => SetField(ref _lockPosterPath, value); }

    public bool LockBackdropPath { get => _lockBackdropPath; set => SetField(ref _lockBackdropPath, value); }

    public bool HasConflict { get => _hasConflict; private set => SetField(ref _hasConflict, value); }

    /// <summary>Nobody has identified this title, so there is nothing to refresh it against.</summary>
    public bool IsUnidentified { get => _isUnidentified; private set => SetField(ref _isUnidentified, value); }

    /// <summary>It is identified, but the provider had no answer to give right now.</summary>
    public bool HasNoProviderAnswer
    {
        get => _hasNoProviderAnswer;
        private set => SetField(ref _hasNoProviderAnswer, value);
    }

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
                BackdropPath)
            {
                PersonalCover = PersonalCover,

                // The general order is an empty list and not null: null means «leave it as it is»,
                // so without the second sentinel there would be no way back once an origin was
                // picked — the hole PersonalCover above still has.
                CoverOrder = CoverSourceIndex <= 0
                    ? []
                    : CoverOrderPolicy.WithFirst(
                        (CoverOrigin)(CoverSourceIndex - 1),
                        CoverOrderPolicy.TryParse(_catalog.Metadata.CoverOrder, out var stored) ? stored : null),
            },
            GetLockedFields(),
            _catalog.Revision)).ConfigureAwait(true);
        await ApplyResultAsync(result).ConfigureAwait(true);
    }

    private async Task RefreshAsync(bool restoreProviderFields)
    {
        var result = await _refreshMetadata.ExecuteAsync(new RefreshMetadataCommand(
            _catalog.TitleId,
            _catalog.Revision,
            restoreProviderFields)).ConfigureAwait(true);
        await ApplyResultAsync(result).ConfigureAwait(true);
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

    private async Task ApplyResultAsync(MetadataWriteResult result)
    {
        HasConflict = result.Outcome == MetadataWriteOutcome.Conflict;

        // A refresh that found nothing to refresh against says so. Leaving it silent is what made
        // both provider buttons look broken: they were pressed, nothing happened, and nothing
        // explained why.
        IsUnidentified = result.Outcome == MetadataWriteOutcome.NotIdentified;
        HasNoProviderAnswer = result.Outcome == MetadataWriteOutcome.Unavailable;
        if (result.Outcome != MetadataWriteOutcome.Applied || result.Catalog is null)
        {
            return;
        }

        ApplyCatalog(result.Catalog);

        // Only a write that landed. A conflict, an unidentified title and a provider with no answer
        // all leave the stored row exactly as it was, so re-reading it would redraw the same card
        // and teach whoever is watching that the button does something when it did not.
        if (_onApplied is { } notify)
        {
            await notify().ConfigureAwait(true);
        }
    }

    private void OnPickerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ArtworkPickerViewModel.SelectedPersonalPath)
            || string.IsNullOrWhiteSpace(ArtworkPicker.SelectedPersonalPath))
        {
            return;
        }

        // Its own field and nothing else (LIB-021). Until 2026-09-18 this wrote the chosen path into
        // PosterPath and set its lock, because the lock was all that kept the next refresh from
        // overwriting the choice — and restoring the provider's fields clears every lock.
        PersonalCover = PersonalCoverPathPolicy.TryGetCoverFileName(ArtworkPicker.SelectedPersonalPath);
    }

    private void ApplyCatalog(CatalogMetadata catalog)
    {
        _catalog = catalog;

        // The picker needs to know which title a chosen cover belongs to, and it learns it here
        // rather than in the constructor: the editor is reloaded with a new catalogue after every
        // save and every refresh, and a target left behind would file the next cover under the last
        // title somebody edited.
        ArtworkPicker.Target = catalog.TitleId;
        Title = catalog.Metadata.Title;
        OriginalTitle = catalog.Metadata.OriginalTitle;
        Overview = catalog.Metadata.Overview;
        ReleaseYear = catalog.Metadata.ReleaseYear?.ToString(CultureInfo.InvariantCulture);
        Genres = string.Join(", ", catalog.Metadata.Genres);
        PosterPath = catalog.Metadata.PosterPath;
        BackdropPath = catalog.Metadata.BackdropPath;
        PersonalCover = catalog.Metadata.PersonalCover;

        // An order stored for this title names its winner; one that names nothing valid is not
        // obeyed and not fatal, and opens on the general order like a title that never set one.
        CoverSourceIndex = CoverOrderPolicy.TryParse(catalog.Metadata.CoverOrder, out var forThisTitle)
            ? 1 + (int)forThisTitle[0]
            : 0;
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

    private sealed class ChooseCoverSource(MetadataEditorViewModel owner) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => parameter is CoverSourceOption;

        public void Execute(object? parameter)
        {
            if (parameter is CoverSourceOption option)
            {
                owner.CoverSourceIndex = option.Index;
            }
        }
    }
}

/// <summary>
/// One of the places this title's cover may be taken from, as a row that can be picked (LIB-021).
/// </summary>
/// <param name="index">
/// <c>0</c> for the general order, and <c>1 + (int)origin</c> for each origin — the same numbering
/// <see cref="MetadataEditorViewModel.CoverSourceIndex"/> holds.
/// </param>
/// <param name="nameKey">
/// The resource key of its name, not the words: the language changes while the application runs.
/// </param>
public sealed class CoverSourceOption(int index, string nameKey) : INotifyPropertyChanged
{
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Index { get; } = index;

    public string NameKey { get; } = nameKey ?? throw new ArgumentNullException(nameof(nameKey));

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) { return; }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
