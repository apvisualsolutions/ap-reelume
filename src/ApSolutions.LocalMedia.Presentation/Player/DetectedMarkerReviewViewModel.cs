using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

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
        _onLoad = onLoad;
        _onAccept = onAccept;
        _onCorrect = onCorrect;
        _onDelete = onDelete;
        _hasRangeError = false;
        AcceptCommand = new ReviewCommand(AcceptAsync);
        CorrectCommand = new ReviewCommand(CorrectAsync);
        DeleteCommand = new ReviewCommand(DeleteAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand AcceptCommand { get; }

    public ICommand CorrectCommand { get; }

    public ICommand DeleteCommand { get; }

    public ObservableCollection<DetectedMarker> Detections { get; } = [];

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class ReviewCommand(Func<Task> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public async void Execute(object? parameter)
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            await execute().ConfigureAwait(true);
        }
    }
}
