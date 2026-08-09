using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>What the person did with the offer of the next episode.</summary>
public enum NextEpisodeAction
{
    PlayNow,
    Cancel,
}

/// <summary>
/// The countdown overlay. It states which episode is next and how long is left, and both of its
/// buttons end the wait immediately, so nobody has to sit through it.
/// </summary>
public sealed class NextEpisodeViewModel : INotifyPropertyChanged
{
    private readonly Func<NextEpisodeAction, Task>? _onAction;
    private string _episodeLabel = string.Empty;
    private int _remainingSeconds;
    private bool _isVisible;

    public NextEpisodeViewModel(Func<NextEpisodeAction, Task>? onAction = null)
    {
        _onAction = onAction;
        PlayNowCommand = new OverlayCommand(() => ActAsync(NextEpisodeAction.PlayNow));
        CancelCommand = new OverlayCommand(() => ActAsync(NextEpisodeAction.Cancel));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand PlayNowCommand { get; }

    public ICommand CancelCommand { get; }

    public bool IsVisible => _isVisible;

    /// <summary>How the next episode is named, already formatted by the caller.</summary>
    public string EpisodeLabel => _episodeLabel;

    public int RemainingSeconds => _remainingSeconds;

    /// <summary>The seconds left, as the digits the overlay shows.</summary>
    public string CountdownText => _remainingSeconds.ToString(CultureInfo.CurrentCulture);

    /// <summary>Shows the offer with the seconds the countdown starts from.</summary>
    public void Offer(string episodeLabel, int remainingSeconds)
    {
        _episodeLabel = episodeLabel ?? throw new ArgumentNullException(nameof(episodeLabel));
        _remainingSeconds = remainingSeconds;
        _isVisible = true;
        RaiseAll();
    }

    /// <summary>Updates the digits as the countdown advances.</summary>
    public void Tick(int remainingSeconds)
    {
        _remainingSeconds = remainingSeconds;
        OnPropertyChanged(nameof(RemainingSeconds));
        OnPropertyChanged(nameof(CountdownText));
    }

    /// <summary>Takes the offer away, whether it was accepted, refused, or simply expired.</summary>
    public void Hide()
    {
        _isVisible = false;
        RaiseAll();
    }

    private async Task ActAsync(NextEpisodeAction action)
    {
        if (!_isVisible)
        {
            return;
        }

        Hide();
        if (_onAction is { } handler)
        {
            await handler(action).ConfigureAwait(true);
        }
    }

    private void RaiseAll()
    {
        foreach (var name in new[]
        {
            nameof(IsVisible),
            nameof(EpisodeLabel),
            nameof(RemainingSeconds),
            nameof(CountdownText),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class OverlayCommand(Func<Task> execute) : ICommand
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
