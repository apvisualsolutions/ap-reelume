// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Offers to skip the range the playhead is inside. It exists only while the position is within a
/// range, which is what stops a skip button from lingering over the middle of an episode.
/// </summary>
public sealed class SkipMarkerViewModel : INotifyPropertyChanged
{
    private readonly Func<TimeSpan, Task>? _onSkip;
    private IntroMarker? _active;

    public SkipMarkerViewModel(Func<TimeSpan, Task>? onSkip = null)
    {
        _onSkip = onSkip;
        SkipCommand = new AsyncRelayCommand(SkipAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SkipCommand { get; }

    public bool IsVisible => _active is not null;

    public bool IsIntro => _active?.Kind == MarkerKind.Intro;

    public bool IsRecap => _active?.Kind == MarkerKind.Recap;

    public bool IsCredits => _active?.Kind == MarkerKind.Credits;

    /// <summary>Recomputes visibility from the ranges of the series and the current position.</summary>
    public void Apply(IReadOnlyList<IntroMarker> markers, TimeSpan position)
    {
        ArgumentNullException.ThrowIfNull(markers);
        _active = MarkerPolicy.FindActive(markers, position);
        foreach (var name in new[] { nameof(IsVisible), nameof(IsIntro), nameof(IsRecap), nameof(IsCredits) })
        {
            OnPropertyChanged(name);
        }
    }

    private async Task SkipAsync()
    {
        if (_active is not { } marker || _onSkip is not { } handler)
        {
            return;
        }

        await handler(MarkerPolicy.SkipTarget(marker)).ConfigureAwait(true);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Edits the ranges of one series. Every range it produces is manual: there is no detection here, and
/// no control offers any.
/// </summary>
public sealed class MarkerEditorViewModel : INotifyPropertyChanged
{
    private readonly Func<MarkerKind, TimeSpan, TimeSpan, Guid?, Task<SaveManualMarkerResult>>? _onSave;
    private readonly Func<Guid, Task<bool>>? _onDelete;
    private SeriesId _seriesId;
    private TimeSpan? _duration;
    private MarkerKind _selectedKind = MarkerKind.Intro;
    private double _startSeconds;
    private double _endSeconds;
    private IntroMarker? _selectedMarker;
    private bool _hasRangeError;
    private bool _hasOverlapError;

    public MarkerEditorViewModel(
        Func<MarkerKind, TimeSpan, TimeSpan, Guid?, Task<SaveManualMarkerResult>>? onSave = null,
        Func<Guid, Task<bool>>? onDelete = null)
    {
        _onSave = onSave;
        _onDelete = onDelete;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SaveCommand { get; }

    public ICommand DeleteCommand { get; }

    /// <summary>The kinds a person can choose; the same three the domain defines.</summary>
    public static IReadOnlyList<MarkerKind> Kinds { get; } = [.. Enum.GetValues<MarkerKind>()];

    public ObservableCollection<IntroMarker> Markers { get; } = [];

    public SeriesId SeriesId => _seriesId;

    public MarkerKind SelectedKind
    {
        get => _selectedKind;
        set => SetField(ref _selectedKind, value);
    }

    public double StartSeconds
    {
        get => _startSeconds;
        set => SetField(ref _startSeconds, value);
    }

    public double EndSeconds
    {
        get => _endSeconds;
        set => SetField(ref _endSeconds, value);
    }

    public IntroMarker? SelectedMarker
    {
        get => _selectedMarker;
        set => SetField(ref _selectedMarker, value);
    }

    /// <summary>True when the last attempt was refused because the range itself made no sense.</summary>
    public bool HasRangeError => _hasRangeError;

    /// <summary>True when the last attempt was refused because another range of the same kind was there.</summary>
    public bool HasOverlapError => _hasOverlapError;

    /// <summary>Shows the ranges of one series; changing series replaces the list entirely.</summary>
    public void Load(SeriesId seriesId, IReadOnlyList<IntroMarker> markers, TimeSpan? duration)
    {
        ArgumentNullException.ThrowIfNull(markers);
        _seriesId = seriesId;
        _duration = duration;
        Markers.Clear();
        foreach (var marker in markers)
        {
            Markers.Add(marker);
        }

        SelectedMarker = null;
        ClearErrors();
        OnPropertyChanged(nameof(SeriesId));
    }

    private async Task SaveAsync()
    {
        if (_onSave is not { } handler)
        {
            return;
        }

        var result = await handler(
                SelectedKind,
                TimeSpan.FromSeconds(StartSeconds),
                TimeSpan.FromSeconds(EndSeconds),
                SelectedMarker?.Id)
            .ConfigureAwait(true);
        _hasRangeError = result.Outcome == SaveMarkerOutcome.InvalidRange;
        _hasOverlapError = result.Outcome == SaveMarkerOutcome.Overlaps;
        if (result is { Outcome: SaveMarkerOutcome.Saved, Marker: { } marker })
        {
            var index = IndexOf(marker.Id);
            if (index >= 0)
            {
                Markers[index] = marker;
            }
            else
            {
                Markers.Add(marker);
            }
        }

        RaiseErrors();
    }

    private async Task DeleteAsync()
    {
        if (_onDelete is not { } handler || SelectedMarker is not { } marker)
        {
            return;
        }

        if (await handler(marker.Id).ConfigureAwait(true))
        {
            var index = IndexOf(marker.Id);
            if (index >= 0)
            {
                Markers.RemoveAt(index);
            }

            SelectedMarker = null;
        }
    }

    private int IndexOf(Guid markerId)
    {
        for (var index = 0; index < Markers.Count; index++)
        {
            if (Markers[index].Id == markerId)
            {
                return index;
            }
        }

        return -1;
    }

    private void ClearErrors()
    {
        _hasRangeError = false;
        _hasOverlapError = false;
        RaiseErrors();
    }

    private void RaiseErrors()
    {
        OnPropertyChanged(nameof(HasRangeError));
        OnPropertyChanged(nameof(HasOverlapError));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
