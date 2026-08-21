// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Reviews what the detector found for one episode: accept a range as it stands, adjust it, or
/// remove it. Accepting or adjusting is a human act, so the row survives every later run.
/// </summary>
public sealed class DetectedMarkerReviewViewModel : INotifyPropertyChanged
{
    private readonly Func<MediaFileId, Task<IReadOnlyList<DetectedMarker>>>? _onLoad;
    private readonly Func<Guid, Task<DetectedMarker?>>? _onAccept;
    private readonly Func<Guid, TimeSpan, TimeSpan, Task<DetectedMarker?>>? _onCorrect;
    private readonly Func<Guid, Task<bool>>? _onDelete;
    private DetectedMarker? _selected;
    private double _startSeconds;
    private double _endSeconds;
    private bool _hasRangeError;

    public DetectedMarkerReviewViewModel(
        Func<MediaFileId, Task<IReadOnlyList<DetectedMarker>>>? onLoad = null,
        Func<Guid, Task<DetectedMarker?>>? onAccept = null,
        Func<Guid, TimeSpan, TimeSpan, Task<DetectedMarker?>>? onCorrect = null,
        Func<Guid, Task<bool>>? onDelete = null)
    {
        // The empty state is derived from the list, so the list is what announces it: every
        // path that adds or clears one would otherwise have to remember, and one that forgot
        // would leave the panel saying it is empty over a list with something in it.
        Detections.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        _onLoad = onLoad;
        _onAccept = onAccept;
        _onCorrect = onCorrect;
        _onDelete = onDelete;
        _hasRangeError = false;
        AcceptCommand = Announcing(AcceptAsync);
        CorrectCommand = Announcing(CorrectAsync);
        DeleteCommand = Announcing(DeleteAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand AcceptCommand { get; }

    public ICommand CorrectCommand { get; }

    public ICommand DeleteCommand { get; }

    public ObservableCollection<DetectedMarker> Detections { get; } = [];

    /// <summary>Nothing was detected in this file, which is not the same as detection being off.</summary>
    public bool IsEmpty => Detections.Count == 0;

    public DetectedMarker? Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
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

    /// <summary>True when the last correction was refused because the range made no sense.</summary>
    public bool HasRangeError => _hasRangeError;

    /// <summary>Shows the detections of one episode; changing episodes replaces the list.</summary>
    public async Task LoadAsync(MediaFileId fileId)
    {
        Detections.Clear();
        if (_onLoad is { } handler)
        {
            foreach (var row in await handler(fileId).ConfigureAwait(true))
            {
                Detections.Add(row);
            }
        }

        Selected = null;
        SetRangeError(false);
    }

    private async Task AcceptAsync()
    {
        if (_onAccept is not { } handler || Selected is not { } row)
        {
            return;
        }

        if (await handler(row.Id).ConfigureAwait(true) is { } accepted)
        {
            Replace(accepted);
            Selected = accepted;
        }
    }

    private async Task CorrectAsync()
    {
        if (_onCorrect is not { } handler || Selected is not { } row)
        {
            return;
        }

        var corrected = await handler(
                row.Id,
                TimeSpan.FromSeconds(StartSeconds),
                TimeSpan.FromSeconds(EndSeconds))
            .ConfigureAwait(true);
        if (corrected is null)
        {
            SetRangeError(true);
            return;
        }

        Replace(corrected);
        Selected = corrected;
        SetRangeError(false);
    }

    private async Task DeleteAsync()
    {
        if (_onDelete is not { } handler || Selected is not { } row)
        {
            return;
        }

        if (await handler(row.Id).ConfigureAwait(true))
        {
            var index = IndexOf(row.Id);
            if (index >= 0)
            {
                Detections.RemoveAt(index);
            }

            Selected = null;
        }
    }

    private void Replace(DetectedMarker row)
    {
        var index = IndexOf(row.Id);
        if (index >= 0)
        {
            Detections[index] = row;
        }
        else
        {
            Detections.Add(row);
        }
    }

    private int IndexOf(Guid markerId)
    {
        for (var index = 0; index < Detections.Count; index++)
        {
            if (Detections[index].Id == markerId)
            {
                return index;
            }
        }

        return -1;
    }

    private void SetRangeError(bool value)
    {
        _hasRangeError = value;
        OnPropertyChanged(nameof(HasRangeError));
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

    /// <summary>
    /// One review button, which announces that the answer to <c>CanExecute</c> may have changed each
    /// time it runs.
    /// </summary>
    /// <remarks>
    /// Kept because the class this replaced did it and a test says so, not because it earns its keep:
    /// these three can always execute, so nothing that re-asks can get a different answer. ARQ-004 is
    /// a move, and a move that quietly changes what a surface does is not a move.
    /// <para>
    /// Decided 2026-08-10, so it is not left as an open question: this goes, along with the assertion
    /// that pins it, the next time somebody opens this file for a reason of their own. Not before —
    /// editing a green test with no functional change behind it is worse than the noise it removes,
    /// and this noise costs nothing but a line.
    /// </para>
    /// </remarks>
    private static AsyncRelayCommand Announcing(Func<Task> execute)
    {
        AsyncRelayCommand? command = null;
        command = new AsyncRelayCommand(() =>
        {
            command!.RaiseCanExecuteChanged();
            return execute();
        });
        return command;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
